using AiAgents.ContentModerationAgent.Domain.Enums;

namespace AiAgents.ContentModerationAgent.Application.DTOs;

public class ModerationTickResult
{
    public Guid ContentId { get; set; }
    public ModerationDecision Decision { get; set; }
    public ConfidenceLevel Confidence { get; set; }
    public double FinalScore { get; set; }
    public ContentStatus NewStatus { get; set; }
    public string ContextFactors { get; set; } = string.Empty;
}
