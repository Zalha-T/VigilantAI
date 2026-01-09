namespace AiAgents.ContentModerationAgent.Domain.Entities;

public class Author
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public int ReputationScore { get; set; } // 0-100
    public int AccountAgeDays { get; set; }
    public int PreviousViolations { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public ICollection<Content> Contents { get; set; } = new List<Content>();
}
