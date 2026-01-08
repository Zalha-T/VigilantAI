using AiAgents.ContentModerationAgent.Domain.Entities;

namespace AiAgents.ContentModerationAgent.ML;

public interface IContentClassifier
{
    Task<ContentScores> PredictAsync(string text, CancellationToken cancellationToken = default);
    Task<ModelMetrics> TrainAsync(List<Review> goldLabels, CancellationToken cancellationToken = default);
    Task LoadModelAsync(string modelPath, CancellationToken cancellationToken = default);
}
