using AiAgents.ContentModerationAgent.Application.Services;
using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Domain.Enums;
using AiAgents.ContentModerationAgent.Infrastructure;
using AiAgents.ContentModerationAgent.ML;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AiAgents.ContentModerationAgent.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContentController : ControllerBase
{
    private readonly ContentModerationDbContext _context;
    private readonly IQueueService _queueService;
    private readonly IImageStorageService _imageStorageService;
    private readonly IImageClassifier _imageClassifier;
    private readonly ILogger<ContentController> _logger;

    public ContentController(
        ContentModerationDbContext context,
        IQueueService queueService,
        IImageStorageService imageStorageService,
        IImageClassifier imageClassifier,
        ILogger<ContentController> logger)
    {
        _context = context;
        _queueService = queueService;
        _imageStorageService = imageStorageService;
        _imageClassifier = imageClassifier;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateContent()
    {
        CreateContentRequest request;
        
        // Check if request is form data (multipart/form-data) or JSON
        if (Request.HasFormContentType)
        {
            // Bind from form data (supports file uploads)
            request = new CreateContentRequest
            {
                Type = Enum.Parse<ContentType>(Request.Form["type"].ToString()),
                Text = Request.Form["text"].ToString(),
                AuthorUsername = Request.Form["authorUsername"].ToString(),
                ThreadId = !string.IsNullOrEmpty(Request.Form["threadId"]) ? Guid.Parse(Request.Form["threadId"].ToString()) : null,
                Image = Request.Form.Files.GetFile("image")
            };
        }
        else
        {
            // Bind from JSON body - enable buffering to allow reading the body
            Request.EnableBuffering();
            Request.Body.Position = 0;
            
            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            Request.Body.Position = 0;
            
            request = System.Text.Json.JsonSerializer.Deserialize<CreateContentRequest>(
                body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (request == null)
            {
                return BadRequest(new { message = "Invalid request body" });
            }
        }

        _logger.LogInformation("========== CreateContent STARTED ==========");
        _logger.LogInformation("Content Type: {Type}, Has Image: {HasImage}", request.Type, request.Image != null && request.Image.Length > 0);
        
        // Ensure ContentImages table exists
        await EnsureContentImagesTableExists();

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

        _context.Contents.Add(content);
        await _context.SaveChangesAsync();

        // Handle image upload if provided
        ContentImage? contentImage = null;
        if (request.Image != null && request.Image.Length > 0)
        {
            _logger.LogInformation("Image provided: {FileName}, Size: {Size} bytes", request.Image.FileName, request.Image.Length);
            
            // Validate: Images are only allowed for Post content type
            if (request.Type != ContentType.Post)
            {
                _logger.LogWarning("Image rejected: Content type is {Type}, must be Post", request.Type);
                return BadRequest(new { message = "Images are only allowed for Post content type." });
            }
            
            _logger.LogInformation("Image validation passed. Proceeding with upload...");

            // Validate image format
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(request.Image.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Invalid image format. Allowed: jpg, jpeg, png, gif, webp" });
            }

            // Read image bytes
            byte[] imageBytes;
            try
            {
                using var ms = new MemoryStream();
                await request.Image.CopyToAsync(ms);
                imageBytes = ms.ToArray();

                // Validate file size (max 5MB)
                if (imageBytes.Length > 5 * 1024 * 1024)
                {
                    return BadRequest(new { message = "Image file size exceeds 5MB limit." });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error reading image: {ex.Message}" });
            }

            // Compress and save image
            string filePath;
            try
            {
                filePath = await _imageStorageService.SaveImageAsync(
                    imageBytes, 
                    request.Image.FileName, 
                    content.Id);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error saving image: {ex.Message}" });
            }

            // Classify image (with error handling)
            ImageClassificationResult classification;
            try
            {
                _logger.LogInformation("Starting image classification. Image size: {Size} bytes", imageBytes.Length);
                classification = await _imageClassifier.ClassifyAsync(imageBytes);
                _logger.LogInformation("Image classification completed. Label: {Label}, Confidence: {Confidence}", classification.Label, classification.Confidence);
            }
            catch (Exception ex)
            {
                // If classification fails, use fallback
                _logger.LogError(ex, "Image classification error: {Message}", ex.Message);
                classification = new ImageClassificationResult
                {
                    Label = "other",
                    Confidence = 0.0f,
                    IsBlocked = false,
                    Details = $"Classification error: {ex.Message}"
                };
            }

            // Create ContentImage entity
            contentImage = new ContentImage
            {
                Id = Guid.NewGuid(),
                ContentId = content.Id,
                FileName = Path.GetFileName(filePath),
                OriginalFileName = request.Image.FileName,
                FilePath = filePath,
                MimeType = request.Image.ContentType,
                FileSize = imageBytes.Length,
                ClassificationResult = System.Text.Json.JsonSerializer.Serialize(new
                {
                    label = classification.Label,
                    confidence = classification.Confidence,
                    isBlocked = classification.IsBlocked,
                    details = classification.Details,
                    topPredictions = classification.TopPredictions // Include for future use, but not displayed in UI
                }),
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _context.ContentImages.Add(contentImage);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Log error but don't fail the request - image is already saved
                Console.WriteLine($"Error saving ContentImage to database: {ex.Message}");
                // Continue - image is saved, just DB entry might be missing
            }
        }

        // Enqueue content for processing (don't fail if this fails)
        try
        {
            await _queueService.EnqueueAsync(content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error enqueueing content: {ex.Message}");
            // Continue - content is created, just not queued yet
        }

        return Ok(new 
        { 
            ContentId = content.Id, 
            Status = content.Status,
            ImageId = contentImage?.Id,
            ImageClassification = contentImage != null ? System.Text.Json.JsonSerializer.Deserialize<object>(contentImage.ClassificationResult!) : null
        });
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
            .FirstOrDefaultAsync();

        if (content == null)
            return NotFound(new { message = "Content not found" });

        // Get image if exists
        var contentImage = await _context.ContentImages
            .FirstOrDefaultAsync(img => img.ContentId == id);

        // Get latest prediction
        var latestPrediction = content.Predictions.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
        
        // Get latest review
        var latestReview = content.Reviews.OrderByDescending(r => r.CreatedAt).FirstOrDefault();

        var result = new
        {
            content.Id,
            content.Text,
            content.Type,
            content.Status,
            content.CreatedAt,
            content.ProcessedAt,
            Author = new
            {
                content.Author.Username,
                content.Author.ReputationScore,
                content.Author.AccountAgeDays,
                content.Author.PreviousViolations
            },
            // Return latest prediction as single object (not array) to match frontend interface
            Prediction = latestPrediction != null ? new
            {
                latestPrediction.Decision,
                latestPrediction.FinalScore,
                latestPrediction.SpamScore,
                latestPrediction.ToxicScore,
                latestPrediction.HateScore,
                latestPrediction.OffensiveScore,
                latestPrediction.Confidence
            } : null,
            // Also return as array for backwards compatibility if needed
            Predictions = content.Predictions.Select(p => new
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
            // Return latest review as single object to match frontend interface
            Review = latestReview != null ? new
            {
                latestReview.GoldLabel,
                latestReview.CorrectDecision,
                latestReview.Feedback
            } : null,
            // Also return as array for backwards compatibility
            Reviews = content.Reviews.Select(r => new
            {
                r.Id,
                r.GoldLabel,
                r.CorrectDecision,
                r.Feedback,
                r.ModeratorId,
                r.CreatedAt,
                r.ReviewedAt
            }),
            Context = content.Context != null ? new
            {
                content.Context.AuthorReputation,
                content.Context.ThreadSentiment,
                content.Context.EngagementLevel,
                content.Context.TimeOfDay,
                content.Context.DayOfWeek
            } : null,
            Image = contentImage != null ? new
            {
                contentImage.Id,
                contentImage.FileName,
                contentImage.OriginalFileName,
                Url = $"/api/content/{id}/images/{contentImage.Id}",
                Classification = !string.IsNullOrEmpty(contentImage.ClassificationResult) 
                    ? JsonSerializer.Deserialize<object>(contentImage.ClassificationResult) 
                    : null
            } : null,
            // Add Labels object to match frontend interface
            Labels = new
            {
                IsSpam = latestPrediction != null && latestPrediction.SpamScore > 0.5,
                IsToxic = latestPrediction != null && latestPrediction.ToxicScore > 0.5,
                IsHate = latestPrediction != null && latestPrediction.HateScore > 0.5,
                IsOffensive = latestPrediction != null && latestPrediction.OffensiveScore > 0.5,
                IsProblematic = latestPrediction != null && latestPrediction.FinalScore > 0.5,
                AgentDecision = latestPrediction != null ? (ModerationDecision?)latestPrediction.Decision : null,
                HumanLabel = latestReview != null ? (ModerationDecision?)latestReview.GoldLabel : null
            }
        };

        return Ok(result);
    }

    [HttpGet("{contentId}/images/{imageId}")]
    public async Task<IActionResult> GetImage(Guid contentId, Guid imageId)
    {
        var image = await _context.ContentImages
            .FirstOrDefaultAsync(img => img.Id == imageId && img.ContentId == contentId);

        if (image == null)
            return NotFound(new { message = "Image not found" });

        var imageBytes = await _imageStorageService.GetImageAsync(image.FilePath);
        if (imageBytes == null)
            return NotFound(new { message = "Image file not found" });

        return File(imageBytes, image.MimeType, image.OriginalFileName);
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
            .Include(c => c.Image) // Include image
            .FirstOrDefaultAsync(c => c.Id == id);

        if (content == null)
            return NotFound(new { message = "Content not found" });

        // Delete image file from disk if exists
        if (content.Image != null)
        {
            try
            {
                await _imageStorageService.DeleteImageAsync(content.Image.FilePath);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the delete operation
                Console.WriteLine($"Error deleting image file: {ex.Message}");
            }
        }

        // Delete related entities (cascade delete should handle this, but being explicit)
        _context.Predictions.RemoveRange(content.Predictions);
        _context.Reviews.RemoveRange(content.Reviews);
        if (content.Context != null)
            _context.Contexts.Remove(content.Context);
        
        // ContentImage will be deleted by cascade delete, but we can be explicit
        if (content.Image != null)
            _context.ContentImages.Remove(content.Image);

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

    private async Task EnsureContentImagesTableExists()
    {
        try
        {
            // Try to query the table - if it doesn't exist, this will throw
            await _context.ContentImages.FirstOrDefaultAsync();
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 208) // Error 208: Invalid object name
        {
            // Table doesn't exist, create it
            await _context.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ContentImages')
                BEGIN
                    CREATE TABLE [ContentImages] (
                        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                        [ContentId] uniqueidentifier NOT NULL,
                        [FileName] nvarchar(255) NOT NULL,
                        [OriginalFileName] nvarchar(255) NOT NULL,
                        [FilePath] nvarchar(500) NOT NULL,
                        [MimeType] nvarchar(100) NOT NULL,
                        [FileSize] bigint NOT NULL,
                        [ClassificationResult] nvarchar(1000) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        CONSTRAINT [FK_ContentImages_Contents_ContentId] FOREIGN KEY ([ContentId]) REFERENCES [Contents] ([Id]) ON DELETE CASCADE
                    );
                    
                    CREATE UNIQUE INDEX [IX_ContentImages_ContentId] ON [ContentImages] ([ContentId]);
                    CREATE INDEX [IX_ContentImages_CreatedAt] ON [ContentImages] ([CreatedAt]);
                END
            ");
        }
        catch (Exception ex)
        {
            // Log other potential errors during table check
            Console.WriteLine($"Error checking/creating ContentImages table: {ex.Message}");
            throw; // Re-throw to propagate the error
        }
    }
}

public class CreateContentRequest
{
    public ContentType Type { get; set; }
    public string Text { get; set; } = string.Empty;
    public string AuthorUsername { get; set; } = string.Empty;
    public Guid? ThreadId { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] // Don't try to deserialize IFormFile from JSON
    public IFormFile? Image { get; set; } // Nullable - images are optional
}
