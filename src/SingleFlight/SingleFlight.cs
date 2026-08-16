namespace SingleFlight;

/// <summary>
/// Coalesces concurrent work by key: while a call for a key is running, other calls with the same key
/// share its result instead of running again. This is not a cache — once the call finishes the key is
/// released and the next call runs fresh.
/// </summary>
/// <remarks>
/// This is a process-wide shared scope per <typeparamref name="T"/>. For an isolated scope (different
/// keyspaces, dependency injection, per-test isolation) create a <see cref="SingleFlightGroup{T}"/>.
/// </remarks>
/// <typeparam name="T">The type of the produced value.</typeparam>
public static class SingleFlight<T>
{
    private static readonly SingleFlightGroup<T> Default = new(StringComparer.Ordinal);

    /// <summary>
    /// Runs <paramref name="factory"/> for <paramref name="key"/>, or waits for the in-flight call if
    /// one is already running for that key. All callers receive the same value.
    /// </summary>
    /// <param name="key">Identifies the work. Concurrent calls with an equal key are coalesced.</param>
    /// <param name="factory">Produces the value. It runs only for the caller that owns the call.</param>
    /// <returns>The value produced by the single shared execution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="factory"/> is <see langword="null"/>.</exception>
    public static Task<T> RunAsync(string key, Func<Task<T>> factory) =>
        Default.RunAsync(key, factory);

    /// <summary>
    /// Like <see cref="RunAsync"/>, but also reports whether this caller joined an already in-flight
    /// call via <see cref="SingleFlightResult{T}.Joined"/>.
    /// </summary>
    /// <param name="key">Identifies the work. Concurrent calls with an equal key are coalesced.</param>
    /// <param name="factory">Produces the value. It runs only for the caller that owns the call.</param>
    /// <returns>The shared value together with whether this caller joined an in-flight call.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="factory"/> is <see langword="null"/>.</exception>
    public static Task<SingleFlightResult<T>> RunDetailedAsync(string key, Func<Task<T>> factory) =>
        Default.RunDetailedAsync(key, factory);
}
