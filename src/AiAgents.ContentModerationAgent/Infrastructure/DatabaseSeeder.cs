using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.ContentModerationAgent.Infrastructure;

public class DatabaseSeeder
{
    private readonly ContentModerationDbContext _context;

    public DatabaseSeeder(ContentModerationDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        // Check if authors are already seeded
        var authorsSeeded = await _context.Authors.AnyAsync();

        // Create authors only if not already seeded
        if (!authorsSeeded)
        {
            var authors = new List<Author>
        {
            new Author
            {
                Id = Guid.NewGuid(),
                Username = "trusted_user",
                ReputationScore = 90,
                AccountAgeDays = 365,
                PreviousViolations = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-365)
            },
            new Author
            {
                Id = Guid.NewGuid(),
                Username = "new_user",
                ReputationScore = 50,
                AccountAgeDays = 5,
                PreviousViolations = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new Author
            {
                Id = Guid.NewGuid(),
                Username = "problematic_user",
                ReputationScore = 20,
                AccountAgeDays = 100,
                PreviousViolations = 3,
                CreatedAt = DateTime.UtcNow.AddDays(-100)
            }
        };

            _context.Authors.AddRange(authors);
            await _context.SaveChangesAsync();
        }

        // Create sample content only if not already seeded
        if (!authorsSeeded)
        {
            var authors = await _context.Authors.ToListAsync();
            var contents = new List<Content>
        {
            // Clean content
            new Content
            {
                Id = Guid.NewGuid(),
                Type = ContentType.Comment,
                Text = "This is a great article! Thanks for sharing.",
                AuthorId = authors[0].Id,
                Status = ContentStatus.Queued,
                CreatedAt = DateTime.UtcNow
            },
            new Content
            {
                Id = Guid.NewGuid(),
                Type = ContentType.Post,
                Text = "I really enjoyed reading this. Very informative and well written.",
                AuthorId = authors[0].Id,
                Status = ContentStatus.Queued,
                CreatedAt = DateTime.UtcNow
            },
            // Potentially problematic content
            new Content
            {
                Id = Guid.NewGuid(),
                Type = ContentType.Comment,
                Text = "This is spam spam spam buy now click here",
                AuthorId = authors[1].Id,
                Status = ContentStatus.Queued,
                CreatedAt = DateTime.UtcNow
            },
            new Content
            {
                Id = Guid.NewGuid(),
                Type = ContentType.Message,
                Text = "You are an idiot and I hate you",
                AuthorId = authors[2].Id,
                Status = ContentStatus.Queued,
                CreatedAt = DateTime.UtcNow
            },
            // Borderline content
            new Content
            {
                Id = Guid.NewGuid(),
                Type = ContentType.Comment,
                Text = "I disagree with your opinion but respect your right to have it.",
                AuthorId = authors[1].Id,
                Status = ContentStatus.Queued,
                CreatedAt = DateTime.UtcNow
            },
            new Content
            {
                Id = Guid.NewGuid(),
                Type = ContentType.Post,
                Text = "Check out this amazing deal! Limited time offer!",
                AuthorId = authors[1].Id,
                Status = ContentStatus.Queued,
                CreatedAt = DateTime.UtcNow
            }
        };

            _context.Contents.AddRange(contents);
            await _context.SaveChangesAsync();
        }

        // Create or update default system settings
        var existingSettings = await _context.SystemSettings.FirstOrDefaultAsync();
        if (existingSettings == null)
        {
            var settings = new SystemSettings
            {
                Id = Guid.NewGuid(),
                AllowThreshold = 0.3,
                ReviewThreshold = 0.5,
                BlockThreshold = 0.7,
                RetrainThreshold = 10, // Changed from 100 to 10 for faster learning
                NewGoldSinceLastTrain = 0,
                RetrainingEnabled = true
            };
            _context.SystemSettings.Add(settings);
            await _context.SaveChangesAsync();
        }
        else
        {
            // Update existing settings to use 10 instead of 100
            if (existingSettings.RetrainThreshold > 10)
            {
                existingSettings.RetrainThreshold = 10;
                await _context.SaveChangesAsync();
            }
        }

        // Seed initial blocked words (always check if table is empty, regardless of other seed status)
        // These are common words that should be blocked - you can add more through the UI
        try
        {
            // Check if BlockedWords table exists and is empty
            var wordCount = await _context.BlockedWords.CountAsync();
            if (wordCount == 0)
            {
                var initialWords = new List<BlockedWord>
                {
                    // Common toxic words (examples - add your own through UI)
                    new BlockedWord { Id = Guid.NewGuid(), Word = "fuck", Category = "toxic", CreatedAt = DateTime.UtcNow, IsActive = true },
                    new BlockedWord { Id = Guid.NewGuid(), Word = "fucking", Category = "toxic", CreatedAt = DateTime.UtcNow, IsActive = true },
                    new BlockedWord { Id = Guid.NewGuid(), Word = "bitch", Category = "toxic", CreatedAt = DateTime.UtcNow, IsActive = true },
                    new BlockedWord { Id = Guid.NewGuid(), Word = "idiot", Category = "toxic", CreatedAt = DateTime.UtcNow, IsActive = true },
                    new BlockedWord { Id = Guid.NewGuid(), Word = "stupid", Category = "toxic", CreatedAt = DateTime.UtcNow, IsActive = true },
                    new BlockedWord { Id = Guid.NewGuid(), Word = "asshole", Category = "toxic", CreatedAt = DateTime.UtcNow, IsActive = true },
                    
                    // Common hate words
                    new BlockedWord { Id = Guid.NewGuid(), Word = "hate", Category = "hate", CreatedAt = DateTime.UtcNow, IsActive = true },
                    new BlockedWord { Id = Guid.NewGuid(), Word = "i hate", Category = "hate", CreatedAt = DateTime.UtcNow, IsActive = true },
                    new BlockedWord { Id = Guid.NewGuid(), Word = "i fucking hate", Category = "hate", CreatedAt = DateTime.UtcNow, IsActive = true },
                    
                    // Common spam phrases
                    new BlockedWord { Id = Guid.NewGuid(), Word = "buy now", Category = "spam", CreatedAt = DateTime.UtcNow, IsActive = true },
                    new BlockedWord { Id = Guid.NewGuid(), Word = "click here", Category = "spam", CreatedAt = DateTime.UtcNow, IsActive = true },
                    new BlockedWord { Id = Guid.NewGuid(), Word = "limited time", Category = "spam", CreatedAt = DateTime.UtcNow, IsActive = true },
                    new BlockedWord { Id = Guid.NewGuid(), Word = "act now", Category = "spam", CreatedAt = DateTime.UtcNow, IsActive = true },
                    
                    // Common offensive words
                    new BlockedWord { Id = Guid.NewGuid(), Word = "damn", Category = "offensive", CreatedAt = DateTime.UtcNow, IsActive = true },
                    new BlockedWord { Id = Guid.NewGuid(), Word = "shit", Category = "offensive", CreatedAt = DateTime.UtcNow, IsActive = true },
                    new BlockedWord { Id = Guid.NewGuid(), Word = "hell", Category = "offensive", CreatedAt = DateTime.UtcNow, IsActive = true },
                    
                    // Note: Add actual slurs through the UI - we don't seed them here
                    // You should add slurs manually as they are sensitive content
                };

                _context.BlockedWords.AddRange(initialWords);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception)
        {
            // If BlockedWords table doesn't exist yet, it will be created by EnsureCreated
            // The WordlistController will handle table creation on first access
            // This is expected if the database was created before BlockedWords was added
        }
    }
}
