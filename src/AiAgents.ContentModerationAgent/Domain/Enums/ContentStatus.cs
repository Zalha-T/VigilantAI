namespace AiAgents.ContentModerationAgent.Domain.Enums;

public enum ContentStatus
{
    Queued = 1,
    Processing = 2,
    Approved = 3,
    PendingReview = 4,
    Blocked = 5
}
