using AiAgents.ContentModerationAgent.Domain.Entities;
using AiAgents.ContentModerationAgent.Infrastructure;
using AiAgents.ContentModerationAgent.ML;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiAgents.ContentModerationAgent.Application.Services;

public class TrainingService : ITrainingService
{
    private readonly ContentModerationDbContext _context;
    private readonly IContentClassifier _classifier;
    private readonly IThresholdService _thresholdService;
    private readonly ILogger<TrainingService>? _logger;

    public TrainingService(
        ContentModerationDbContext context,
        IContentClassifier classifier,
        IThresholdService thresholdService,
        ILogger<TrainingService>? logger = null)
    {
        _context = context;
        _classifier = classifier;
        _thresholdService = thresholdService;
        _logger = logger;
    }

    public async Task<bool> ShouldRetrainAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _thresholdService.GetSettingsAsync(cancellationToken);
        return settings.RetrainingEnabled && 
               settings.NewGoldSinceLastTrain >= settings.RetrainThreshold;
    }

    public async Task TrainModelAsync(bool activate = false, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("========== RETRAINING STARTED ==========");
        
        // Get all reviews with gold labels for training
        var goldLabels = await _context.Reviews
            .Include(r => r.Content)
            .Where(r => r.GoldLabel != null)
            .ToListAsync(cancellationToken);

        _logger?.LogInformation($"Found {goldLabels.Count} gold labels for training");

        if (goldLabels.Count < 10) // Minimum samples needed (reduced from 50 to allow faster learning)
        {
            var message = $"Not enough gold labels for training. Need at least 10, have {goldLabels.Count}. Retraining will be skipped until more feedback is provided.";
            _logger?.LogWarning(message);
            throw new InvalidOperationException(message);
        }

        _logger?.LogInformation("Starting model training with {Count} gold labels...", goldLabels.Count);

        // Train model
        var modelMetrics = await _classifier.TrainAsync(goldLabels, cancellationToken);

        _logger?.LogInformation("Model training completed. Metrics: Accuracy={Accuracy:P2}, Precision={Precision:P2}, Recall={Recall:P2}, F1Score={F1Score:P2}",
            modelMetrics.Accuracy, modelMetrics.Precision, modelMetrics.Recall, modelMetrics.F1Score);

        // Get next version number
        var maxVersion = await _context.ModelVersions
            .AnyAsync(cancellationToken)
            ? await _context.ModelVersions.MaxAsync(m => (int?)m.Version, cancellationToken) ?? 0
            : 0;

        var newVersion = maxVersion + 1;
        _logger?.LogInformation("Creating new model version: v{Version}", newVersion);

        // Determine model path (use absolute path for saving)
        // In ASP.NET Core, Directory.GetCurrentDirectory() might not be reliable
        // Use AppContext.BaseDirectory or ContentRootPath instead
        var baseDirectory = AppContext.BaseDirectory;
        var modelsDirectory = Path.Combine(baseDirectory, "models");
        
        // If running from bin/Debug/net8.0/, go up to project root
        if (baseDirectory.Contains("bin"))
        {
            var binIndex = baseDirectory.IndexOf("bin", StringComparison.OrdinalIgnoreCase);
            var projectRoot = baseDirectory.Substring(0, binIndex).TrimEnd('\\', '/');
            modelsDirectory = Path.Combine(projectRoot, "models");
        }
        
        Directory.CreateDirectory(modelsDirectory);
        var modelPath = Path.GetFullPath(Path.Combine(modelsDirectory, $"model_v{newVersion}.zip"));
        
        _logger?.LogInformation("Saving model to: {ModelPath}", modelPath);
        
        // Save model to disk
        try
        {
            await _classifier.SaveModelAsync(modelPath, cancellationToken);
            _logger?.LogInformation("Model saved to: {ModelPath}", modelPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save model to disk: {ModelPath}", modelPath);
            // Continue anyway - model is in memory
        }

        // Create new model version
        // Ensure no NaN or Infinity values (SQL Server doesn't support them)
        var accuracy = double.IsNaN(modelMetrics.Accuracy) || double.IsInfinity(modelMetrics.Accuracy) ? 0.0 : modelMetrics.Accuracy;
        var precision = double.IsNaN(modelMetrics.Precision) || double.IsInfinity(modelMetrics.Precision) ? 0.0 : modelMetrics.Precision;
        var recall = double.IsNaN(modelMetrics.Recall) || double.IsInfinity(modelMetrics.Recall) ? 0.0 : modelMetrics.Recall;
        var f1Score = double.IsNaN(modelMetrics.F1Score) || double.IsInfinity(modelMetrics.F1Score) ? 0.0 : modelMetrics.F1Score;
        
        var modelVersion = new ModelVersion
        {
            Id = Guid.NewGuid(),
            Version = newVersion,
            Accuracy = accuracy,
            Precision = precision,
            Recall = recall,
            F1Score = f1Score,
            IsActive = activate,
            ModelPath = $"models/model_v{newVersion}.zip", // Relative path for database
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
                _logger?.LogInformation("Deactivating old model version: v{Version}", model.Version);
                model.IsActive = false;
            }
            
            _logger?.LogInformation("Activating new model version: v{Version}", newVersion);
            
            // Load the new model into classifier if it's MlNetContentClassifier
            if (_classifier is MlNetContentClassifier mlClassifier)
            {
                try
                {
                    await mlClassifier.LoadModelAsync(modelPath, cancellationToken);
                    _logger?.LogInformation("New model v{Version} loaded into classifier", newVersion);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to load new model into classifier");
                }
            }
        }

        _context.ModelVersions.Add(modelVersion);

        // Reset counter and update LastRetrainDate
        var settings = await _thresholdService.GetSettingsAsync(cancellationToken);
        var previousCount = settings.NewGoldSinceLastTrain;
        settings.NewGoldSinceLastTrain = 0;
        settings.LastRetrainDate = DateTime.UtcNow;
        
        // Ensure the entity is tracked and marked as modified
        _context.SystemSettings.Update(settings);
        await _context.SaveChangesAsync(cancellationToken);
        
        _logger?.LogInformation("Settings updated: NewGoldSinceLastTrain reset to 0, LastRetrainDate set to {Date}", settings.LastRetrainDate);

        _logger?.LogInformation("========== RETRAINING COMPLETED ==========");
        _logger?.LogInformation("New model version v{Version} created and {ActivationStatus}. Previous gold labels count: {PreviousCount}, Reset to 0.",
            newVersion, activate ? "ACTIVATED" : "NOT ACTIVATED", previousCount);
    }
}
