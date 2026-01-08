namespace AiAgents.Core;

/// <summary>
/// Represents a source of perceptions for an agent.
/// </summary>
/// <typeparam name="T">The type of perception</typeparam>
public interface IPerceptionSource<T>
{
    /// <summary>
    /// Gets the next perception from the source.
    /// Returns null if no perception is available.
    /// </summary>
    Task<T?> GetNextAsync(CancellationToken cancellationToken = default);
}
