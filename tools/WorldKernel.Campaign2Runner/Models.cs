using System.Text.Json;

namespace StealthEye.WorldKernel.Campaign2Runner;

public sealed record ObservationEnvelope(
    string ProviderNamespace,
    string ObserverName,
    string ObserverVersion,
    DateTimeOffset CapturedAt,
    string? ProviderRevision,
    string AcquisitionStatus,
    JsonElement Payload,
    IReadOnlyList<string> PublicFacts);

public sealed record SeedCommitInput(
    string SeedId,
    string Phase,
    string ConfigurationBlockId,
    string CommitmentSha256,
    string SealedPayloadRef,
    string PublicFixtureRevision);

public sealed record HiddenConfigurationInput(
    string SeedId,
    string RegimeLabel,
    JsonElement Configuration,
    string ExpectedResetFingerprint,
    string AnswerKeyVersion);

public sealed record ResetVerificationInput(
    string SeedId,
    string Arm,
    Guid GenerationId,
    string ActualFingerprint,
    string ExpectedFingerprint,
    IReadOnlyList<string> ProviderEvidenceHashes,
    bool Passed);

public sealed record ArmRandomizationInput(
    string ConfigurationBlockId,
    string SeedId,
    IReadOnlyList<string> ArmOrder,
    string RandomizerVersion,
    string RandomizationProof);

public sealed record TrialDeclareInput(
    string TrialId,
    string ConfigurationBlockId,
    string Phase,
    string Arm,
    string SemanticAction,
    string FixtureScopeId,
    string BranchName,
    string WorkingCopy,
    string FixtureManifestationRef,
    string PublicTopologyClass,
    string ProviderVersionFingerprint,
    string SharedExactCommit,
    string EnvironmentFingerprint,
    JsonElement Parameters,
    IReadOnlyDictionary<string, double?> Prediction,
    IReadOnlyList<string> ExpectedDeltas,
    IReadOnlyList<string> ExpectedInvariants,
    JsonElement Horizons,
    JsonElement ProducerModel,
    ObservationEnvelope LocalObservation,
    ObservationEnvelope ProviderObservation,
    string SubjectAttestationRef,
    string IsolatedSessionId,
    string ModelConfigurationHash,
    string CommonInstructionsHash,
    string? InheritedPackageHash,
    int InheritedTokens,
    IReadOnlyList<Guid> SourceEpisodeIds);

public sealed record TrialLedgerState(
    string TrialId,
    string ConfigurationBlockId,
    string Phase,
    string Arm,
    string SemanticAction,
    string FixtureManifestationRef,
    string PublicTopologyClass,
    string ProviderVersionFingerprint,
    string SharedExactCommit,
    string EnvironmentFingerprint,
    DateTimeOffset PreKnownAt,
    Guid LocalManifestationId,
    Guid HostedManifestationId,
    Guid CorrespondenceId,
    IReadOnlyList<Guid> PreObservationIds,
    IReadOnlyList<Guid> PreClaimIds,
    IReadOnlyList<string> PreEvidenceHashes,
    Guid ActionId,
    Guid PredictionId,
    JsonElement Parameters,
    IReadOnlyDictionary<string, double> Prediction,
    IReadOnlyList<string> ExpectedDeltas,
    IReadOnlyList<string> ExpectedInvariants,
    JsonElement Horizons,
    IReadOnlyList<Guid> SourceEpisodeIds,
    IReadOnlyList<string> PreObservedFacts);

public sealed record TrialCloseInput(
    ObservationEnvelope LocalObservation,
    ObservationEnvelope ProviderObservation,
    IReadOnlyDictionary<string, bool?> ActualPropositions,
    IReadOnlyList<string> MaterialDeltas,
    IReadOnlyList<string> InvariantViolations,
    string ResolutionStatus,
    string AttributionStatus,
    string ResolverVersion,
    JsonElement Receipt,
    JsonElement LatencyMetrics);

public sealed record TrialCloseResult(
    Guid EpisodeId,
    Guid OutcomeId,
    Guid EvaluationId,
    double? MeanBrierLoss,
    string EligibilityStatus,
    string EpisodeExportPath,
    string EpisodeExportSha256);
