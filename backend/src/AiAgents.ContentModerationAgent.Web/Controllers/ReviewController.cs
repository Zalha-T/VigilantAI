using AiAgents.ContentModerationAgent.Application.Services;
using AiAgents.ContentModerationAgent.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace AiAgents.ContentModerationAgent.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpPost("{contentId}/review")]
    public async Task<IActionResult> SubmitReview(
        Guid contentId,
        [FromBody] SubmitReviewRequest request)
    {
        var review = await _reviewService.CreateReviewAsync(contentId);
        
        await _reviewService.UpdateReviewAsync(
            review.Id,
            request.GoldLabel,
            request.CorrectDecision,
            request.Feedback,
            request.ModeratorId);

        return Ok(new { ReviewId = review.Id });
    }
}

public class SubmitReviewRequest
{
    public ModerationDecision GoldLabel { get; set; }
    public bool? CorrectDecision { get; set; }
    public string? Feedback { get; set; }
    public Guid? ModeratorId { get; set; }
}
