namespace SmartPacking.Domain;

public enum ClothingType { TShirt, Trousers, Shorts, Jacket, Shoes, Sandals, Accessory }
public enum Season { Summer, Winter, MidSeason, AllYear }
public enum Style { Casual, Formal, Sport }
public enum TripActivity { Sightseeing, Beach, Hiking, Business, FormalEvent, Sport, Nightlife, Relaxation }
public enum LuggageType { Backpack, Cabin, Checked }
public enum TransportType { Car, Plane, Train, Bus, Cruise }
public sealed record TransportOption(TransportType Type, bool IsAvailable, string Reason);

public static class TransportAdvisor
{
    public static IReadOnlyList<TransportOption> GetOptions(string? origin, string destination)
    {
        var originPlace = Normalize(origin);
        var destinationPlace = Normalize(destination);
        var hasAirport = IsNearAirport(originPlace) && IsNearAirport(destinationPlace);
        var hasTrain = HasRailStation(originPlace) && HasRailStation(destinationPlace);
        var hasPort = HasPort(originPlace) && HasPort(destinationPlace);
        return
        [
            new(TransportType.Car, true, "Disponible por carretera entre origen y destino."),
            new(TransportType.Bus, true, "Puede requerir un transbordo; confirma horarios y paradas."),
            new(TransportType.Plane, hasAirport, hasAirport ? $"Salida aérea posible desde {NearestAirport(originPlace)}." : "No se ha identificado un aeropuerto cercano en ambos extremos."),
            new(TransportType.Train, hasTrain, hasTrain ? "Hay estación ferroviaria en origen y destino o su entorno." : "No se ha identificado conexión ferroviaria en ambos extremos."),
            new(TransportType.Cruise, hasPort, hasPort ? "Hay puerto de pasajeros en origen y destino o su entorno." : "No se ha identificado puerto de pasajeros en ambos extremos.")
        ];
    }

    private static string Normalize(string? place) => (place ?? string.Empty).Trim().ToUpperInvariant();
    private static bool IsNearAirport(string place) => place.Contains("MADRID") || place.Contains("OCAÑA") || place.Contains("ROMA") || place.Contains("BARCELONA") || place.Contains("SEVILLA") || place.Contains("VALENCIA") || place.Contains("MALAGA");
    private static bool HasRailStation(string place) => !place.Contains("OCAÑA") && (place.Contains("MEDINA DEL CAMPO") || place.Contains("MADRID") || place.Contains("ROMA") || place.Contains("BARCELONA") || place.Contains("SEVILLA") || place.Contains("VALENCIA"));
    private static bool HasPort(string place) => place.Contains("ROMA") || place.Contains("BARCELONA") || place.Contains("VALENCIA") || place.Contains("MALAGA") || place.Contains("PALMA");
    private static string NearestAirport(string place)
    {
        if (place.Contains("OCAÑA"))
        {
            return "Madrid-Barajas (con traslado desde Ocaña)";
        }

        if (place.Contains("ROMA"))
        {
            return "Roma Fiumicino/Ciampino";
        }
        return place;
    }
}
public sealed record TripLuggage(
    Guid Id,
    LuggageType Type,
    int AllowanceGrams,
    int HeightCentimetres,
    int WidthCentimetres,
    int DepthCentimetres,
    string? Name = null);
public sealed record LuggageProfile(LuggageType Type, int AllowanceGrams, int HeightCentimetres, int WidthCentimetres, int DepthCentimetres)
{
    public static LuggageProfile DefaultFor(LuggageType type) => type switch
    {
        LuggageType.Backpack => new(type, 5000, 40, 30, 20),
        LuggageType.Cabin => new(type, 10000, 55, 40, 20),
        _ => new(type, 23000, 75, 50, 30)
    };
}
public sealed record AirlineLuggageRule(string Code, string Name, int AllowanceGrams, int HeightCentimetres, int WidthCentimetres, int DepthCentimetres, string Note)
{
    public int CapacityMillilitres => HeightCentimetres * WidthCentimetres * DepthCentimetres * 1000;
}

public static class AirlineLuggageCatalog
{
    public static readonly IReadOnlyList<AirlineLuggageRule> All =
    [
        new("iberia", "Iberia · cabina Economy", 10000, 56, 40, 25, "Incluye ruedas y asas; revisa la tarifa antes de volar."),
        new("vueling", "Vueling · cabina", 10000, 55, 40, 20, "La franquicia depende de la tarifa contratada."),
        new("ryanair-priority", "Ryanair · Priority", 10000, 55, 40, 20, "Solo con la opción Priority; sin ella se aplica el bulto pequeño.")
    ];

    public static AirlineLuggageRule? Find(string? code) => All.SingleOrDefault(rule => string.Equals(rule.Code, code, StringComparison.OrdinalIgnoreCase));
}
public sealed record TripDayPlan(DateOnly Date, IReadOnlyCollection<TripActivity> Activities);

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

public sealed record UserProfile(Guid Id, string Name, bool IsOnboarded, string? Address = null);
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
    bool CabinOnly = true,
    LuggageType LuggageType = LuggageType.Cabin,
    int LuggageHeightCentimetres = 55,
    int LuggageWidthCentimetres = 40,
    int LuggageDepthCentimetres = 20,
    IReadOnlyCollection<TripDayPlan>? DayPlans = null,
    string? AirlineCode = null,
    IReadOnlyCollection<TransportType>? TransportTypes = null,
    IReadOnlyCollection<TripLuggage>? Luggages = null,
    string? Origin = null)
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

    public IReadOnlyCollection<TripDayPlan> DayPlansOrEmpty => DayPlans ?? [];
    public IReadOnlyCollection<TransportType> TransportTypesOrEmpty => TransportTypes ?? [];
    public IReadOnlyCollection<TripLuggage> LuggagesOrDefault => Luggages is { Count: > 0 }
        ? Luggages
        : [new TripLuggage(Guid.Empty, LuggageType, LuggageAllowanceGrams, LuggageHeightCentimetres, LuggageWidthCentimetres, LuggageDepthCentimetres)];
}

public enum TripStatus { Planning, InProgress, Completed }
