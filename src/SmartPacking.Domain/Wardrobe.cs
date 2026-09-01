namespace SmartPacking.Domain;

public enum ClothingType { TShirt, Trousers, Shorts, Jacket, Shoes, Sandals, Accessory }
public enum Season { Summer, Winter, MidSeason, AllYear }
public enum Style { Casual, Formal, Sport }

public sealed record ClothingItem(
    Guid Id,
    string Name,
    ClothingType Type,
    Season Season,
    string Color,
    int WarmthLevel,
    bool Waterproof,
    Style Style,
    int? WeightGrams,
    bool IsClean,
    bool IsAvailable,
    int PreferenceScore,
    IReadOnlyCollection<Guid> CombinesWith,
    bool IsDeleted = false,
    Guid? OwnerProfileId = null,
    string? PhotoUrl = null);

public sealed record UserProfile(Guid Id, string Name);
public sealed record FamilyProfile(Guid Id, string Name, bool IsArchived = false, string? PackingNotes = null, string? MedicalNotes = null);

public sealed record PackingList(Guid Id, Guid TripId, Guid UserId, DateTimeOffset CreatedAt, IReadOnlyCollection<PackingListItem> Items);
public sealed record PackingListItem(Guid ClothingItemId, bool IsPacked);
public sealed record ProfilePackingList(Guid Id, Guid TripId, Guid ProfileId, Guid UserId, DateTimeOffset CreatedAt, IReadOnlyCollection<PackingListItem> Items);
public enum ChecklistCategory { Documents, Toiletries, Technology, Health, Other }
public sealed record ChecklistItem(Guid Id, Guid TripId, ChecklistCategory Category, string Name, bool IsPacked, Guid? ProfileId = null);
public sealed record ClothingUsage(Guid TripId, Guid ClothingItemId, bool WasUsed);

public sealed record Trip(
    Guid Id,
    string Destination,
    DateOnly StartDate,
    DateOnly EndDate,
    int MinimumTemperatureCelsius,
    int MaximumTemperatureCelsius,
    IReadOnlyCollection<Style> Activities,
    string? TemplateKey = null,
    int LuggageAllowanceGrams = 10000,
    bool CabinOnly = true)
{
    public int Days => EndDate.DayNumber - StartDate.DayNumber + 1;
    public TripStatus GetStatus(DateOnly today)
    {
        if (EndDate < today)
        {
            return TripStatus.Completed;
        }

        return StartDate <= today ? TripStatus.InProgress : TripStatus.Planning;
    }
}

public enum TripStatus { Planning, InProgress, Completed }
