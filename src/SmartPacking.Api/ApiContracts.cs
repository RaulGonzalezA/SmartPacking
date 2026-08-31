using SmartPacking.Domain;

namespace SmartPacking.Api;

public sealed record SetPackedRequest(bool IsPacked);
public sealed record UpdateClothingStatusRequest(bool IsClean, bool IsAvailable);
public sealed record CreateChecklistItemRequest(ChecklistCategory Category, string Name);
public sealed record CreateTripRequest(string Destination, DateOnly StartDate, DateOnly EndDate, int MinimumTemperatureCelsius, int MaximumTemperatureCelsius, IReadOnlyCollection<Style> Activities);
public sealed record CreateFamilyProfileRequest(string Name);
public sealed record SetTripProfilesRequest(IReadOnlyCollection<Guid> ProfileIds);
