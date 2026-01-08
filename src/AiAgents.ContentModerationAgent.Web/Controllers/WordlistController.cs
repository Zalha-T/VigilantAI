using AiAgents.ContentModerationAgent.Application.Services;
using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.ContentModerationAgent.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WordlistController : ControllerBase
{
    private readonly IWordlistService _wordlistService;
    private readonly ContentModerationDbContext _context;

    public WordlistController(IWordlistService wordlistService, ContentModerationDbContext context)
    {
        _wordlistService = wordlistService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            // Ensure BlockedWords table exists
            await EnsureBlockedWordsTableExists();
            
            var words = await _wordlistService.GetAllAsync();
            return Ok(words);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error loading wordlist: {ex.Message}", details = ex.ToString() });
        }
    }

    private async Task EnsureBlockedWordsTableExists()
    {
        try
        {
            // Try to query the table - if it doesn't exist, this will throw
            await _context.BlockedWords.FirstOrDefaultAsync();
        }
        catch
        {
            // Table doesn't exist, create it
            await _context.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BlockedWords')
                BEGIN
                    CREATE TABLE [BlockedWords] (
                        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                        [Word] nvarchar(100) NOT NULL,
                        [Category] nvarchar(50) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        [IsActive] bit NOT NULL DEFAULT 1
                    );
                    
                    CREATE INDEX [IX_BlockedWords_Word] ON [BlockedWords] ([Word]);
                    CREATE INDEX [IX_BlockedWords_Category] ON [BlockedWords] ([Category]);
                    CREATE INDEX [IX_BlockedWords_IsActive] ON [BlockedWords] ([IsActive]);
                END
            ");
        }
    }

    [HttpGet("category/{category}")]
    public async Task<IActionResult> GetByCategory(string category)
    {
        try
        {
            await EnsureBlockedWordsTableExists();
            var words = await _wordlistService.GetByCategoryAsync(category);
            return Ok(words);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error loading wordlist: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> AddWord([FromBody] AddWordRequest request)
    {
        try
        {
            await EnsureBlockedWordsTableExists();
            var word = await _wordlistService.AddAsync(request.Word, request.Category);
            return Ok(word);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error adding word: {ex.Message}" });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateWord(Guid id, [FromBody] UpdateWordRequest request)
    {
        try
        {
            await EnsureBlockedWordsTableExists();
            var word = await _wordlistService.UpdateAsync(id, request.Word, request.Category, request.IsActive);
            if (word == null)
                return NotFound(new { message = "Word not found" });
            return Ok(word);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error updating word: {ex.Message}" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteWord(Guid id)
    {
        try
        {
            await EnsureBlockedWordsTableExists();
            var deleted = await _wordlistService.DeleteAsync(id);
            if (!deleted)
                return NotFound(new { message = "Word not found" });
            return Ok(new { message = "Word deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error deleting word: {ex.Message}" });
        }
    }
}

public class AddWordRequest
{
    public string Word { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "toxic", "hate", "spam", "offensive", "slur"
}

public class UpdateWordRequest
{
    public string? Word { get; set; }
    public string? Category { get; set; }
    public bool? IsActive { get; set; }
}
