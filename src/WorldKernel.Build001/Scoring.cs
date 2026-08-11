using System.Text.Json;

namespace StealthEye.WorldKernel.Build001;

public sealed record PredictionScore(
    string EligibilityStatus,
    double? MeanBrierLoss,
    IReadOnlyDictionary<string, double?> BrierComponents,
    int DeltaTruePositive,
    int DeltaFalsePositive,
    int DeltaFalseNegative,
    double? DeltaPrecision,
    double? DeltaRecall,
    double? DeltaF1,
    IReadOnlyList<string> InvariantViolations,
    IReadOnlyList<string> FormatDefects);

public static class PredictionScorer
{
    public static PredictionScore Score(
        string semanticAction,
        IReadOnlyDictionary<string, double?> suppliedProbabilities,
        IReadOnlyDictionary<string, bool?> actualPropositions,
        IReadOnlyCollection<string> expectedDeltas,
        IReadOnlyCollection<string> actualDeltas,
        IReadOnlyCollection<string> expectedInvariants,
        IReadOnlyCollection<string> violatedInvariants,
        string unresolvedStatus = "unknown")
    {
        var normalized = Build001Contract.NormalizePrediction(semanticAction, suppliedProbabilities, out var defects);
        var components = new SortedDictionary<string, double?>(StringComparer.Ordinal);
        var unresolved = false;
        foreach (var proposition in Build001Contract.ForAction(semanticAction))
        {
            if (!actualPropositions.TryGetValue(proposition, out var outcome) || outcome is null)
            {
                components[proposition] = null;
                unresolved = true;
                continue;
            }

            var y = outcome.Value ? 1.0 : 0.0;
            var error = normalized[proposition] - y;
            components[proposition] = error * error;
        }

        var predictedDeltaSet = expectedDeltas.ToHashSet(StringComparer.Ordinal);
        var actualDeltaSet = actualDeltas.ToHashSet(StringComparer.Ordinal);
        var tp = predictedDeltaSet.Intersect(actualDeltaSet).Count();
        var fp = predictedDeltaSet.Except(actualDeltaSet).Count();
        var fn = actualDeltaSet.Except(predictedDeltaSet).Count();
        var precision = tp + fp == 0 ? (double?)null : (double)tp / (tp + fp);
        var recall = tp + fn == 0 ? (double?)null : (double)tp / (tp + fn);
        var f1 = precision is null || recall is null || precision + recall == 0
            ? null
            : 2 * precision * recall / (precision + recall);

        var expectedInvariantSet = expectedInvariants.ToHashSet(StringComparer.Ordinal);
        var violations = violatedInvariants
            .Where(expectedInvariantSet.Contains)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        return new PredictionScore(
            unresolved ? unresolvedStatus : "eligible",
            unresolved ? null : components.Values.Average(value => value!.Value),
            components,
            tp,
            fp,
            fn,
            precision,
            recall,
            f1,
            violations,
            defects);
    }
}

public sealed record PairedBlock(string BlockId, double MemoryLoss, double StructuredLoss, double? ColdLoss = null);

public sealed record PrimaryStatistics(
    int BlockCount,
    double MeanMemoryBrier,
    double MeanStructuredBrier,
    double MeanDifference,
    double RelativeReduction,
    double BootstrapDifferenceLower,
    double BootstrapDifferenceUpper,
    double RandomizationPValue,
    int BootstrapResamples,
    int RandomizationResamples,
    string RandomizationMethod,
    string DeterministicSeedHash);

public static class PreregisteredStatistics
{
    public const int BootstrapResamples = 10_000;
    public const int RandomizationMonteCarloResamples = 100_000;

    public static PrimaryStatistics Analyze(IReadOnlyList<PairedBlock> blocks, string inputManifestHash)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        if (blocks.Count == 0)
        {
            throw new ArgumentException("At least one complete matched block is required.", nameof(blocks));
        }
        if (blocks.Select(block => block.BlockId).Distinct(StringComparer.Ordinal).Count() != blocks.Count)
        {
            throw new ArgumentException("Configuration block IDs must be unique independent units.", nameof(blocks));
        }
        if (blocks.Any(block => !IsLoss(block.MemoryLoss) || !IsLoss(block.StructuredLoss)))
        {
            throw new ArgumentOutOfRangeException(nameof(blocks), "Brier block losses must be finite values in [0,1].");
        }

        var memoryMean = blocks.Average(block => block.MemoryLoss);
        var structuredMean = blocks.Average(block => block.StructuredLoss);
        if (memoryMean <= 0)
        {
            throw new InvalidOperationException("Relative improvement is undefined when Memory mean Brier loss is zero.");
        }
        var differences = blocks.Select(block => block.MemoryLoss - block.StructuredLoss).ToArray();
        var observedDifference = differences.Average();
        var relative = observedDifference / memoryMean;

        var seedBytes = Convert.FromHexString(inputManifestHash);
        var seed = BitConverter.ToInt32(seedBytes, 0);
        var bootstrapRandom = new Random(seed);
        var bootstrap = new double[BootstrapResamples];
        for (var resample = 0; resample < bootstrap.Length; resample++)
        {
            var total = 0.0;
            for (var i = 0; i < blocks.Count; i++)
            {
                total += differences[bootstrapRandom.Next(blocks.Count)];
            }
            bootstrap[resample] = total / blocks.Count;
        }
        Array.Sort(bootstrap);
        var lower = Percentile(bootstrap, 0.025);
        var upper = Percentile(bootstrap, 0.975);

        var randomizationSeed = seed ^ unchecked((int)0x9e3779b9);
        var randomizationRandom = new Random(randomizationSeed);
        long atLeastAsExtreme = 0;
        for (var resample = 0; resample < RandomizationMonteCarloResamples; resample++)
        {
            var total = 0.0;
            foreach (var difference in differences)
            {
                total += randomizationRandom.Next(2) == 0 ? difference : -difference;
            }
            var permuted = total / differences.Length;
            if (Math.Abs(permuted) >= Math.Abs(observedDifference) - 1e-15)
            {
                atLeastAsExtreme++;
            }
        }
        var pValue = (atLeastAsExtreme + 1.0) / (RandomizationMonteCarloResamples + 1.0);

        return new PrimaryStatistics(
            blocks.Count,
            memoryMean,
            structuredMean,
            observedDifference,
            relative,
            lower,
            upper,
            pValue,
            BootstrapResamples,
            RandomizationMonteCarloResamples,
            "paired two-sided Monte Carlo label-swap randomization with add-one correction",
            inputManifestHash);
    }

    public static int PilotDerivedBlockCount(double meanMemoryBrier, double pairedDifferenceStandardDeviation)
    {
        if (meanMemoryBrier < 0.05)
        {
            throw new InvalidOperationException("Pilot Memory Brier is below the preregistered headroom floor; redesign and rerun pilot.");
        }
        if (pairedDifferenceStandardDeviation < 0 || double.IsNaN(pairedDifferenceStandardDeviation))
        {
            throw new ArgumentOutOfRangeException(nameof(pairedDifferenceStandardDeviation));
        }
        var delta = 0.20 * meanMemoryBrier;
        var nRaw = Math.Pow((1.959964 + 0.841621) * pairedDifferenceStandardDeviation / delta, 2);
        var n = (int)Math.Ceiling(Math.Max(48, nRaw) / 8.0) * 8;
        if (n > 96)
        {
            throw new InvalidOperationException($"Pilot-derived N={n} exceeds the preregistered maximum 96; redesign before confirmatory execution.");
        }
        return n;
    }

    public static byte[] SerializeInput(IReadOnlyList<PairedBlock> blocks) => CanonicalJson.Serialize(
        blocks.OrderBy(block => block.BlockId, StringComparer.Ordinal).Select(block => new
        {
            block_id = block.BlockId,
            memory_loss = block.MemoryLoss,
            structured_loss = block.StructuredLoss,
            cold_loss = block.ColdLoss
        }).ToArray());

    private static bool IsLoss(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value is >= 0 and <= 1;

    private static double Percentile(IReadOnlyList<double> sorted, double probability)
    {
        var position = probability * (sorted.Count - 1);
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return sorted[lower];
        }
        var fraction = position - lower;
        return sorted[lower] + (sorted[upper] - sorted[lower]) * fraction;
    }
}

public sealed record BehavioralRun(
    string BlockId,
    string Arm,
    int ActualMaterialActions,
    int OracleMinimumMaterialActions,
    bool Completed,
    int ConsequentialErrors)
{
    public int ExcessMaterialActions => Math.Max(0, ActualMaterialActions - OracleMinimumMaterialActions);
}

public sealed record BehavioralSummary(
    double MemoryMeanExcess,
    double StructuredMeanExcess,
    double? RelativeReduction,
    double MemoryCompletionRate,
    double StructuredCompletionRate,
    int MemoryConsequentialErrors,
    int StructuredConsequentialErrors,
    bool PassesGate);

public static class BehavioralScorer
{
    public static BehavioralSummary Analyze(IReadOnlyCollection<BehavioralRun> runs)
    {
        var memory = runs.Where(run => run.Arm == "memory").ToArray();
        var structured = runs.Where(run => run.Arm == "structured").ToArray();
        if (memory.Length == 0 || structured.Length == 0 || memory.Length != structured.Length)
        {
            throw new ArgumentException("Behavioral analysis requires complete matched Memory and Structured runs.", nameof(runs));
        }
        var memoryExcess = memory.Average(run => run.ExcessMaterialActions);
        var structuredExcess = structured.Average(run => run.ExcessMaterialActions);
        double? relative = memoryExcess == 0 ? null : (memoryExcess - structuredExcess) / memoryExcess;
        var memoryCompletion = memory.Count(run => run.Completed) / (double)memory.Length;
        var structuredCompletion = structured.Count(run => run.Completed) / (double)structured.Length;
        var memoryErrors = memory.Sum(run => run.ConsequentialErrors);
        var structuredErrors = structured.Sum(run => run.ConsequentialErrors);
        var actionGate = memoryExcess == 0 ? structuredExcess <= 1e-12 : relative >= 0.20;
        var completionGate = structuredCompletion >= memoryCompletion - 0.05;
        var errorGate = structuredErrors <= memoryErrors;
        return new BehavioralSummary(
            memoryExcess,
            structuredExcess,
            relative,
            memoryCompletion,
            structuredCompletion,
            memoryErrors,
            structuredErrors,
            actionGate && completionGate && errorGate);
    }
}
