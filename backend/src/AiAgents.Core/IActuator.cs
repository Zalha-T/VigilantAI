namespace AiAgents.Core;

/// <summary>
/// Represents an actuator that executes actions and produces results.
/// </summary>
/// <typeparam name="TAction">The type of action</typeparam>
/// <typeparam name="TResult">The type of result</typeparam>
public interface IActuator<TAction, TResult>
{
    /// <summary>
    /// Executes the given action and returns the result.
    /// </summary>
    Task<TResult> ExecuteAsync(TAction action, CancellationToken cancellationToken = default);
}
