namespace AiAgents.Core;

/// <summary>
/// Represents a learning component that learns from experiences.
/// </summary>
/// <typeparam name="TExperience">The type of experience</typeparam>
public interface ILearningComponent<TExperience>
{
    /// <summary>
    /// Learns from the given experience.
    /// </summary>
    Task LearnAsync(TExperience experience, CancellationToken cancellationToken = default);
}
