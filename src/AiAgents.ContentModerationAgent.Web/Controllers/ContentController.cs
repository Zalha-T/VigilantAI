using AiAgents.ContentModerationAgent.Application.Services;
using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Domain.Enums;
using AiAgents.ContentModerationAgent.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.ContentModerationAgent.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContentController : ControllerBase
{
    private readonly ContentModerationDbContext _context;
    private readonly IQueueService _queueService;

    public ContentController(
        ContentModerationDbContext context,
        IQueueService queueService)
    {
        _context = context;
        _queueService = queueService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateContent([FromBody] CreateContentRequest request)
    {
        // Find or create author
        var author = await _context.Authors
            .FirstOrDefaultAsync(a => a.Username == request.AuthorUsername);

        if (author == null)
        {
            author = new Author
            {
                Id = Guid.NewGuid(),
                Username = request.AuthorUsername,
                ReputationScore = 50, // Default
                AccountAgeDays = 0,
                PreviousViolations = 0,
                CreatedAt = DateTime.UtcNow
            };
            _context.Authors.Add(author);
            await _context.SaveChangesAsync();
        }

        // Create content
        var content = new Content
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            Text = request.Text,
            AuthorId = author.Id,
            ThreadId = request.ThreadId,
            Status = ContentStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };

        await _queueService.EnqueueAsync(content);

        return Ok(new { ContentId = content.Id, Status = content.Status });
    }

    [HttpGet("pending-review")]
    public async Task<IActionResult> GetPendingReview()
    {
        var contents = await _context.Contents
            .Where(c => c.Status == ContentStatus.PendingReview)
            .Include(c => c.Author)
            .Include(c => c.Predictions.OrderByDescending(p => p.CreatedAt).Take(1))
            .OrderBy(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Text,
                c.Type,
                c.Status,
                Author = new
                {
                    Username = c.Author.Username,
                    ReputationScore = c.Author.ReputationScore
                },
                Prediction = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault() != null ? new
                {
                    Decision = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.Decision,
                    FinalScore = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.FinalScore,
                    SpamScore = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.SpamScore,
                    ToxicScore = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.ToxicScore,
                    HateScore = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.HateScore,
                    OffensiveScore = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.OffensiveScore,
                    Confidence = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.Confidence
                } : null,
                c.CreatedAt
            })
            .ToListAsync();

        return Ok(contents);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetContentById(Guid id)
    {
        var content = await _context.Contents
            .Include(c => c.Author)
            .Include(c => c.Predictions.OrderByDescending(p => p.CreatedAt))
            .Include(c => c.Reviews.OrderByDescending(r => r.CreatedAt))
            .Include(c => c.Context)
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Text,
                c.Type,
                c.Status,
                c.CreatedAt,
                c.ProcessedAt,
                Author = new
                {
                    c.Author.Username,
                    c.Author.ReputationScore,
                    c.Author.AccountAgeDays,
                    c.Author.PreviousViolations
                },
                Predictions = c.Predictions.Select(p => new
                {
                    p.Id,
                    p.Decision,
                    p.FinalScore,
                    p.SpamScore,
                    p.ToxicScore,
                    p.HateScore,
                    p.OffensiveScore,
                    p.Confidence,
                    p.ContextFactors,
                    p.CreatedAt
                }),
                Reviews = c.Reviews.Select(r => new
                {
                    r.Id,
                    r.GoldLabel,
                    r.CorrectDecision,
                    r.Feedback,
                    r.ModeratorId,
                    r.CreatedAt,
                    r.ReviewedAt
                }),
                Context = c.Context != null ? new
                {
                    c.Context.AuthorReputation,
                    c.Context.ThreadSentiment,
                    c.Context.EngagementLevel,
                    c.Context.TimeOfDay,
                    c.Context.DayOfWeek
                } : null
            })
            .FirstOrDefaultAsync();

        if (content == null)
            return NotFound(new { message = "Content not found" });

        return Ok(content);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllContent(
        [FromQuery] ContentStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _context.Contents
            .Include(c => c.Author)
            .Include(c => c.Predictions.OrderByDescending(p => p.CreatedAt).Take(1))
            .Include(c => c.Reviews.OrderByDescending(r => r.CreatedAt).Take(1))
            .AsQueryable();

        // Filter by status if provided
        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        var totalCount = await query.CountAsync();

        var contents = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Text,
                c.Type,
                c.Status,
                c.CreatedAt,
                c.ProcessedAt,
                Author = new
                {
                    c.Author.Username,
                    c.Author.ReputationScore
                },
                // Latest prediction
                Prediction = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault() != null ? new
                {
                    Decision = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.Decision,
                    FinalScore = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.FinalScore,
                    SpamScore = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.SpamScore,
                    ToxicScore = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.ToxicScore,
                    HateScore = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.HateScore,
                    OffensiveScore = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.OffensiveScore,
                    Confidence = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.Confidence
                } : null,
                // Latest review (gold label)
                Review = c.Reviews.OrderByDescending(r => r.CreatedAt).FirstOrDefault() != null ? new
                {
                    GoldLabel = c.Reviews.OrderByDescending(r => r.CreatedAt).FirstOrDefault()!.GoldLabel,
                    CorrectDecision = c.Reviews.OrderByDescending(r => r.CreatedAt).FirstOrDefault()!.CorrectDecision,
                    Feedback = c.Reviews.OrderByDescending(r => r.CreatedAt).FirstOrDefault()!.Feedback
                } : null,
                // Labels based on scores and decisions
                Labels = new
                {
                    IsSpam = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault() != null && 
                             c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.SpamScore > 0.5,
                    IsToxic = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault() != null && 
                              c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.ToxicScore > 0.5,
                    IsHate = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault() != null && 
                             c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.HateScore > 0.5,
                    IsOffensive = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault() != null && 
                                  c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.OffensiveScore > 0.5,
                    IsProblematic = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault() != null && 
                                    c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.FinalScore > 0.5,
                    AgentDecision = c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault() != null ? 
                                    c.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault()!.Decision : (ModerationDecision?)null,
                    HumanLabel = c.Reviews.OrderByDescending(r => r.CreatedAt).FirstOrDefault() != null ? 
                                 c.Reviews.OrderByDescending(r => r.CreatedAt).FirstOrDefault()!.GoldLabel : (ModerationDecision?)null
                }
            })
            .ToListAsync();

            return Ok(new
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Data = contents
            });
    }

    [HttpPost("reset-stuck")]
    public async Task<IActionResult> ResetStuckContent()
    {
        // Reset content that's been stuck in Processing status for more than 5 minutes
        var stuckThreshold = DateTime.UtcNow.AddMinutes(-5);
        
        var stuckContents = await _context.Contents
            .Where(c => c.Status == ContentStatus.Processing && 
                       c.CreatedAt < stuckThreshold)
            .ToListAsync();

        foreach (var content in stuckContents)
        {
            content.Status = ContentStatus.Queued;
            content.ProcessedAt = null;
        }

        await _context.SaveChangesAsync();

        return Ok(new 
        { 
            ResetCount = stuckContents.Count,
            Message = $"Reset {stuckContents.Count} stuck content(s) back to Queued status"
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteContent(Guid id)
    {
        var content = await _context.Contents
            .Include(c => c.Predictions)
            .Include(c => c.Reviews)
            .Include(c => c.Context)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (content == null)
            return NotFound(new { message = "Content not found" });

        // Delete related entities (cascade delete should handle this, but being explicit)
        _context.Predictions.RemoveRange(content.Predictions);
        _context.Reviews.RemoveRange(content.Reviews);
        if (content.Context != null)
            _context.Contexts.Remove(content.Context);

        _context.Contents.Remove(content);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Content deleted successfully" });
    }

    [HttpPost("{id}/send-to-review")]
    public async Task<IActionResult> SendToReview(Guid id)
    {
        var content = await _context.Contents.FindAsync(id);

        if (content == null)
            return NotFound(new { message = "Content not found" });

        // Update status to PendingReview
        content.Status = ContentStatus.PendingReview;
        content.ProcessedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Content sent to review queue", status = content.Status });
    }
}

public class CreateContentRequest
{
    public ContentType Type { get; set; }
    public string Text { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
    public Guid? ThreadId { get; set; }
}
