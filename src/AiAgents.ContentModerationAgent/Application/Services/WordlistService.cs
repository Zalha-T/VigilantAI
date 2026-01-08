using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.ContentModerationAgent.Application.Services;

public class WordlistService : IWordlistService
{
    private readonly ContentModerationDbContext _context;

    public WordlistService(ContentModerationDbContext context)
    {
        _context = context;
    }

    public async Task<List<BlockedWord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.BlockedWords
            .OrderBy(w => w.Category)
            .ThenBy(w => w.Word)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<BlockedWord>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        return await _context.BlockedWords
            .Where(w => w.Category == category)
            .OrderBy(w => w.Word)
            .ToListAsync(cancellationToken);
    }

    public async Task<BlockedWord> AddAsync(string word, string category, CancellationToken cancellationToken = default)
    {
        // Check if word already exists
        var existing = await _context.BlockedWords
            .FirstOrDefaultAsync(w => w.Word.ToLower() == word.ToLower() && w.Category == category, cancellationToken);

        if (existing != null)
        {
            // Reactivate if it was deleted
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var blockedWord = new BlockedWord
        {
            Id = Guid.NewGuid(),
            Word = word.ToLowerInvariant(),
            Category = category.ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.BlockedWords.Add(blockedWord);
        await _context.SaveChangesAsync(cancellationToken);

        return blockedWord;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var word = await _context.BlockedWords.FindAsync(new object[] { id }, cancellationToken);
        if (word == null)
            return false;

        _context.BlockedWords.Remove(word);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<BlockedWord?> UpdateAsync(Guid id, string? word = null, string? category = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var blockedWord = await _context.BlockedWords.FindAsync(new object[] { id }, cancellationToken);
        if (blockedWord == null)
            return null;

        if (word != null)
            blockedWord.Word = word.ToLowerInvariant();
        if (category != null)
            blockedWord.Category = category.ToLowerInvariant();
        if (isActive.HasValue)
            blockedWord.IsActive = isActive.Value;
        
        blockedWord.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return blockedWord;
    }

    public async Task<List<string>> GetActiveWordsByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        return await _context.BlockedWords
            .Where(w => w.Category == category.ToLowerInvariant() && w.IsActive)
            .Select(w => w.Word)
            .ToListAsync(cancellationToken);
    }
}
