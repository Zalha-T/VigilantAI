using AiAgents.ContentModerationAgent.Domain.Enums;

namespace AiAgents.ContentModerationAgent.Domain.Entities;

public class Content
{
    public Guid Id { get; set; }
    public ContentType Type { get; set; }
    public string Text { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public Guid? ThreadId { get; set; }
    public ContentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    // Navigation properties
    public Author Author { get; set; } = null!;
    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public Context? Context { get; set; }
    public ContentImage? Image { get; set; }
}
