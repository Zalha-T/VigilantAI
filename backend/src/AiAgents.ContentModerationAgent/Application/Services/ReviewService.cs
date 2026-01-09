using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Domain.Enums;
using AiAgents.ContentModerationAgent.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiAgents.ContentModerationAgent.Application.Services;

public class ReviewService : IReviewService
{
    private readonly ContentModerationDbContext _context;
    private readonly ITrainingService? _trainingService;
    private readonly ILogger<ReviewService>? _logger;

    public ReviewService(
        ContentModerationDbContext context, 
        ITrainingService? trainingService = null,
        ILogger<ReviewService>? logger = null)
    {
        _context = context;
        _trainingService = trainingService; // Optional to avoid circular dependency issues
        _logger = logger;
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
        
        // Check if retraining should be triggered immediately
        await CheckAndTriggerRetrainingAsync(cancellationToken);
    }
    
    private async Task CheckAndTriggerRetrainingAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _context.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings != null && settings.RetrainingEnabled && 
            settings.NewGoldSinceLastTrain >= settings.RetrainThreshold)
        {
            // Trigger retraining immediately if training service is available
            if (_trainingService != null)
            {
                try
                {
                    var shouldRetrain = await _trainingService.ShouldRetrainAsync(cancellationToken);
                    if (shouldRetrain)
                    {
                        _logger?.LogInformation("🚀 IMMEDIATE RETRAINING TRIGGERED: Threshold reached ({Current}/{Threshold})", 
                            settings.NewGoldSinceLastTrain, settings.RetrainThreshold);
                        await _trainingService.TrainModelAsync(activate: true, cancellationToken);
                        _logger?.LogInformation("✅ Immediate retraining completed successfully");
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't fail the review update
                    System.Diagnostics.Debug.WriteLine($"Error triggering immediate retraining: {ex.Message}");
                }
            }
        }
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
