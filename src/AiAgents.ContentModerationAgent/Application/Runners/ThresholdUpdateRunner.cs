using AiAgents.ContentModerationAgent.Application.Services;
using AiAgents.ContentModerationAgent.Domain.Enums;
using AiAgents.ContentModerationAgent.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.ContentModerationAgent.Application.Runners;

/// <summary>
/// Threshold update agent runner implementing Sense → Think → Act → Learn cycle.
/// Adaptively updates thresholds based on feedback metrics.
/// </summary>
public class ThresholdUpdateRunner
{
    private readonly ContentModerationDbContext _context;
    private readonly IThresholdService _thresholdService;

    public ThresholdUpdateRunner(
        ContentModerationDbContext context,
        IThresholdService thresholdService)
    {
        _context = context;
        _thresholdService = thresholdService;
    }

    /// <summary>
    /// Executes one tick of the threshold update agent.
    /// Returns true if thresholds were updated, false otherwise.
    /// </summary>
    public async Task<bool> TickAsync(CancellationToken cancellationToken = default)
    {
        // SENSE: Read feedback metrics
        var recentReviews = await _context.Reviews
            .Include(r => r.Content)
            .ThenInclude(c => c.Predictions.OrderByDescending(p => p.CreatedAt).Take(1))
            .Where(r => r.ReviewedAt != null && 
                       r.ReviewedAt >= DateTime.UtcNow.AddDays(-7)) // Last 7 days
            .ToListAsync();

        if (recentReviews.Count < 50) // Need minimum samples
            return false;

        // THINK: Calculate error rates
        var falsePositives = recentReviews.Count(r => 
            r.Content.Predictions.FirstOrDefault()?.Decision == ModerationDecision.Block &&
            r.GoldLabel == ModerationDecision.Allow);

        var falseNegatives = recentReviews.Count(r => 
            r.Content.Predictions.FirstOrDefault()?.Decision == ModerationDecision.Allow &&
            r.GoldLabel == ModerationDecision.Block);

        var falsePositiveRate = (double)falsePositives / recentReviews.Count;
        var falseNegativeRate = (double)falseNegatives / recentReviews.Count;

        var settings = await _thresholdService.GetSettingsAsync();

        // THINK: Decide if thresholds need adjustment
        const double errorThreshold = 0.1; // 10% error rate threshold

        bool needsUpdate = false;
        double newAllowThreshold = settings.AllowThreshold;
        double newReviewThreshold = settings.ReviewThreshold;
        double newBlockThreshold = settings.BlockThreshold;

        if (falsePositiveRate > errorThreshold)
        {
            // Too many false positives (blocking good content) - be more lenient
            newBlockThreshold += 0.05;
            newReviewThreshold += 0.03;
            needsUpdate = true;
        }

        if (falseNegativeRate > errorThreshold)
        {
            // Too many false negatives (missing bad content) - be stricter
            newBlockThreshold -= 0.05;
            newReviewThreshold -= 0.03;
            needsUpdate = true;
        }

        if (!needsUpdate)
            return false;

        // ACT: Update thresholds
        await _thresholdService.UpdateThresholdsAsync(
            newAllowThreshold, 
            newReviewThreshold, 
            newBlockThreshold);

        // LEARN: Log the change for tracking
        // (In a real system, you'd log this to an audit table)

        return true;
    }
}
