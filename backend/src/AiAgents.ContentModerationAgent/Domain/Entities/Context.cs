namespace AiAgents.ContentModerationAgent.Domain.Entities;

public class Context
{
    public Guid Id { get; set; }
    public Guid ContentId { get; set; }
    public double AuthorReputation { get; set; } // 0-1 normalized
    public double ThreadSentiment { get; set; } // -1 to 1
    public double EngagementLevel { get; set; } // 0-1 normalized
    public int TimeOfDay { get; set; } // 0-23
    public int DayOfWeek { get; set; } // 0-6
    public string Language { get; set; } = "en";
    public int ContentLength { get; set; }

    // Navigation properties
    public Content Content { get; set; } = null!;
}
