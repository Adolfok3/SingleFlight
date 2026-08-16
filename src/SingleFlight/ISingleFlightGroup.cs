namespace SingleFlight;

/// <summary>
/// An isolated scope of coalesced calls. Unlike the static <see cref="SingleFlight{T}"/>, each group
/// owns its own set of in-flight keys, so different groups never coalesce with each other even for an
/// equal key. Groups are safe for concurrent use and are intended to be long-lived (e.g. registered as
/// a singleton in a dependency-injection container).
/// </summary>
/// <typeparam name="T">The type of the produced value.</typeparam>
public interface ISingleFlightGroup<T>
{
    /// <summary>
    /// Runs <paramref name="factory"/> for <paramref name="key"/>, or waits for the in-flight call if
    /// one is already running for that key. All callers receive the same value.
    /// </summary>
    /// <param name="key">Identifies the work. Concurrent calls with an equal key are coalesced.</param>
    /// <param name="factory">Produces the value. It runs only for the caller that owns the call.</param>
    /// <returns>The value produced by the single shared execution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="factory"/> is <see langword="null"/>.</exception>
    Task<T> RunAsync(string key, Func<Task<T>> factory);

    /// <summary>
    /// Like <see cref="RunAsync"/>, but also reports whether this caller joined an already in-flight
    /// call via <see cref="SingleFlightResult{T}.Joined"/>.
    /// </summary>
    /// <param name="key">Identifies the work. Concurrent calls with an equal key are coalesced.</param>
    /// <param name="factory">Produces the value. It runs only for the caller that owns the call.</param>
    /// <returns>The shared value together with whether this caller joined an in-flight call.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="factory"/> is <see langword="null"/>.</exception>
    Task<SingleFlightResult<T>> RunDetailedAsync(string key, Func<Task<T>> factory);
}
