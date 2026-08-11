namespace StealthEye.WorldKernel.Build001.Tests;

internal static class AssertEx
{
    public static void True(bool condition, string message = "Expected condition to be true.")
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void False(bool condition, string message = "Expected condition to be false.") => True(!condition, message);

    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(message ?? $"Expected '{expected}', received '{actual}'.");
        }
    }

    public static void Near(double expected, double actual, double tolerance, string? message = null)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException(message ?? $"Expected {expected} ± {tolerance}, received {actual}.");
        }
    }

    public static async Task<TException> ThrowsAsync<TException>(Func<Task> action, Func<TException, bool>? predicate = null)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException exception) when (predicate is null || predicate(exception))
        {
            return exception;
        }
        throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
    }
}

