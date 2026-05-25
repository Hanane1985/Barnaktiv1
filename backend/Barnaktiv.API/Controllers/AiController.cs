using Barnaktiv.Application.DTOs.Ai;
using Barnaktiv.Application.Interfaces;
using Barnaktiv.Application.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Barnaktiv.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AiController(
    IActivityAiService activityAiService,
    IOptions<AiOptions> options) : ControllerBase
{
    public const string RateLimitPolicyName = "ai";

    /// <summary>Ask the activity assistant a question in natural language.</summary>
    [HttpPost("ask")]
    [EnableRateLimiting(RateLimitPolicyName)]
    [ProducesResponseType(typeof(AskResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AskResponseDto>> Ask(
        [FromBody] AskRequestDto? request,
        CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
        {
            return Problem(
                detail: "AI-assistenten är inte aktiverad. Sätt Ai:Enabled och Ai:ApiKey.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "AI not available");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { message = "Question is required." });
        }

        try
        {
            var response = await activityAiService.AskAsync(request.Question, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status502BadGateway,
                title: "AI request failed");
        }
    }
}
