namespace SmartPacking.Application;

public interface IGarmentRecognitionUsageService
{
    Task<GarmentRecognitionUsageResult> RegisterAttemptAsync(CancellationToken cancellationToken);
    Task<GarmentRecognitionUsageResult> GetUsageAsync(CancellationToken cancellationToken);
}

public sealed record GarmentRecognitionUsageResult(bool Allowed, int Used, int MonthlyLimit, int CreditBalance, int Remaining, bool IsNearLimit, string Plan, DateOnly PeriodStart);
