namespace SmartPacking.Domain;

public enum ClothingType
{
    TShirt,
    Trousers,
    Shorts,
    Jacket,
    Shoes,
    Sandals,
    Accessory,
    Shirt,
    Sweater,
    Hoodie,
    Coat,
    Dress,
    Skirt,
    Underwear,
    Socks,
    Swimwear,
    Pyjamas,
    Belt,
    Bag
}

public enum Season { Summer, Winter, MidSeason, AllYear }
public enum Style { Casual, Formal, Sport, Business }

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
    string? PhotoUrl = null,
    string? Material = null);
