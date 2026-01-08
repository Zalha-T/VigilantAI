namespace AiAgents.Core;

/// <summary>
/// Represents a policy that maps perceptions to actions.
/// </summary>
/// <typeparam name="TPercept">The type of perception</typeparam>
/// <typeparam name="TAction">The type of action</typeparam>
public interface IPolicy<TPercept, TAction>
{
    /// <summary>
    /// Determines the action to take based on the given perception.
    /// </summary>
    Task<TAction> DecideAsync(TPercept percept, CancellationToken cancellationToken = default);
}
