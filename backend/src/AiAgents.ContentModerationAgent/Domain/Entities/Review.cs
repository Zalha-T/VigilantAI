using AiAgents.ContentModerationAgent.Domain.Enums;

namespace AiAgents.ContentModerationAgent.Domain.Entities;

public class Review
{
    public Guid Id { get; set; }
    public Guid ContentId { get; set; }
    public Guid? ModeratorId { get; set; }
    public ModerationDecision? GoldLabel { get; set; } // Nullable - null means not reviewed yet
    public string? Feedback { get; set; }
    public bool? CorrectDecision { get; set; } // True if agent decision was correct
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Navigation properties
    public Content Content { get; set; } = null!;
}
