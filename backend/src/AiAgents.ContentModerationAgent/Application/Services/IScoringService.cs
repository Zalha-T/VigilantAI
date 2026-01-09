using AiAgents.ContentModerationAgent.Domain.Entities;

namespace AiAgents.ContentModerationAgent.Application.Services;

public interface IScoringService
{
    Task<Prediction> ScoreAndDecideAsync(Content content, CancellationToken cancellationToken = default);
}
