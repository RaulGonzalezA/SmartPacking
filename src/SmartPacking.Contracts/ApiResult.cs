namespace SmartPacking.Contracts;

/// <summary>Envelope used by API endpoints that return data.</summary>
public sealed record ApiResult<T>(T Data);
