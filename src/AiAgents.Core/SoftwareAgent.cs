namespace AiAgents.Core;

/// <summary>
/// Base class for software agents following the Sense-Think-Act-Learn cycle.
/// </summary>
/// <typeparam name="TPercept">The type of perception</typeparam>
/// <typeparam name="TAction">The type of action</typeparam>
/// <typeparam name="TResult">The type of result</typeparam>
/// <typeparam name="TExperience">The type of experience for learning</typeparam>
public abstract class SoftwareAgent<TPercept, TAction, TResult, TExperience>
{
    protected readonly IPerceptionSource<TPercept> PerceptionSource;
    protected readonly IPolicy<TPercept, TAction> Policy;
    protected readonly IActuator<TAction, TResult> Actuator;
    protected readonly ILearningComponent<TExperience>? LearningComponent;

    protected SoftwareAgent(
        IPerceptionSource<TPercept> perceptionSource,
        IPolicy<TPercept, TAction> policy,
        IActuator<TAction, TResult> actuator,
        ILearningComponent<TExperience>? learningComponent = null)
    {
        PerceptionSource = perceptionSource;
        Policy = policy;
        Actuator = actuator;
        LearningComponent = learningComponent;
    }

    /// <summary>
    /// Executes one step of the agent cycle (Sense → Think → Act → Learn).
    /// Returns null if no work is available.
    /// </summary>
    public async Task<TResult?> StepAsync(CancellationToken cancellationToken = default)
    {
        // SENSE
        var percept = await PerceptionSource.GetNextAsync(cancellationToken);
        if (percept == null)
            return default;

        // THINK
        var action = await Policy.DecideAsync(percept, cancellationToken);

        // ACT
        var result = await Actuator.ExecuteAsync(action, cancellationToken);

        // LEARN (optional)
        if (LearningComponent != null && result != null)
        {
            var experience = CreateExperience(percept, action, result);
            if (experience != null)
            {
                await LearningComponent.LearnAsync(experience, cancellationToken);
            }
        }

        return result;
    }

    /// <summary>
    /// Creates an experience from the perception, action, and result.
    /// Override this method to provide custom experience creation logic.
    /// </summary>
    protected virtual TExperience? CreateExperience(TPercept percept, TAction action, TResult result)
    {
        return default;
    }
}
