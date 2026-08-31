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
    Guid? OwnerProfileId = null);

public sealed record UserProfile(Guid Id, string Name);
public sealed record FamilyProfile(Guid Id, string Name);

public sealed record PackingList(Guid Id, Guid TripId, Guid UserId, DateTimeOffset CreatedAt, IReadOnlyCollection<PackingListItem> Items);
public sealed record PackingListItem(Guid ClothingItemId, bool IsPacked);
public sealed record ProfilePackingList(Guid Id, Guid TripId, Guid ProfileId, Guid UserId, DateTimeOffset CreatedAt, IReadOnlyCollection<PackingListItem> Items);
public enum ChecklistCategory { Documents, Toiletries, Technology, Health, Other }
public sealed record ChecklistItem(Guid Id, Guid TripId, ChecklistCategory Category, string Name, bool IsPacked);
public sealed record ClothingUsage(Guid TripId, Guid ClothingItemId, bool WasUsed);

public sealed record Trip(
    Guid Id,
    string Destination,
    DateOnly StartDate,
    DateOnly EndDate,
    int MinimumTemperatureCelsius,
    int MaximumTemperatureCelsius,
    IReadOnlyCollection<Style> Activities)
{
    public int Days => EndDate.DayNumber - StartDate.DayNumber + 1;
}
