using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPacking.Infrastructure;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController(SmartPackingDbContext dbContext) : ControllerBase
{
    [HttpGet("users")]
    [Authorize(Policy = "AdminUsers")]
    public async Task<IActionResult> GetUsersAsync(CancellationToken cancellationToken) => Ok(await dbContext.Users
        .OrderBy(user => user.Name)
        .Select(user => new { user.Id, user.Name, user.IsOnboarded, user.AiPlan, user.AiRecognitionCredits })
        .ToArrayAsync(cancellationToken));

    [HttpPut("users/{userId:guid}/plan")]
    [Authorize(Policy = "AdminPlans")]
    public async Task<IActionResult> SetPlanAsync(Guid userId, [FromBody] SetPlanRequest request, CancellationToken cancellationToken)
    {
        if (request.Plan is not ("Free" or "Premium"))
        {
            return BadRequest(new ProblemDetails { Title = "Plan no válido" });
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.AiPlan = request.Plan;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("users/{userId:guid}/credits")]
    [Authorize(Policy = "AdminCredits")]
    public async Task<IActionResult> AddCreditsAsync(Guid userId, [FromBody] AddCreditsRequest request, CancellationToken cancellationToken)
    {
        if (request.Credits is null or <= 0 or > 10_000)
        {
            return BadRequest(new ProblemDetails { Title = "Cantidad de créditos no válida" });
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.AiRecognitionCredits += request.Credits.Value;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("billing")]
    [Authorize(Policy = "AdminBilling")]
    public IActionResult GetBilling() => Ok(new { Message = "La integración de facturación se conectará aquí." });

    [HttpGet("audit")]
    [Authorize(Policy = "AdminAudit")]
    public async Task<IActionResult> GetAuditAsync(CancellationToken cancellationToken) => Ok(await dbContext.UserAuditEvents
        .OrderByDescending(item => item.OccurredAt).Take(200)
        .Select(item => new { item.UserId, item.Action, item.OccurredAt }).ToArrayAsync(cancellationToken));
}

public sealed record SetPlanRequest(string Plan);
public sealed record AddCreditsRequest(int? Credits);
