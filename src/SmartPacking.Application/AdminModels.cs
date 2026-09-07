namespace SmartPacking.Application;

public sealed record AdminUserSummary(Guid Id, string Name, bool IsOnboarded, string AiPlan, int AiRecognitionCredits);
public sealed record AdminAuditEntry(Guid UserId, string Action, DateTimeOffset OccurredAt);
