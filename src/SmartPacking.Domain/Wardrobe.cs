namespace SmartPacking.Domain;

public enum TripActivity { Sightseeing, Beach, Hiking, Business, FormalEvent, Sport, Nightlife, Relaxation }
public enum LuggageType { Backpack, Cabin, Checked }
public enum TransportType { Car, Plane, Train, Bus, Cruise }
public sealed record TransportOption(TransportType Type, bool IsAvailable, string Reason);
public sealed record TransportLeg(TransportType Type, string From, string To, int EstimatedMinutes, string Description);
public sealed record TransportPlan(string Summary, IReadOnlyCollection<TransportLeg> Legs);

public static class TransportPlanner
{
    public static TransportPlan Build(string? origin, string destination, IReadOnlyCollection<TransportType>? transportTypes)
    {
        var selected = transportTypes ?? [];
        var originName = string.IsNullOrWhiteSpace(origin) ? "Origen por confirmar" : origin.Trim();
        var destinationName = destination.Trim();
        if (selected.Contains(TransportType.Plane) && TransportAdvisor.GetOptions(originName, destinationName).Any(option => option.Type == TransportType.Plane && option.IsAvailable))
        {
            var departureAirport = AirportFor(originName);
            var arrivalAirport = AirportFor(destinationName);
            var transferMinutes = TransferMinutes(originName);
            var legs = new List<TransportLeg>();
            if (transferMinutes > 0)
            {
                legs.Add(new(TransportType.Car, originName, departureAirport, transferMinutes, "Traslado recomendado al aeropuerto de salida."));
            }

            legs.Add(new(TransportType.Plane, departureAirport, arrivalAirport, FlightMinutes(departureAirport, arrivalAirport), "Vuelo estimado; confirma horario y vuelo directo al reservar."));
            return new($"{originName} → {departureAirport} → {arrivalAirport}", legs);
        }

        if (selected.Contains(TransportType.Train) && TransportAdvisor.GetOptions(originName, destinationName).Any(option => option.Type == TransportType.Train && option.IsAvailable))
        {
            return new($"Tren desde {originName} hasta {destinationName}", [new(TransportType.Train, originName, destinationName, TrainMinutes(originName, destinationName), "Conexión ferroviaria orientativa; confirma transbordos y horarios.")]);
        }

        var mode = selected.Contains(TransportType.Bus) ? TransportType.Bus : TransportType.Car;
        return new($"{TransportName(mode)} desde {originName} hasta {destinationName}", [new(mode, originName, destinationName, 0, "Trayecto orientativo. Consulta duración y paradas antes de salir.")]);
    }

    private static string AirportFor(string place) => Normalize(place) switch
    {
        var value when value.Contains("OCAÑA") || value.Contains("MADRID") => "Madrid-Barajas",
        var value when value.Contains("ROMA") => "Roma Fiumicino",
        var value when value.Contains("BARCELONA") => "Barcelona-El Prat",
        var value when value.Contains("MALAGA") => "Málaga-Costa del Sol",
        _ => place
    };
    private static int TransferMinutes(string place) => Normalize(place).Contains("OCAÑA") ? 55 : 0;
    private static int FlightMinutes(string departure, string arrival) => departure.Contains("Madrid", StringComparison.OrdinalIgnoreCase) && arrival.Contains("Roma", StringComparison.OrdinalIgnoreCase) ? 150 : 120;
    private static int TrainMinutes(string origin, string destination) => Normalize(origin).Contains("MEDINA DEL CAMPO") && Normalize(destination).Contains("MADRID") ? 75 : 120;
    private static string TransportName(TransportType type) => type == TransportType.Bus ? "Autobús" : "Coche";
    private static string Normalize(string place) => place.Trim().ToUpperInvariant();
}

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

public sealed record UserAddress(string? Street, string? PostalCode, string? City, string? Region)
{
    public string? DisplayAddress => string.Join(" · ", new[] { Street, PostalCode, City, Region }.Where(value => !string.IsNullOrWhiteSpace(value)));
}

public sealed record UserProfile(Guid Id, string Name, bool IsOnboarded, string? Address = null, UserAddress? AddressDetails = null);
public sealed record FamilyProfile(Guid Id, string Name, bool IsArchived = false, string? PackingNotes = null, string? MedicalNotes = null);

public sealed record PackingList(Guid Id, Guid TripId, Guid UserId, DateTimeOffset CreatedAt, IReadOnlyCollection<PackingListItem> Items);
public enum RecommendationDecision { Current, Applied, Ignored }
public sealed record PackingListItem(Guid ClothingItemId, bool IsPacked, bool IsManual = false, RecommendationDecision RecommendationDecision = RecommendationDecision.Current);
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
    string? Origin = null,
    TransportPlan? TransportPlan = null,
    decimal? Latitude = null,
    decimal? Longitude = null)
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
