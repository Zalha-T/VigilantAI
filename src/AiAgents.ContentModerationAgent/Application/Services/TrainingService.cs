using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Infrastructure;
using AiAgents.ContentModerationAgent.ML;
using Microsoft.EntityFrameworkCore;

namespace AiAgents.ContentModerationAgent.Application.Services;

public class TrainingService : ITrainingService
{
    private readonly ContentModerationDbContext _context;
    private readonly IContentClassifier _classifier;
    private readonly IThresholdService _thresholdService;

    public TrainingService(
        ContentModerationDbContext context,
        IContentClassifier classifier,
        IThresholdService thresholdService)
    {
        _context = context;
        _classifier = classifier;
        _thresholdService = thresholdService;
    }

    public async Task<bool> ShouldRetrainAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _thresholdService.GetSettingsAsync(cancellationToken);
        return settings.RetrainingEnabled && 
               settings.NewGoldSinceLastTrain >= settings.RetrainThreshold;
    }

    public async Task TrainModelAsync(bool activate = false, CancellationToken cancellationToken = default)
    {
        // Get all reviews with gold labels for training
        var goldLabels = await _context.Reviews
            .Include(r => r.Content)
            .Where(r => r.GoldLabel != null)
            .ToListAsync(cancellationToken);

        if (goldLabels.Count < 10) // Minimum samples needed (reduced from 50 to allow faster learning)
        {
            var message = $"Not enough gold labels for training. Need at least 10, have {goldLabels.Count}. Retraining will be skipped until more feedback is provided.";
            // Log warning instead of throwing exception - allows system to continue
            System.Diagnostics.Debug.WriteLine(message);
            throw new InvalidOperationException(message);
        }

        // Train model
        var modelMetrics = await _classifier.TrainAsync(goldLabels, cancellationToken);

        // Get next version number
        var maxVersion = await _context.ModelVersions
            .Select(m => m.Version)
            .DefaultIfEmpty(0)
            .MaxAsync(cancellationToken);

        // Create new model version
        var modelVersion = new ModelVersion
        {
            Id = Guid.NewGuid(),
            Version = maxVersion + 1,
            Accuracy = modelMetrics.Accuracy,
            Precision = modelMetrics.Precision,
            Recall = modelMetrics.Recall,
            F1Score = modelMetrics.F1Score,
            IsActive = activate,
            ModelPath = $"models/model_v{maxVersion + 1}.zip",
            TrainedAt = DateTime.UtcNow,
            TrainingSampleCount = goldLabels.Count
        };

        // Deactivate old models if activating new one
        if (activate)
        {
            var activeModels = await _context.ModelVersions
                .Where(m => m.IsActive)
                .ToListAsync(cancellationToken);
            
            foreach (var model in activeModels)
            {
                model.IsActive = false;
            }
        }

        _context.ModelVersions.Add(modelVersion);

        // Reset counter
        var settings = await _thresholdService.GetSettingsAsync(cancellationToken);
        settings.NewGoldSinceLastTrain = 0;
        settings.LastRetrainDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
