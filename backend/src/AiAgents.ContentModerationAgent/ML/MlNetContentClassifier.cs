using AiAgents.ContentModerationAgent.Application.Services;
using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML;
using Microsoft.ML.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace AiAgents.ContentModerationAgent.ML;

public class MlNetContentClassifier : IContentClassifier
{
    private MLContext _mlContext;
    private ITransformer? _model;
    private readonly string _modelsDirectory;
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// Returns true if ML model is loaded in memory
    /// </summary>
    public bool IsModelLoaded => _model != null;

    public MlNetContentClassifier(string modelsDirectory = "models", IServiceProvider? serviceProvider = null)
    {
        _mlContext = new MLContext(seed: 0);
        _modelsDirectory = modelsDirectory;
        _serviceProvider = serviceProvider;
        Directory.CreateDirectory(_modelsDirectory);
    }

    public async Task<ContentScores> PredictAsync(string text, CancellationToken cancellationToken = default)
    {
        // 1. Calculate wordlist scores (rule-based, instant blocking)
        var wordlistScores = await CalculateWordlistScoresAsync(text, cancellationToken);
        
        // 2. Try to use ML model if available (contextual understanding)
        ContentScores? mlScores = null;
        if (_model != null)
        {
            try
            {
                mlScores = await PredictWithMLModelAsync(text, cancellationToken);
            }
            catch (Exception ex)
            {
                // If ML model fails, fallback to wordlist only
                // Log error if needed: System.Diagnostics.Debug.WriteLine($"ML model prediction failed: {ex.Message}");
            }
        }
        
        // 3. Combine wordlist and ML model scores
        if (mlScores != null)
        {
            // Use combination strategy: wordlist boost + ML model
            return CombineScores(wordlistScores, mlScores);
        }
        else
        {
            // No ML model available → use wordlist only
            return wordlistScores;
        }
    }

    private async Task<ContentScores> CalculateWordlistScoresAsync(string text, CancellationToken cancellationToken)
    {
        var lowerText = text.ToLowerInvariant();
        var originalText = text;
        
        // Base keyword lists (fallback if wordlist service is not available)
        var baseSpamKeywords = new[] { 
            "spam", "buy now", "click here", "click", "limited time", "deal", "offer", "this offer", 
            "amazing deal", "act now", "urgent", "free money", "limited offer", "special offer",
            "exclusive deal", "one time", "don't miss", "hurry", "today only"
        };
        var baseToxicKeywords = new[] { "fuck", "fucking", "bitch", "idiot", "stupid", "moron", "dumb", "retard", "asshole", "bastard", "crap" };
        var baseHateKeywords = new[] { "hate", "kill", "die", "you are an idiot", "i hate", "i fucking hate", "deserve to die", "should die", "wish you were dead" };
        var baseOffensiveKeywords = new[] { "fuck", "fucking", "bitch", "damn", "shit", "asshole", "crap", "hell", "bastard" };

        // Load dynamic wordlist from database if available
        List<string> spamKeywords = baseSpamKeywords.ToList();
        List<string> toxicKeywords = baseToxicKeywords.ToList();
        List<string> hateKeywords = baseHateKeywords.ToList();
        List<string> offensiveKeywords = baseOffensiveKeywords.ToList();

        if (_serviceProvider != null)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var wordlistService = scope.ServiceProvider.GetService<IWordlistService>();
                if (wordlistService != null)
                {
                    // Load dynamic words from database
                    var dynamicSpam = await wordlistService.GetActiveWordsByCategoryAsync("spam", cancellationToken);
                    var dynamicToxic = await wordlistService.GetActiveWordsByCategoryAsync("toxic", cancellationToken);
                    var dynamicHate = await wordlistService.GetActiveWordsByCategoryAsync("hate", cancellationToken);
                    var dynamicOffensive = await wordlistService.GetActiveWordsByCategoryAsync("offensive", cancellationToken);
                    var dynamicSlur = await wordlistService.GetActiveWordsByCategoryAsync("slur", cancellationToken);

                    // Combine base keywords with dynamic ones (avoid duplicates)
                    spamKeywords = spamKeywords.Union(dynamicSpam).ToList();
                    toxicKeywords = toxicKeywords.Union(dynamicToxic).Union(dynamicSlur).ToList(); // Slurs are also toxic
                    hateKeywords = hateKeywords.Union(dynamicHate).Union(dynamicSlur).ToList(); // Slurs are also hate
                    offensiveKeywords = offensiveKeywords.Union(dynamicOffensive).Union(dynamicSlur).ToList(); // Slurs are also offensive
                }
            }
            catch
            {
                // If wordlist service fails, use base keywords
            }
        }

        // Helper function to check if keyword matches text
        bool KeywordMatches(string keyword, string text)
        {
            // If keyword contains spaces, it's a phrase - use Contains()
            if (keyword.Contains(' '))
            {
                return text.Contains(keyword);
            }
            
            // For single words, use word boundary matching to avoid substring matches
            var pattern = $@"\b{Regex.Escape(keyword)}\b";
            return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
        }

        // Calculate scores based on keyword matches
        var spamMatches = spamKeywords.Count(k => KeywordMatches(k, lowerText));
        var toxicMatches = toxicKeywords.Count(k => KeywordMatches(k, lowerText));
        var hateMatches = hateKeywords.Count(k => KeywordMatches(k, lowerText));
        var offensiveMatches = offensiveKeywords.Count(k => KeywordMatches(k, lowerText));

        // Additional spam detection patterns
        // 1. Repeated words (e.g., "CLICK CLICK CLICK")
        var words = lowerText.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var repeatedWordCount = words
            .GroupBy(w => w)
            .Where(g => g.Count() >= 3)
            .Sum(g => g.Count() - 2); // Count extra repetitions beyond 2
        if (repeatedWordCount > 0)
        {
            spamMatches += repeatedWordCount; // Boost spam score for repeated words
        }

        // 2. Excessive punctuation (e.g., "!!!!!!", "???", "!!!")
        var exclamationCount = originalText.Count(c => c == '!');
        var questionCount = originalText.Count(c => c == '?');
        if (exclamationCount >= 3 || questionCount >= 3 || (exclamationCount + questionCount) >= 4)
        {
            spamMatches += 2; // Boost spam score for excessive punctuation
        }

        // 3. ALL CAPS detection
        var capsRatio = originalText.Count(char.IsUpper) / (double)Math.Max(1, originalText.Count(char.IsLetter));
        if (capsRatio > 0.7 && originalText.Count(char.IsLetter) > 5)
        {
            spamMatches += 1; // Boost spam score for all caps
        }

        // 4. Multiple spam indicators in short text
        if (text.Length < 100 && spamMatches >= 2)
        {
            spamMatches += 1; // Extra boost for short spammy text
        }

        // Enhanced score calculation: higher base scores and better multipliers
        var hasStrongToxic = toxicMatches > 0;
        var hasStrongHate = hateMatches > 0;
        var hasStrongOffensive = offensiveMatches > 0;
        
        // Base scores with higher minimums when keywords are found
        var spamScore = spamMatches > 0 
            ? Math.Min(0.95, 0.4 + (spamMatches * 0.2)) 
            : 0.05;
        
        var toxicScore = hasStrongToxic
            ? Math.Min(0.95, 0.5 + (toxicMatches * 0.2))
            : 0.05;
        
        var hateScore = hasStrongHate
            ? Math.Min(0.95, 0.6 + (hateMatches * 0.2))
            : 0.05;
        
        var offensiveScore = hasStrongOffensive
            ? Math.Min(0.95, 0.5 + (offensiveMatches * 0.2))
            : 0.05;

        // Boost if multiple categories are triggered (compound effect)
        var categoryCount = (hasStrongToxic ? 1 : 0) + (hasStrongHate ? 1 : 0) + (hasStrongOffensive ? 1 : 0);
        if (categoryCount >= 2)
        {
            toxicScore = Math.Min(0.95, toxicScore * 1.2);
            hateScore = Math.Min(0.95, hateScore * 1.2);
            offensiveScore = Math.Min(0.95, offensiveScore * 1.2);
        }

        return await Task.FromResult(new ContentScores
        {
            SpamScore = spamScore,
            ToxicScore = toxicScore,
            HateScore = hateScore,
            OffensiveScore = offensiveScore
        });
    }

    private async Task<ContentScores> PredictWithMLModelAsync(string text, CancellationToken cancellationToken)
    {
        if (_model == null)
            throw new InvalidOperationException("ML model is not loaded");

        // Create input data
        var input = new ContentInput { Text = text };
        var inputData = new[] { input };
        var dataView = _mlContext.Data.LoadFromEnumerable(inputData);
        
        // Make prediction
        var predictions = _model.Transform(dataView);
        var prediction = _mlContext.Data.CreateEnumerable<ContentPrediction>(predictions, reuseRowObject: false)
            .FirstOrDefault();
        
        if (prediction == null)
        {
            // Fallback to default scores if prediction fails
            return new ContentScores
            {
                SpamScore = 0.05,
                ToxicScore = 0.05,
                HateScore = 0.05,
                OffensiveScore = 0.05
            };
        }
        
        // Convert ML model prediction to ContentScores
        // Note: Current model only predicts IsSpam, so we use that for SpamScore
        // For other scores, we use the probability as a base (model would need multi-label training for full support)
        var spamProbability = prediction.PredictedLabel ? prediction.SpamProbability : (1.0f - prediction.SpamProbability);
        
        // Since current model is binary (IsSpam), we use the probability for spam
        // For other categories, we use a scaled version (model would need multi-label training for accurate scores)
        return await Task.FromResult(new ContentScores
        {
            SpamScore = Math.Max(0.05, Math.Min(0.95, spamProbability)),
            // For now, use scaled spam probability for other categories
            // In production, train separate models or use multi-label classification
            ToxicScore = Math.Max(0.05, Math.Min(0.95, spamProbability * 0.7)),
            HateScore = Math.Max(0.05, Math.Min(0.95, spamProbability * 0.6)),
            OffensiveScore = Math.Max(0.05, Math.Min(0.95, spamProbability * 0.8))
        });
    }

    private ContentScores CombineScores(ContentScores wordlistScores, ContentScores mlScores)
    {
        // Combination strategy: Wordlist boost + ML model contextual understanding
        // If wordlist has strong match → prioritize wordlist, otherwise use ML model with wordlist boost
        
        return new ContentScores
        {
            // Spam: If wordlist match → boost, otherwise use ML model
            SpamScore = CombineSingleScore(wordlistScores.SpamScore, mlScores.SpamScore, threshold: 0.4),
            
            // Toxic: If wordlist match → boost, otherwise use ML model
            ToxicScore = CombineSingleScore(wordlistScores.ToxicScore, mlScores.ToxicScore, threshold: 0.5),
            
            // Hate: If wordlist match → boost, otherwise use ML model
            HateScore = CombineSingleScore(wordlistScores.HateScore, mlScores.HateScore, threshold: 0.6),
            
            // Offensive: If wordlist match → boost, otherwise use ML model
            OffensiveScore = CombineSingleScore(wordlistScores.OffensiveScore, mlScores.OffensiveScore, threshold: 0.5)
        };
    }

    private double CombineSingleScore(double wordlistScore, double mlScore, double threshold)
    {
        // If wordlist has strong match → prioritize wordlist with ML boost
        if (wordlistScore > threshold)
        {
            // Wordlist match → boost with ML model contribution
            // Take maximum of wordlist and ML, with slight boost from ML
            return Math.Min(0.95, Math.Max(wordlistScore, mlScore) + (mlScore * 0.1));
        }
        else
        {
            // No strong wordlist match → use ML model with wordlist as context
            // Weighted average: 70% ML model, 30% wordlist
            return Math.Min(0.95, (mlScore * 0.7) + (wordlistScore * 0.3));
        }
    }

    public async Task<ModelMetrics> TrainAsync(List<Review> goldLabels, CancellationToken cancellationToken = default)
    {
        // Prepare training data
        var trainingData = new List<ContentInput>();
        var labels = new List<ContentLabel>();

        foreach (var review in goldLabels)
        {
            if (review.Content == null || review.GoldLabel == null)
                continue;

            trainingData.Add(new ContentInput { Text = review.Content.Text });
            labels.Add(new ContentLabel
            {
                IsSpam = review.GoldLabel == ModerationDecision.Block,
                IsToxic = review.GoldLabel == ModerationDecision.Block,
                IsHate = review.GoldLabel == ModerationDecision.Block,
                IsOffensive = review.GoldLabel == ModerationDecision.Block
            });
        }

        var dataView = _mlContext.Data.LoadFromEnumerable(
            trainingData.Zip(labels, (input, label) => new { Input = input, Label = label })
                .Select(x => new TrainingData
                {
                    Text = x.Input.Text,
                    IsSpam = x.Label.IsSpam,
                    IsToxic = x.Label.IsToxic,
                    IsHate = x.Label.IsHate,
                    IsOffensive = x.Label.IsOffensive
                })
        );

        // Define pipeline
        var pipeline = _mlContext.Transforms.Text.FeaturizeText("Features", "Text")
            .Append(_mlContext.BinaryClassification.Trainers.FastTree(
                labelColumnName: "IsSpam",
                numberOfLeaves: 20,
                numberOfTrees: 100));

        // Train model (simplified - would need multi-label classification in production)
        var model = pipeline.Fit(dataView);

        // Evaluate (simplified)
        var predictions = model.Transform(dataView);
        var metrics = _mlContext.BinaryClassification.Evaluate(predictions, "IsSpam");

        _model = model;

        // Calculate F1Score, handling division by zero (when precision + recall = 0)
        var precision = metrics.PositivePrecision;
        var recall = metrics.PositiveRecall;
        var f1Score = (precision + recall) > 0
            ? 2 * (precision * recall) / (precision + recall)
            : 0.0; // If both are 0, F1Score is 0 (not NaN)

        return new ModelMetrics
        {
            Accuracy = metrics.Accuracy,
            Precision = precision,
            Recall = recall,
            F1Score = f1Score
        };
    }

    public async Task LoadModelAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(modelPath);
        if (File.Exists(fullPath))
        {
            _model = _mlContext.Model.Load(fullPath, out _);
        }
        else if (File.Exists(modelPath))
        {
            _model = _mlContext.Model.Load(modelPath, out _);
        }
        await Task.CompletedTask;
    }

    public async Task SaveModelAsync(string modelPath, CancellationToken cancellationToken = default)
    {
        if (_model == null)
            throw new InvalidOperationException("No model to save. Train model first.");
        
        // Create directory if it doesn't exist
        var directory = Path.GetDirectoryName(modelPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Save model
        var fullPath = Path.GetFullPath(modelPath);
        _mlContext.Model.Save(_model, null, fullPath);
        await Task.CompletedTask;
    }

    private async Task LoadDefaultModelAsync(CancellationToken cancellationToken = default)
    {
        // Don't create ML model - use heuristic instead
        // ML model creation is complex and requires proper training data
        // For now, PredictAsync will use keyword-based heuristics
        await Task.CompletedTask;
    }
}

// Helper classes for ML.NET
internal class ContentInput
{
    public string Text { get; set; } = string.Empty;
}

internal class ContentPrediction
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabel { get; set; }

    public float SpamProbability { get; set; }
    public float ToxicProbability { get; set; }
    public float HateProbability { get; set; }
    public float OffensiveProbability { get; set; }
}

internal class ContentLabel
{
    public bool IsSpam { get; set; }
    public bool IsToxic { get; set; }
    public bool IsHate { get; set; }
    public bool IsOffensive { get; set; }
}

internal class TrainingData
{
    public string Text { get; set; } = string.Empty;
    public bool IsSpam { get; set; }
    public bool IsToxic { get; set; }
    public bool IsHate { get; set; }
    public bool IsOffensive { get; set; }
}
