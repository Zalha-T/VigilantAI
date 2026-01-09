using AiAgents.ContentModerationAgent.ML;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AiAgents.ContentModerationAgent.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModelController : ControllerBase
{
    private readonly IImageClassifier _imageClassifier;
    private readonly ILogger<ModelController> _logger;

    public ModelController(IImageClassifier imageClassifier, ILogger<ModelController> logger)
    {
        _imageClassifier = imageClassifier;
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
}
