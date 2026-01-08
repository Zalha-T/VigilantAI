using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Domain.Enums;
using AiAgents.ContentModerationAgent.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.ContentModerationAgent.Application.Services;

public class QueueService : IQueueService
{
    private readonly ContentModerationDbContext _context;

    public QueueService(ContentModerationDbContext context)
    {
        _context = context;
    }

    public async Task<Content?> DequeueNextAsync(CancellationToken cancellationToken = default)
    {
        // Get next queued content and mark as processing atomically
        var content = await _context.Contents
            .Where(c => c.Status == ContentStatus.Queued)
            .OrderBy(c => c.CreatedAt)
            .Include(c => c.Author)
            .FirstOrDefaultAsync(cancellationToken);

        if (content != null)
        {
            content.Status = ContentStatus.Processing;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return content;
    }

    public async Task EnqueueAsync(Content content, CancellationToken cancellationToken = default)
    {
        content.Status = ContentStatus.Queued;
        _context.Contents.Add(content);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(Guid contentId, ContentStatus status, CancellationToken cancellationToken = default)
    {
        var content = await _context.Contents.FindAsync(new object[] { contentId }, cancellationToken);
        if (content != null)
        {
            content.Status = status;
            content.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
