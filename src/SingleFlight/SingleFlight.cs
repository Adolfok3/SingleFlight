using System.Collections.Concurrent;

namespace SingleFlight;

/// <summary>
/// Coalesces concurrent work by key: while a call for a key is running, other calls with the same key
/// share its result instead of running again. This is not a cache — once the call finishes the key is
/// released and the next call runs fresh.
/// </summary>
/// <typeparam name="T">The type of the produced value.</typeparam>
public static class SingleFlight<T>
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<T>>> Calls = new(StringComparer.Ordinal);

    /// <summary>
    /// Runs <paramref name="factory"/> for <paramref name="key"/>, or waits for the in-flight call if
    /// one is already running for that key. All callers receive the same value.
    /// </summary>
    /// <param name="key">Identifies the work. Concurrent calls with an equal key are coalesced.</param>
    /// <param name="factory">Produces the value. It runs only for the caller that owns the call.</param>
    /// <returns>The value produced by the single shared execution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="factory"/> is <see langword="null"/>.</exception>
    public static Task<T> RunAsync(string key, Func<Task<T>> factory)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));

        if (Calls.TryGetValue(key, out var pending))
            return pending.Value;

        var flight = new Lazy<Task<T>>(() => ExecuteAsync(key, factory));
        return Calls.GetOrAdd(key, flight).Value;
    }

    private static async Task<T> ExecuteAsync(string key, Func<Task<T>> factory)
    {
        try
        {
            return await factory().ConfigureAwait(false);
        }
        finally
        {
            Calls.TryRemove(key, out _);
        }
    }
}
