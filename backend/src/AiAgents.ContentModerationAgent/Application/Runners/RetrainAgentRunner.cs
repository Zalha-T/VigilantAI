using AiAgents.ContentModerationAgent.Application.Services;

namespace AiAgents.ContentModerationAgent.Application.Runners;

/// <summary>
/// Retraining agent runner implementing Sense → Think → Act → Learn cycle.
/// Checks if retraining is needed and executes it.
/// </summary>
public class RetrainAgentRunner
{
    private readonly ITrainingService _trainingService;
    private readonly IThresholdService _thresholdService;

    public RetrainAgentRunner(
        ITrainingService trainingService,
        IThresholdService thresholdService)
    {
        _trainingService = trainingService;
        _thresholdService = thresholdService;
    }

    /// <summary>
    /// Executes one tick of the retrain agent.
    /// Returns true if retraining was performed, false otherwise.
    /// </summary>
    public async Task<bool> TickAsync(CancellationToken cancellationToken = default)
    {
        // SENSE: Check if retraining is needed
        var shouldRetrain = await _trainingService.ShouldRetrainAsync(cancellationToken);
        if (!shouldRetrain)
            return false; // No work available

        // THINK: Decision already made (shouldRetrain = true)
        
        // ACT: Train new model
        await _trainingService.TrainModelAsync(activate: true, cancellationToken);

        // LEARN: Counter reset and settings update are handled in TrainingService
        // This is the learning component - the system has learned from new gold labels

        return true;
    }
}
