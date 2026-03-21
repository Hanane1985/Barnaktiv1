using Barnaktiv.Application.DTOs.Activities;
using Barnaktiv.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Barnaktiv.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ActivitiesController(IActivityService activityService) : ControllerBase
{
    /// <summary>Returns all activities ordered by date.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ActivityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> GetAll(CancellationToken cancellationToken)
    {
        var activities = await activityService.GetAllAsync(cancellationToken);
        return Ok(activities);
    }
}
