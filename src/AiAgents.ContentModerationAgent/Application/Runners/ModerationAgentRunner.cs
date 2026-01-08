using AiAgents.ContentModerationAgent.Application.DTOs;
using AiAgents.ContentModerationAgent.Application.Services;
using AiAgents.ContentModerationAgent.Domain.Enums;

namespace AiAgents.ContentModerationAgent.Application.Runners;

/// <summary>
/// Main moderation agent runner implementing Sense → Think → Act cycle.
/// Processes one content item per tick.
/// </summary>
public class ModerationAgentRunner
{
    private readonly IQueueService _queueService;
    private readonly IScoringService _scoringService;

    public ModerationAgentRunner(
        IQueueService queueService,
        IScoringService scoringService)
    {
        _queueService = queueService;
        _scoringService = scoringService;
    }

    /// <summary>
    /// Executes one tick of the moderation agent.
    /// Returns null if no work is available.
    /// </summary>
    public async Task<ModerationTickResult?> TickAsync(CancellationToken cancellationToken = default)
    {
        // SENSE: Get next content from queue
        var content = await _queueService.DequeueNextAsync(cancellationToken);
        if (content == null)
            return null; // No work available

        try
        {
            // THINK + ACT: Score and decide (this combines Think and Act)
            var prediction = await _scoringService.ScoreAndDecideAsync(content, cancellationToken);

            // Create result DTO
            var result = new ModerationTickResult
            {
                ContentId = content.Id,
                Decision = prediction.Decision,
                Confidence = prediction.Confidence,
                FinalScore = prediction.FinalScore,
                NewStatus = content.Status,
                ContextFactors = prediction.ContextFactors
            };

            return result;
        }
        catch (Exception ex)
        {
            // Log error and rethrow - BackgroundService will handle it
            throw new InvalidOperationException($"Error processing content {content.Id}: {ex.Message}", ex);
        }
    }
}
