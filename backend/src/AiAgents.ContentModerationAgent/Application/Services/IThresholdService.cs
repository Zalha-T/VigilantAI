using AiAgents.ContentModerationAgent.Domain.Entities;

namespace AiAgents.ContentModerationAgent.Application.Services;

public interface IThresholdService
{
    Task<SystemSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateThresholdsAsync(double allowThreshold, double reviewThreshold, double blockThreshold, CancellationToken cancellationToken = default);
    Task UpdateRetrainThresholdAsync(int retrainThreshold, CancellationToken cancellationToken = default);
}
