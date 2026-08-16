using System.Collections.Concurrent;

namespace SingleFlight;

/// <summary>
/// An isolated, instance-based scope of coalesced calls. See <see cref="ISingleFlightGroup{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the produced value.</typeparam>
public sealed class SingleFlightGroup<T> : ISingleFlightGroup<T>
{
    private readonly ConcurrentDictionary<string, Lazy<Task<T>>> _calls;

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleFlightGroup{T}"/> class that compares keys
    /// with <see cref="StringComparer.Ordinal"/>.
    /// </summary>
    public SingleFlightGroup()
        : this(StringComparer.Ordinal)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleFlightGroup{T}"/> class that compares keys
    /// with <paramref name="keyComparer"/>.
    /// </summary>
    /// <param name="keyComparer">Decides which keys count as equal for coalescing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keyComparer"/> is <see langword="null"/>.</exception>
    public SingleFlightGroup(IEqualityComparer<string> keyComparer) =>
        _calls = new ConcurrentDictionary<string, Lazy<Task<T>>>(keyComparer ?? throw new ArgumentNullException(nameof(keyComparer)));

    /// <inheritdoc />
    public Task<T> RunAsync(string key, Func<Task<T>> factory)
    {
        ValidateArguments(key, factory);
        return Start(key, factory).Task;
    }

    /// <inheritdoc />
    public async Task<SingleFlightResult<T>> RunDetailedAsync(string key, Func<Task<T>> factory)
    {
        ValidateArguments(key, factory);
        var (task, joined) = Start(key, factory);
        return new SingleFlightResult<T>(await task.ConfigureAwait(false), joined);
    }

    private static void ValidateArguments(string key, Func<Task<T>> factory)
    {
        if (key is null)
            throw new ArgumentNullException(nameof(key));
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));
    }

    private (Task<T> Task, bool Joined) Start(string key, Func<Task<T>> factory)
    {
        if (_calls.TryGetValue(key, out var pending))
            return (pending.Value, true);

        var flight = new Lazy<Task<T>>(() => ExecuteAsync(key, factory));
        var stored = _calls.GetOrAdd(key, flight);
        return (stored.Value, !ReferenceEquals(stored, flight));
    }

    private async Task<T> ExecuteAsync(string key, Func<Task<T>> factory)
    {
        try
        {
            return await factory().ConfigureAwait(false);
        }
        finally
        {
            _calls.TryRemove(key, out _);
        }
    }
}
