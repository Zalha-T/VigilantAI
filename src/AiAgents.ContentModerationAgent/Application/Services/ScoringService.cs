using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Domain.Enums;
using AiAgents.ContentModerationAgent.Infrastructure;
using AiAgents.ContentModerationAgent.ML;
using System.Text.Json;

namespace AiAgents.ContentModerationAgent.Application.Services;

public class ScoringService : IScoringService
{
    private readonly ContentModerationDbContext _context;
    private readonly IContentClassifier _classifier;
    private readonly IContextService _contextService;
    private readonly IThresholdService _thresholdService;

    public ScoringService(
        ContentModerationDbContext context,
        IContentClassifier classifier,
        IContextService contextService,
        IThresholdService thresholdService)
    {
        _context = context;
        _classifier = classifier;
        _contextService = contextService;
        _thresholdService = thresholdService;
    }

    public async Task<Prediction> ScoreAndDecideAsync(Content content, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get ML scores
            var mlScores = await _classifier.PredictAsync(content.Text, cancellationToken);

        // Calculate context
        var context = await _contextService.CalculateContextAsync(content, cancellationToken);
        var contextMultiplier = await _contextService.CalculateContextMultiplierAsync(context, cancellationToken);

        // Calculate final score (weighted average of ML scores, adjusted by context)
        var finalScore = (mlScores.SpamScore * 0.3 +
                         mlScores.ToxicScore * 0.3 +
                         mlScores.HateScore * 0.25 +
                         mlScores.OffensiveScore * 0.15) * contextMultiplier;

        // Get thresholds
        var settings = await _thresholdService.GetSettingsAsync(cancellationToken);

        // Determine decision
        var decision = finalScore < settings.AllowThreshold
            ? ModerationDecision.Allow
            : finalScore > settings.BlockThreshold
                ? ModerationDecision.Block
                : ModerationDecision.Review;

        // Determine confidence
        var confidence = Math.Abs(finalScore - settings.ReviewThreshold) > 0.2
            ? ConfidenceLevel.High
            : Math.Abs(finalScore - settings.ReviewThreshold) > 0.1
                ? ConfidenceLevel.Medium
                : ConfidenceLevel.Low;

        // Create prediction
        var prediction = new Prediction
        {
            Id = Guid.NewGuid(),
            ContentId = content.Id,
            SpamScore = mlScores.SpamScore,
            ToxicScore = mlScores.ToxicScore,
            HateScore = mlScores.HateScore,
            OffensiveScore = mlScores.OffensiveScore,
            FinalScore = finalScore,
            Decision = decision,
            Confidence = confidence,
            ContextFactors = JsonSerializer.Serialize(new
            {
                AuthorReputation = context.AuthorReputation,
                ThreadSentiment = context.ThreadSentiment,
                EngagementLevel = context.EngagementLevel,
                TimeOfDay = context.TimeOfDay,
                ContextMultiplier = contextMultiplier
            }),
            CreatedAt = DateTime.UtcNow
        };

        _context.Predictions.Add(prediction);

        // Update content status based on decision
        var newStatus = decision switch
        {
            ModerationDecision.Allow => ContentStatus.Approved,
            ModerationDecision.Review => ContentStatus.PendingReview,
            ModerationDecision.Block => ContentStatus.Blocked,
            _ => ContentStatus.PendingReview
        };

        content.Status = newStatus;
        content.ProcessedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return prediction;
        }
        catch (Exception ex)
        {
            // Log error and rethrow
            throw new InvalidOperationException($"Error scoring content {content.Id}: {ex.Message}", ex);
        }
    }
}
