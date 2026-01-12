using AiAgents.ContentModerationAgent.Infrastructure;
using AiAgents.ContentModerationAgent.ML;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiAgents.ContentModerationAgent.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModelController : ControllerBase
{
    private readonly IImageClassifier _imageClassifier;
    private readonly IContentClassifier _contentClassifier;
    private readonly ContentModerationDbContext _context;
    private readonly ILogger<ModelController> _logger;

    public ModelController(
        IImageClassifier imageClassifier,
        IContentClassifier contentClassifier,
        ContentModerationDbContext context,
        ILogger<ModelController> logger)
    {
        _imageClassifier = imageClassifier;
        _contentClassifier = contentClassifier;
        _context = context;
        _logger = logger;
    }

    [HttpPost("download-onnx")]
    public async Task<IActionResult> DownloadOnnxModel()
    {
        try
        {
            _logger.LogInformation("Starting manual ONNX model download...");
            
            // Create a dummy image to trigger model download
            var dummyImage = new byte[100]; // Small dummy image
            var result = await _imageClassifier.ClassifyAsync(dummyImage);
            
            _logger.LogInformation("ONNX model download completed. Result: {Result}", result.Details);
            
            return Ok(new 
            { 
                message = "ONNX model download initiated. Check console for progress.",
                details = result.Details
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading ONNX model");
            return StatusCode(500, new { message = $"Error downloading ONNX model: {ex.Message}" });
        }
    }

    [HttpGet("onnx-status")]
    public IActionResult GetOnnxStatus()
    {
        var modelsDir = Path.Combine(Directory.GetCurrentDirectory(), "models");
        var modelPath = Path.Combine(modelsDir, "resnet50-v2-7.onnx");
        var exists = System.IO.File.Exists(modelPath);
        
        return Ok(new
        {
            exists = exists,
            path = modelPath,
            directory = modelsDir,
            size = exists ? new System.IO.FileInfo(modelPath).Length : 0,
            sizeMB = exists ? Math.Round(new System.IO.FileInfo(modelPath).Length / (1024.0 * 1024.0), 2) : 0
        });
    }

    [HttpGet("ml-status")]
    public async Task<IActionResult> GetMLModelStatus()
    {
        try
        {
            // 1. Check database for model versions
            var allModels = await _context.ModelVersions
                .OrderByDescending(m => m.Version)
                .Select(m => new
                {
                    m.Version,
                    m.IsActive,
                    m.Accuracy,
                    m.Precision,
                    m.Recall,
                    m.F1Score,
                    m.TrainedAt,
                    m.TrainingSampleCount,
                    m.ModelPath
                })
                .ToListAsync();

            var activeModel = allModels.FirstOrDefault(m => m.IsActive);

            // 2. Check if model is loaded in memory
            bool isModelLoadedInMemory = false;
            if (_contentClassifier is MlNetContentClassifier mlClassifier)
            {
                isModelLoadedInMemory = mlClassifier.IsModelLoaded;
            }

            // 3. Check if model file exists on disk
            bool modelFileExists = false;
            long modelFileSize = 0;
            string? modelFilePath = null;
            
            if (activeModel != null && !string.IsNullOrEmpty(activeModel.ModelPath))
            {
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), activeModel.ModelPath);
                modelFilePath = Path.GetFullPath(fullPath);
                modelFileExists = System.IO.File.Exists(modelFilePath);
                
                if (modelFileExists)
                {
                    var fileInfo = new System.IO.FileInfo(modelFilePath);
                    modelFileSize = fileInfo.Length;
                }
            }

            // 4. Count gold labels available for training
            var goldLabelsCount = await _context.Reviews
                .Where(r => r.GoldLabel != null)
                .CountAsync();

            // 5. Get retraining settings
            var settings = await _context.SystemSettings.FirstOrDefaultAsync();
            var retrainThreshold = settings?.RetrainThreshold ?? 6;
            var newGoldSinceLastTrain = settings?.NewGoldSinceLastTrain ?? 0;
            var retrainingEnabled = settings?.RetrainingEnabled ?? false;

            return Ok(new
            {
                // Model status
                hasActiveModel = activeModel != null,
                isModelLoadedInMemory = isModelLoadedInMemory,
                modelFileExists = modelFileExists,
                
                // Active model details
                activeModel = activeModel != null ? new
                {
                    activeModel.Version,
                    activeModel.IsActive,
                    activeModel.Accuracy,
                    activeModel.Precision,
                    activeModel.Recall,
                    activeModel.F1Score,
                    activeModel.TrainedAt,
                    activeModel.TrainingSampleCount,
                    activeModel.ModelPath,
                    ModelFileExists = modelFileExists,
                    ModelFilePath = modelFilePath,
                    ModelFileSizeMB = modelFileExists ? Math.Round(modelFileSize / (1024.0 * 1024.0), 2) : 0
                } : null,
                
                // All models
                allModels = allModels,
                totalModels = allModels.Count,
                
                // Training data
                goldLabelsCount = goldLabelsCount,
                retrainingEnabled = retrainingEnabled,
                retrainThreshold = retrainThreshold,
                newGoldSinceLastTrain = newGoldSinceLastTrain,
                goldLabelsUntilRetrain = Math.Max(0, retrainThreshold - newGoldSinceLastTrain),
                
                // Status summary
                status = activeModel != null && isModelLoadedInMemory && modelFileExists
                    ? "ACTIVE_AND_LOADED"
                    : activeModel != null && modelFileExists
                        ? "ACTIVE_BUT_NOT_LOADED"
                        : activeModel != null
                            ? "ACTIVE_BUT_FILE_MISSING"
                            : "NO_ACTIVE_MODEL"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ML model status");
            return StatusCode(500, new { message = $"Error getting ML model status: {ex.Message}" });
        }
    }

    [HttpPost("save-active-model")]
    public async Task<IActionResult> SaveActiveModel()
    {
        try
        {
            var activeModel = await _context.ModelVersions
                .Where(m => m.IsActive)
                .OrderByDescending(m => m.Version)
                .FirstOrDefaultAsync();

            if (activeModel == null)
            {
                return NotFound(new { message = "No active model found" });
            }

            // Determine model path (same logic as TrainingService)
            var baseDirectory = AppContext.BaseDirectory;
            var modelsDirectory = Path.Combine(baseDirectory, "models");
            
            if (baseDirectory.Contains("bin"))
            {
                var binIndex = baseDirectory.IndexOf("bin", StringComparison.OrdinalIgnoreCase);
                var projectRoot = baseDirectory.Substring(0, binIndex).TrimEnd('\\', '/');
                modelsDirectory = Path.Combine(projectRoot, "models");
            }
            
            Directory.CreateDirectory(modelsDirectory);
            var modelPath = Path.GetFullPath(Path.Combine(modelsDirectory, $"model_v{activeModel.Version}.zip"));

            // Check if model is loaded in memory
            if (_contentClassifier is MlNetContentClassifier mlClassifier)
            {
                if (!mlClassifier.IsModelLoaded)
                {
                    return BadRequest(new { message = "Model is not loaded in memory. Please retrain the model first." });
                }

                // Save model to disk
                await _contentClassifier.SaveModelAsync(modelPath);
                
                _logger.LogInformation("Model v{Version} saved to: {ModelPath}", activeModel.Version, modelPath);

                return Ok(new
                {
                    message = $"Model v{activeModel.Version} saved successfully",
                    version = activeModel.Version,
                    path = modelPath,
                    fileExists = System.IO.File.Exists(modelPath),
                    fileSizeMB = System.IO.File.Exists(modelPath) 
                        ? Math.Round(new System.IO.FileInfo(modelPath).Length / (1024.0 * 1024.0), 2) 
                        : 0
                });
            }
            else
            {
                return BadRequest(new { message = "Content classifier is not MlNetContentClassifier" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving active model");
            return StatusCode(500, new { message = $"Error saving model: {ex.Message}" });
        }
    }

    [HttpPost("reload-active-model")]
    public async Task<IActionResult> ReloadActiveModel()
    {
        try
        {
            var activeModel = await _context.ModelVersions
                .Where(m => m.IsActive)
                .OrderByDescending(m => m.Version)
                .FirstOrDefaultAsync();

            if (activeModel == null)
            {
                return NotFound(new { message = "No active model found" });
            }

            // Determine model path (same logic as TrainingService)
            var baseDirectory = AppContext.BaseDirectory;
            var modelsDirectory = Path.Combine(baseDirectory, "models");
            
            if (baseDirectory.Contains("bin"))
            {
                var binIndex = baseDirectory.IndexOf("bin", StringComparison.OrdinalIgnoreCase);
                var projectRoot = baseDirectory.Substring(0, binIndex).TrimEnd('\\', '/');
                modelsDirectory = Path.Combine(projectRoot, "models");
            }
            
            var modelPath = Path.GetFullPath(Path.Combine(modelsDirectory, $"model_v{activeModel.Version}.zip"));

            if (!System.IO.File.Exists(modelPath))
            {
                return NotFound(new 
                { 
                    message = $"Model file not found at: {modelPath}",
                    path = modelPath,
                    suggestion = "Try saving the model first using POST /api/model/save-active-model"
                });
            }

            if (_contentClassifier is MlNetContentClassifier mlClassifier)
            {
                await mlClassifier.LoadModelAsync(modelPath);
                _logger.LogInformation("Model v{Version} reloaded from: {ModelPath}", activeModel.Version, modelPath);

                return Ok(new
                {
                    message = $"Model v{activeModel.Version} reloaded successfully",
                    version = activeModel.Version,
                    path = modelPath,
                    isLoaded = mlClassifier.IsModelLoaded
                });
            }
            else
            {
                return BadRequest(new { message = "Content classifier is not MlNetContentClassifier" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading active model");
            return StatusCode(500, new { message = $"Error reloading model: {ex.Message}" });
        }
    }
}
