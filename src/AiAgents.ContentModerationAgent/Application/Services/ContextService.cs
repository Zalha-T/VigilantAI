using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.ContentModerationAgent.Application.Services;

public class ContextService : IContextService
{
    private readonly ContentModerationDbContext _context;

    public ContextService(ContentModerationDbContext context)
    {
        _context = context;
    }

    public async Task<Context> CalculateContextAsync(Content content, CancellationToken cancellationToken = default)
    {
        // Check if context already exists for this content
        var existingContext = await _context.Contexts
            .FirstOrDefaultAsync(c => c.ContentId == content.Id, cancellationToken);
        
        if (existingContext != null)
            return existingContext;

        var author = await _context.Authors
            .Include(a => a.Contents)
            .FirstOrDefaultAsync(a => a.Id == content.AuthorId, cancellationToken);

        if (author == null)
            throw new InvalidOperationException($"Author {content.AuthorId} not found");

        // Calculate author reputation (0-1 normalized)
        var authorReputation = Math.Min(1.0, (author.ReputationScore / 100.0) + 
            (author.AccountAgeDays > 30 ? 0.1 : 0) - 
            (author.PreviousViolations * 0.1));

        // Calculate thread sentiment (simplified - would use actual sentiment analysis)
        var threadSentiment = 0.0; // Placeholder

        // Calculate engagement level (simplified)
        var engagementLevel = 0.5; // Placeholder

        var now = DateTime.UtcNow;
        var context = new Context
        {
            Id = Guid.NewGuid(),
            ContentId = content.Id,
            AuthorReputation = Math.Max(0, Math.Min(1, authorReputation)),
            ThreadSentiment = Math.Max(-1, Math.Min(1, threadSentiment)),
            EngagementLevel = Math.Max(0, Math.Min(1, engagementLevel)),
            TimeOfDay = now.Hour,
            DayOfWeek = (int)now.DayOfWeek,
            Language = "en", // Placeholder - would use language detection
            ContentLength = content.Text.Length
        };

        _context.Contexts.Add(context);
        await _context.SaveChangesAsync(cancellationToken);

        return context;
    }

    public Task<double> CalculateContextMultiplierAsync(Context context, CancellationToken cancellationToken = default)
    {
        // Context multiplier affects final score
        // Higher reputation = lower multiplier (more lenient)
        // Lower reputation = higher multiplier (stricter)
        // Night time = higher multiplier (stricter, less moderators)
        
        var multiplier = 1.0;

        // Author reputation effect
        if (context.AuthorReputation > 0.8)
            multiplier *= 0.9; // More lenient for trusted users
        else if (context.AuthorReputation < 0.3)
            multiplier *= 1.2; // Stricter for new/low-reputation users

        // Time of day effect (night = stricter)
        if (context.TimeOfDay >= 22 || context.TimeOfDay <= 6)
            multiplier *= 1.1; // Stricter at night

        // Engagement effect
        if (context.EngagementLevel > 0.8)
            multiplier *= 1.15; // Stricter for high-engagement content

        return Task.FromResult(multiplier);
    }
}
