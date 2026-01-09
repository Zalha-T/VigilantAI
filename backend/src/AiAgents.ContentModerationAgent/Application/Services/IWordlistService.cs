using AiAgents.ContentModerationAgent.Domain.Entities;

namespace AiAgents.ContentModerationAgent.Application.Services;

public interface IWordlistService
{
    Task<List<BlockedWord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<BlockedWord>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
    Task<BlockedWord> AddAsync(string word, string category, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BlockedWord?> UpdateAsync(Guid id, string? word = null, string? category = null, bool? isActive = null, CancellationToken cancellationToken = default);
    Task<List<string>> GetActiveWordsByCategoryAsync(string category, CancellationToken cancellationToken = default);
}
