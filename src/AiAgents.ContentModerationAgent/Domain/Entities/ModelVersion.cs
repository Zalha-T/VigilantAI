namespace AiAgents.ContentModerationAgent.Domain.Entities;

public class ModelVersion
{
    public Guid Id { get; set; }
    public int Version { get; set; }
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public bool IsActive { get; set; }
    public string ModelPath { get; set; } = string.Empty;
    public DateTime TrainedAt { get; set; }
    public int TrainingSampleCount { get; set; }
}
