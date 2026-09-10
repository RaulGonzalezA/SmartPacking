using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartPacking.Application;
using System.Data;

namespace SmartPacking.Infrastructure;

public sealed class GarmentRecognitionUsageService(
    SmartPackingDbContext dbContext,
    ISmartPackingStore store,
    IConfiguration configuration) : IGarmentRecognitionUsageService
{
    public Task<GarmentRecognitionUsageResult> CheckAllowanceAsync(CancellationToken cancellationToken) => GetUsageAsync(cancellationToken);

    public async Task<GarmentRecognitionUsageCommitResult> RegisterSuccessfulUsageAsync(CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return await CommitSuccessfulUsageAsync(user.Id, cancellationToken);
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                // A serializable transaction can be retried after a concurrent commit.
                dbContext.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException("No se pudo registrar el consumo de IA de forma segura.");
    }

    private async Task<GarmentRecognitionUsageCommitResult> CommitSuccessfulUsageAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var entity = await dbContext.Users.SingleAsync(candidate => candidate.Id == userId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var periodStart = new DateOnly(now.Year, now.Month, 1);
        var start = new DateTimeOffset(periodStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = start.AddMonths(1);
        var used = await dbContext.GarmentRecognitionEvents.CountAsync(item => item.UserId == userId && item.OccurredAt >= start && item.OccurredAt < end, cancellationToken);
        var plan = NormalizePlan(entity.AiPlan);
        var limit = PlanLimit(plan);
        var usesCredit = used >= limit;
        if (usesCredit && entity.AiRecognitionCredits <= 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new(false, CreateUsageResult(used, limit, entity.AiRecognitionCredits, plan, periodStart));
        }

        if (usesCredit)
        {
            entity.AiRecognitionCredits--;
        }

        dbContext.GarmentRecognitionEvents.Add(new GarmentRecognitionEventEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OccurredAt = now,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, CreateUsageResult(used + 1, limit, entity.AiRecognitionCredits, plan, periodStart));
    }

    public async Task<GarmentRecognitionUsageResult> GetUsageAsync(CancellationToken cancellationToken)
    {
        var user = await store.GetDefaultUserAsync(cancellationToken);
        var entity = await dbContext.Users.SingleAsync(candidate => candidate.Id == user.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var periodStart = new DateOnly(now.Year, now.Month, 1);
        var start = new DateTimeOffset(periodStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = start.AddMonths(1);
        var used = await dbContext.GarmentRecognitionEvents.CountAsync(
            item => item.UserId == user.Id && item.OccurredAt >= start && item.OccurredAt < end,
            cancellationToken);
        var plan = NormalizePlan(entity.AiPlan);
        var limit = PlanLimit(plan);
        return CreateUsageResult(used, limit, entity.AiRecognitionCredits, plan, periodStart);
    }

    private static string NormalizePlan(string? plan) => string.Equals(plan, "Premium", StringComparison.OrdinalIgnoreCase) ? "Premium" : "Free";

    private static GarmentRecognitionUsageResult CreateUsageResult(int used, int limit, int creditBalance, string plan, DateOnly periodStart)
    {
        var remaining = Math.Max(0, limit - used) + creditBalance;
        return new(remaining > 0, used, limit, creditBalance, remaining, remaining <= Math.Max(2, limit / 10), plan, periodStart);
    }

    private int PlanLimit(string plan)
    {
        var configured = configuration[$"Gemini:Plans:{plan}:MonthlyLimit"] ?? configuration["Gemini:MonthlyRecognitionLimit"];
        var defaultLimit = plan == "Premium" ? 200 : 20;
        return Math.Max(0, int.TryParse(configured, out var limit) ? limit : defaultLimit);
    }
}
