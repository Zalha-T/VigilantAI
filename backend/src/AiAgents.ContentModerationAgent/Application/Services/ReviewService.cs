using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Domain.Enums;
using AiAgents.ContentModerationAgent.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.ContentModerationAgent.Application.Services;

public class ReviewService : IReviewService
{
    private readonly ContentModerationDbContext _context;

    public ReviewService(ContentModerationDbContext context)
    {
        _context = context;
    }

    public async Task<Review> CreateReviewAsync(Guid contentId, CancellationToken cancellationToken = default)
    {
        var review = new Review
        {
            Id = Guid.NewGuid(),
            ContentId = contentId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync(cancellationToken);

        return review;
    }

    public async Task UpdateReviewAsync(Guid reviewId, ModerationDecision goldLabel, bool? correctDecision, string? feedback, Guid? moderatorId, CancellationToken cancellationToken = default)
    {
        var review = await _context.Reviews
            .Include(r => r.Content)
            .FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken);
        
        if (review == null)
            throw new InvalidOperationException($"Review {reviewId} not found");

        review.GoldLabel = goldLabel;
        review.CorrectDecision = correctDecision;
        review.Feedback = feedback;
        review.ModeratorId = moderatorId;
        review.ReviewedAt = DateTime.UtcNow;

        // Update content status based on gold label
        if (review.Content != null)
        {
            review.Content.Status = goldLabel switch
            {
                ModerationDecision.Allow => ContentStatus.Approved,
                ModerationDecision.Block => ContentStatus.Blocked,
                ModerationDecision.Review => ContentStatus.PendingReview, // Keep in review if moderator says it needs more review
                _ => review.Content.Status
            };
            review.Content.ProcessedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Increment gold label counter (gold label is always provided)
        await IncrementGoldLabelCounterAsync(cancellationToken);
    }

    public async Task IncrementGoldLabelCounterAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _context.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings != null)
        {
            settings.NewGoldSinceLastTrain++;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
