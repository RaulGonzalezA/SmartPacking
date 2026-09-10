namespace SmartPacking.Application;

public interface IGarmentRecognitionUsageService
{
    Task<GarmentRecognitionUsageResult> CheckAllowanceAsync(CancellationToken cancellationToken);
    Task<GarmentRecognitionUsageCommitResult> RegisterSuccessfulUsageAsync(CancellationToken cancellationToken);
    Task<GarmentRecognitionUsageResult> GetUsageAsync(CancellationToken cancellationToken);
}

public sealed record GarmentRecognitionUsageResult(bool Allowed, int Used, int MonthlyLimit, int CreditBalance, int Remaining, bool IsNearLimit, string Plan, DateOnly PeriodStart);
public sealed record GarmentRecognitionUsageCommitResult(bool Registered, GarmentRecognitionUsageResult Usage);
