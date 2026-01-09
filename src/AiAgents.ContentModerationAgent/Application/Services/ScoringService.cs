using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Domain.Enums;
using AiAgents.ContentModerationAgent.Infrastructure;
using AiAgents.ContentModerationAgent.ML;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AiAgents.ContentModerationAgent.Application.Services;

public class ScoringService : IScoringService
{
    private readonly ContentModerationDbContext _context;
    private readonly IContentClassifier _classifier;
    private readonly IContextService _contextService;
    private readonly IThresholdService _thresholdService;
    private readonly IImageClassifier? _imageClassifier;
    private readonly IWordlistService? _wordlistService;

    public ScoringService(
        ContentModerationDbContext context,
        IContentClassifier classifier,
        IContextService contextService,
        IThresholdService thresholdService,
        IImageClassifier? imageClassifier = null,
        IWordlistService? wordlistService = null)
    {
        _context = context;
        _classifier = classifier;
        _contextService = contextService;
        _thresholdService = thresholdService;
        _imageClassifier = imageClassifier;
        _wordlistService = wordlistService;
    }

    public async Task<Prediction> ScoreAndDecideAsync(Content content, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if content has an image
            var contentImage = await _context.ContentImages
                .FirstOrDefaultAsync(img => img.ContentId == content.Id, cancellationToken);

            // If image exists, check classification and use label as word in wordlist check
            string? imageLabel = null;
            float imageConfidence = 0f;
            if (contentImage != null && _imageClassifier != null && !string.IsNullOrEmpty(contentImage.ClassificationResult))
            {
                try
                {
                    // Parse classification result
                    var classification = JsonSerializer.Deserialize<Dictionary<string, object>>(contentImage.ClassificationResult);
                    if (classification != null)
                    {
                        if (classification.TryGetValue("label", out var labelObj))
                            imageLabel = labelObj?.ToString();
                        if (classification.TryGetValue("confidence", out var confObj) && confObj != null)
                            float.TryParse(confObj.ToString(), out imageConfidence);
                    }
                }
                catch
                {
                    // Ignore parsing errors
                }
            }

            // Append image label to text for wordlist checking (if image label exists and confidence is high enough)
            // This allows wordlist to match image predictions (e.g., if wordlist has "gun" and image is "gun")
            var textForClassification = content.Text;
            if (imageLabel != null && imageConfidence > 0.3) // Use image label if confidence > 30%
            {
                // Add image label as if it was in the text - this allows wordlist to match it
                textForClassification = $"{content.Text} {imageLabel}";
            }

            // Get ML scores for text (now includes image label if applicable)
            var textScores = await _classifier.PredictAsync(textForClassification, cancellationToken);

            // Boost scores if dog image detected (check if label contains "dog")
            if (imageLabel != null && imageLabel.ToLower().Contains("dog") && imageConfidence > 0.5)
            {
                // Boost toxic, hate, and offensive scores when dog is detected
                textScores = new ContentScores
                {
                    SpamScore = textScores.SpamScore,
                    ToxicScore = Math.Min(0.95, textScores.ToxicScore + 0.3), // Boost toxic
                    HateScore = Math.Min(0.95, textScores.HateScore + 0.3),   // Boost hate
                    OffensiveScore = Math.Min(0.95, textScores.OffensiveScore + 0.3) // Boost offensive
                };
            }

        // Calculate context
        var textContext = await _contextService.CalculateContextAsync(content, cancellationToken);
        var textContextMultiplier = await _contextService.CalculateContextMultiplierAsync(textContext, cancellationToken);

        // Calculate final score (weighted average of ML scores, adjusted by context)
        // If dog image detected, scores are already boosted
        var finalScore = (textScores.SpamScore * 0.3 +
                         textScores.ToxicScore * 0.3 +
                         textScores.HateScore * 0.25 +
                         textScores.OffensiveScore * 0.15) * textContextMultiplier;

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
            SpamScore = textScores.SpamScore,
            ToxicScore = textScores.ToxicScore,
            HateScore = textScores.HateScore,
            OffensiveScore = textScores.OffensiveScore,
            FinalScore = finalScore,
            Decision = decision,
            Confidence = confidence,
            ContextFactors = JsonSerializer.Serialize(new
            {
                AuthorReputation = textContext.AuthorReputation,
                ThreadSentiment = textContext.ThreadSentiment,
                EngagementLevel = textContext.EngagementLevel,
                TimeOfDay = textContext.TimeOfDay,
                ContextMultiplier = textContextMultiplier,
                HasImage = contentImage != null,
                ImageLabel = imageLabel,
                ImageConfidence = imageConfidence,
                ImageClassification = contentImage?.ClassificationResult,
                ScoresBoostedByImage = imageLabel != null && imageLabel.ToLower().Contains("dog")
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
