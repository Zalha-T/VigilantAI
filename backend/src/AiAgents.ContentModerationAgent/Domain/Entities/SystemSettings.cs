namespace AiAgents.ContentModerationAgent.Domain.Entities;

public class SystemSettings
{
    public Guid Id { get; set; }
    public double AllowThreshold { get; set; } // Below this = Allow
    public double ReviewThreshold { get; set; } // Between Allow and Review = Review
    public double BlockThreshold { get; set; } // Above this = Block
    public int RetrainThreshold { get; set; } // Number of new gold labels needed for retraining
    public int NewGoldSinceLastTrain { get; set; }
    public DateTime? LastRetrainDate { get; set; }
    public bool RetrainingEnabled { get; set; } = true;
}
