namespace AiAgents.ContentModerationAgent.Application.Services;

public interface ITrainingService
{
    Task TrainModelAsync(bool activate = false, CancellationToken cancellationToken = default);
    Task<bool> ShouldRetrainAsync(CancellationToken cancellationToken = default);
}
