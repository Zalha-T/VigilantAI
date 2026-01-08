using AiAgents.ContentModerationAgent.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiAgents.ContentModerationAgent.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly IThresholdService _thresholdService;

    public SettingsController(IThresholdService thresholdService)
    {
        _thresholdService = thresholdService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var settings = await _thresholdService.GetSettingsAsync();
        return Ok(new
        {
            settings.AllowThreshold,
            settings.ReviewThreshold,
            settings.BlockThreshold,
            settings.RetrainThreshold,
            settings.NewGoldSinceLastTrain,
            settings.LastRetrainDate,
            settings.RetrainingEnabled
        });
    }

    [HttpPost("retrain-threshold")]
    public async Task<IActionResult> UpdateRetrainThreshold([FromBody] UpdateRetrainThresholdRequest request)
    {
        await _thresholdService.UpdateRetrainThresholdAsync(request.RetrainThreshold);
        return Ok(new { message = $"Retrain threshold updated to {request.RetrainThreshold}" });
    }

    [HttpPut("thresholds")]
    public async Task<IActionResult> UpdateThresholds([FromBody] UpdateThresholdsRequest request)
    {
        await _thresholdService.UpdateThresholdsAsync(
            request.AllowThreshold,
            request.ReviewThreshold,
            request.BlockThreshold
        );
        return Ok(new { message = "Thresholds updated successfully" });
    }
}

public class UpdateRetrainThresholdRequest
{
    public int RetrainThreshold { get; set; }
}

public class UpdateThresholdsRequest
{
    public double AllowThreshold { get; set; }
    public double ReviewThreshold { get; set; }
    public double BlockThreshold { get; set; }
}
