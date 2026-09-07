using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartPacking.Application;

namespace SmartPacking.Infrastructure;

public sealed class GarmentRecognitionUsageService(
    SmartPackingDbContext dbContext,
    ISmartPackingStore store,
    IConfiguration configuration) : IGarmentRecognitionUsageService
{
    public async Task<GarmentRecognitionUsageResult> RegisterAttemptAsync(CancellationToken cancellationToken)
    {
        var usage = await GetUsageAsync(cancellationToken);
        if (!usage.Allowed)
        {
            return usage;
        }

        var user = await store.GetDefaultUserAsync(cancellationToken);
        var entity = await dbContext.Users.SingleAsync(candidate => candidate.Id == user.Id, cancellationToken);
        if (usage.Used >= usage.MonthlyLimit && entity.AiRecognitionCredits > 0)
        {
            entity.AiRecognitionCredits--;
        }
        var now = DateTimeOffset.UtcNow;
        dbContext.GarmentRecognitionEvents.Add(new GarmentRecognitionEventEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OccurredAt = now,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetUsageAsync(cancellationToken);
    }

    public async Task<GarmentRecognitionUsageResult> GetUsageAsync(CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        var entity = await dbContext.Users.SingleAsync(candidate => candidate.Id == user.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var periodStart = new DateOnly(now.Year, now.Month, 1);
        var start = new DateTimeOffset(periodStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = start.AddMonths(1);
        var entries = await dbContext.GarmentRecognitionEvents.Where(item => item.UserId == user.Id).ToListAsync(cancellationToken);
        var used = entries.Count(item => item.OccurredAt >= start && item.OccurredAt < end);
        var plan = string.Equals(entity.AiPlan, "Premium", StringComparison.OrdinalIgnoreCase) ? "Premium" : "Free";
        var limit = PlanLimit(plan);
        var remaining = Math.Max(0, limit - used) + entity.AiRecognitionCredits;
        return new GarmentRecognitionUsageResult(remaining > 0, used, limit, entity.AiRecognitionCredits, remaining, remaining <= Math.Max(2, limit / 10), plan, periodStart);
    }

    private int PlanLimit(string plan)
    {
        var configured = configuration[$"Gemini:Plans:{plan}:MonthlyLimit"] ?? configuration["Gemini:MonthlyRecognitionLimit"];
        var defaultLimit = plan == "Premium" ? 200 : 20;
        return Math.Max(0, int.TryParse(configured, out var limit) ? limit : defaultLimit);
    }
}
