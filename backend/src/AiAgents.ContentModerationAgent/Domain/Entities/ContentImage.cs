namespace AiAgents.ContentModerationAgent.Domain.Entities;

public class ContentImage
{
    public Guid Id { get; set; }
    public Guid ContentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty; // Relative path: uploads/images/{contentId}/{fileName}
    public string MimeType { get; set; } = string.Empty; // image/jpeg, image/png, etc.
    public long FileSize { get; set; } // Bytes
    public string? ClassificationResult { get; set; } // JSON: {"label": "dog", "confidence": 0.95, "isBlocked": true}
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Content Content { get; set; } = null!;
}
