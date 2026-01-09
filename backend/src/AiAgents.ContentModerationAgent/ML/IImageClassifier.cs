namespace AiAgents.ContentModerationAgent.ML;

public class ImageClassificationResult
{
    public string Label { get; set; } = string.Empty; // "dog", "cat", "other"
    public float Confidence { get; set; } // 0.0 - 1.0
    public bool IsBlocked { get; set; } // true if dog detected
    public string Details { get; set; } = string.Empty; // Human-readable description
    public List<TopPrediction>? TopPredictions { get; set; } // Top N predictions with their scores
}

public class TopPrediction
{
    public string Label { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public int ClassIndex { get; set; }
}

public interface IImageClassifier
{
    Task<ImageClassificationResult> ClassifyAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}
