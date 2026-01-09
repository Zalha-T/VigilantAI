using AiAgents.ContentModerationAgent.Domain.Enums;

namespace AiAgents.ContentModerationAgent.Domain.Entities;

public class Prediction
{
    public Guid Id { get; set; }
    public Guid ContentId { get; set; }
    public double SpamScore { get; set; }
    public double ToxicScore { get; set; }
    public double HateScore { get; set; }
    public double OffensiveScore { get; set; }
    public double FinalScore { get; set; }
    public ModerationDecision Decision { get; set; }
    public ConfidenceLevel Confidence { get; set; }
    public string ContextFactors { get; set; } = string.Empty; // JSON string
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Content Content { get; set; } = null!;
}
