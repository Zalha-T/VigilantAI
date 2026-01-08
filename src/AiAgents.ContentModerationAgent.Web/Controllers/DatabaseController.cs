using AiAgents.ContentModerationAgent.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.ContentModerationAgent.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatabaseController : ControllerBase
{
    private readonly ContentModerationDbContext _context;

    public DatabaseController(ContentModerationDbContext context)
    {
        _context = context;
    }

    [HttpPost("ensure-created")]
    public async Task<IActionResult> EnsureCreated()
    {
        try
        {
            await _context.Database.EnsureCreatedAsync();
            return Ok(new { message = "Database tables ensured successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error ensuring database: {ex.Message}", details = ex.ToString() });
        }
    }

    [HttpPost("seed-wordlist")]
    public async Task<IActionResult> SeedWordlist()
    {
        try
        {
            // Ensure BlockedWords table exists
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

            // Check if wordlist is empty
            var wordCount = await _context.BlockedWords.CountAsync();
            if (wordCount > 0)
            {
                return Ok(new { message = $"Wordlist already has {wordCount} words. Use /api/wordlist to view them." });
            }

            // Seed wordlist
            var seeder = new DatabaseSeeder(_context);
            await seeder.SeedAsync();

            // Count again
            wordCount = await _context.BlockedWords.CountAsync();
            return Ok(new { message = $"Wordlist seeded successfully. Now has {wordCount} words." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error seeding wordlist: {ex.Message}", details = ex.ToString() });
        }
    }
}
