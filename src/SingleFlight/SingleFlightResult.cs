namespace SingleFlight;

/// <summary>
/// The outcome of a coalesced call: the produced <see cref="Value"/> plus whether this caller
/// <see cref="Joined"/> a call that was already in flight instead of running the factory itself.
/// </summary>
/// <typeparam name="T">The type of the produced value.</typeparam>
public readonly struct SingleFlightResult<T>
{
    internal SingleFlightResult(T value, bool joined)
    {
        Value = value;
        Joined = joined;
    }

    /// <summary>The value produced by the single shared execution.</summary>
    public T Value { get; }

    /// <summary>
    /// <see langword="true"/> when this caller attached to a call that was already running for the key
    /// (it did not execute the factory); <see langword="false"/> when this caller owned the execution.
    /// </summary>
    public bool Joined { get; }

    /// <summary>Deconstructs the result into its <see cref="Value"/> and <see cref="Joined"/> parts.</summary>
    /// <param name="value">Receives <see cref="Value"/>.</param>
    /// <param name="joined">Receives <see cref="Joined"/>.</param>
    public void Deconstruct(out T value, out bool joined)
    {
        value = Value;
        joined = Joined;
    }
}
