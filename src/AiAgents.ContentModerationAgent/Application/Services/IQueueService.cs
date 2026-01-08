using AiAgents.ContentModerationAgent.Domain.Entities;

namespace AiAgents.ContentModerationAgent.Application.Services;

public interface IQueueService
{
    Task<Content?> DequeueNextAsync(CancellationToken cancellationToken = default);
    Task EnqueueAsync(Content content, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid contentId, Domain.Enums.ContentStatus status, CancellationToken cancellationToken = default);
}
