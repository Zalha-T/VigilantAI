namespace AiAgents.ContentModerationAgent.Domain.Entities;

public class BlockedWord
{
    public Guid Id { get; set; }
    public string Word { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "toxic", "hate", "spam", "offensive", "slur"
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
