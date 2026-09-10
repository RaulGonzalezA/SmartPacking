using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using SmartPacking.Api.Validation;
using SmartPacking.Infrastructure;

namespace SmartPacking.Api.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminController(
    SmartPackingDbContext dbContext,
    IValidator<SetPlanRequest> planValidator,
    IValidator<AddCreditsRequest> creditsValidator) : ControllerBase
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
        var validationProblem = await planValidator.ToProblemDetailsAsync(request, cancellationToken);
        if (validationProblem is not null)
        {
            return BadRequest(validationProblem);
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.AiPlan = request.Plan;
        AddAuditEvent(user.Id, "admin.plan_updated");
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("users/{userId:guid}/credits")]
    [Authorize(Policy = "AdminCredits")]
    public async Task<IActionResult> AddCreditsAsync(Guid userId, [FromBody] AddCreditsRequest request, CancellationToken cancellationToken)
    {
        var validationProblem = await creditsValidator.ToProblemDetailsAsync(request, cancellationToken);
        if (validationProblem is not null)
        {
            return BadRequest(validationProblem);
        }

        var credits = request.Credits!.Value;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var changed = await dbContext.Users
            .Where(item => item.Id == userId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    user => user.AiRecognitionCredits,
                    user => user.AiRecognitionCredits + credits),
                cancellationToken);
        if (changed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return NotFound();
        }

        AddAuditEvent(userId, "admin.credits_added");
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("billing")]
    [Authorize(Policy = "AdminBilling")]
    public IActionResult GetBilling() => Ok(new { Message = "La integración de facturación se conectará aquí." });

    [HttpGet("audit")]
    [Authorize(Policy = "AdminAudit")]
    public async Task<IActionResult> GetAuditAsync(CancellationToken cancellationToken)
    {
        var events = await dbContext.UserAuditEvents
            .OrderByDescending(item => item.OccurredAt).Take(200)
            .Select(item => new { item.UserId, item.Action, item.OccurredAt })
            .ToArrayAsync(cancellationToken);
        return Ok(events.Select(item => new { item.UserId, item.Action, OccurredAt = NormalizeAuditTimestamp(item.OccurredAt) }));
    }

    private void AddAuditEvent(Guid userId, string action) => dbContext.UserAuditEvents.Add(new UserAuditEventEntity
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Action = action,
        OccurredAt = DateTimeOffset.UtcNow
    });

    private static DateTimeOffset NormalizeAuditTimestamp(DateTimeOffset occurredAt) => occurredAt.Year < 2000 && occurredAt >= DateTimeOffset.UnixEpoch
        ? DateTimeOffset.FromUnixTimeSeconds(occurredAt.ToUnixTimeMilliseconds())
        : occurredAt;
}

public sealed record SetPlanRequest(string Plan);
public sealed record AddCreditsRequest(int? Credits);
