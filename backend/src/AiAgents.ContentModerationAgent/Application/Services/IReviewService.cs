using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Domain.Enums;

namespace AiAgents.ContentModerationAgent.Application.Services;

public interface IReviewService
{
    Task<Review> CreateReviewAsync(Guid contentId, CancellationToken cancellationToken = default);
    Task UpdateReviewAsync(Guid reviewId, ModerationDecision goldLabel, bool? correctDecision, string? feedback, Guid? moderatorId, CancellationToken cancellationToken = default);
    Task IncrementGoldLabelCounterAsync(CancellationToken cancellationToken = default);
}
