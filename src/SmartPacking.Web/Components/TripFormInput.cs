using SmartPacking.Domain;

namespace SmartPacking.Web.Components;

public sealed class TripFormInput
{
    public string Destination { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(3));
    public int MinimumTemperatureCelsius { get; set; } = 18;
    public int MaximumTemperatureCelsius { get; set; } = 28;
    public string? TemplateKey { get; set; }
    public int LuggageAllowanceGrams { get; set; } = 10000;
    public bool CabinOnly { get; set; } = true;
    public LuggageType LuggageType { get; set; } = LuggageType.Cabin;
    public int LuggageHeightCentimetres { get; set; } = 55;
    public int LuggageWidthCentimetres { get; set; } = 40;
    public int LuggageDepthCentimetres { get; set; } = 20;
    public List<TripDayPlanInput> DayPlans { get; } = [];
    public HashSet<TransportType> TransportTypes { get; } = [];
    public List<TripLuggageInput> Luggages { get; } = [TripLuggageInput.Create(LuggageType.Cabin)];
    public string? AirlineCode { get; set; }

    public void CopyFrom(Trip trip)
    {
        Destination = trip.Destination;
        StartDate = trip.StartDate;
        EndDate = trip.EndDate;
        MinimumTemperatureCelsius = trip.MinimumTemperatureCelsius;
        MaximumTemperatureCelsius = trip.MaximumTemperatureCelsius;
        TemplateKey = trip.TemplateKey;
        LuggageAllowanceGrams = trip.LuggageAllowanceGrams;
        CabinOnly = trip.CabinOnly;
        LuggageType = trip.LuggageType;
        LuggageHeightCentimetres = trip.LuggageHeightCentimetres;
        LuggageWidthCentimetres = trip.LuggageWidthCentimetres;
        LuggageDepthCentimetres = trip.LuggageDepthCentimetres;
        AirlineCode = trip.AirlineCode;
        TransportTypes.Clear();
        TransportTypes.UnionWith(trip.TransportTypesOrEmpty);
        Luggages.Clear();
        Luggages.AddRange(trip.LuggagesOrDefault.Select(TripLuggageInput.From));
        DayPlans.Clear();
        DayPlans.AddRange(trip.DayPlansOrEmpty.Select(plan => new TripDayPlanInput(plan.Date, plan.Activities)));
    }

    public void ApplyLuggageDefaults()
    {
        var profile = LuggageProfile.DefaultFor(LuggageType);
        LuggageAllowanceGrams = profile.AllowanceGrams;
        LuggageHeightCentimetres = profile.HeightCentimetres;
        LuggageWidthCentimetres = profile.WidthCentimetres;
        LuggageDepthCentimetres = profile.DepthCentimetres;
        CabinOnly = LuggageType == LuggageType.Cabin;
        if (Luggages.Count > 0)
        {
            Luggages[0].ApplyDefaults();
        }
    }

    public void ApplyAirlineRule()
    {
        var rule = AirlineLuggageCatalog.Find(AirlineCode);
        if (rule is null)
        {
            return;
        }

        TransportTypes.Add(TransportType.Plane);
        foreach (var luggage in Luggages.Where(luggage => luggage.Type == LuggageType.Cabin))
        {
            luggage.ApplyAirlineRule(rule);
        }
    }

    public void EnsureDayPlans()
    {
        var existing = DayPlans.ToDictionary(plan => plan.Date, plan => plan.Activities.ToArray());
        DayPlans.Clear();
        for (var day = StartDate; day <= EndDate; day = day.AddDays(1))
        {
            DayPlans.Add(new(day, existing.GetValueOrDefault(day, [TripActivity.Sightseeing])));
        }
    }

    public void SetTransport(TransportType transport, bool selected)
    {
        if (selected)
        {
            TransportTypes.Add(transport);
        }
        else
        {
            TransportTypes.Remove(transport);
            if (transport == TransportType.Plane)
            {
                AirlineCode = null;
            }
        }
    }

    public void AddLuggage() => Luggages.Add(TripLuggageInput.Create(LuggageType.Checked));
    public void ApplyLuggageType(TripLuggageInput luggage)
    {
        luggage.ApplyDefaults();
        if (luggage.Type == LuggageType.Cabin && AirlineLuggageCatalog.Find(AirlineCode) is { } rule)
        {
            luggage.ApplyAirlineRule(rule);
        }
    }
    public void RemoveLuggage(TripLuggageInput luggage)
    {
        if (Luggages.Count > 1)
        {
            Luggages.Remove(luggage);
        }
    }

    public Trip ToTrip(Guid id)
    {
        var primary = Luggages[0];
        return new(id, Destination, StartDate, EndDate, MinimumTemperatureCelsius, MaximumTemperatureCelsius, [Style.Casual], TemplateKey, primary.AllowanceGrams, primary.Type == LuggageType.Cabin, primary.Type, primary.HeightCentimetres, primary.WidthCentimetres, primary.DepthCentimetres, DayPlans.Select(plan => new TripDayPlan(plan.Date, plan.Activities.ToArray())).ToArray(), AirlineCode, TransportTypes.ToArray(), Luggages.Select(luggage => luggage.ToDomain()).ToArray());
    }
}

public sealed class TripLuggageInput
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Name { get; set; }
    public LuggageType Type { get; set; }
    public int AllowanceGrams { get; set; }
    public int HeightCentimetres { get; set; }
    public int WidthCentimetres { get; set; }
    public int DepthCentimetres { get; set; }

    public static TripLuggageInput Create(LuggageType type)
    {
        var profile = LuggageProfile.DefaultFor(type);
        return new() { Type = type, AllowanceGrams = profile.AllowanceGrams, HeightCentimetres = profile.HeightCentimetres, WidthCentimetres = profile.WidthCentimetres, DepthCentimetres = profile.DepthCentimetres };
    }

    public static TripLuggageInput From(TripLuggage luggage) => new() { Id = luggage.Id == Guid.Empty ? Guid.NewGuid() : luggage.Id, Name = luggage.Name, Type = luggage.Type, AllowanceGrams = luggage.AllowanceGrams, HeightCentimetres = luggage.HeightCentimetres, WidthCentimetres = luggage.WidthCentimetres, DepthCentimetres = luggage.DepthCentimetres };
    public void ApplyDefaults()
    {
        var profile = LuggageProfile.DefaultFor(Type);
        AllowanceGrams = profile.AllowanceGrams;
        HeightCentimetres = profile.HeightCentimetres;
        WidthCentimetres = profile.WidthCentimetres;
        DepthCentimetres = profile.DepthCentimetres;
    }

    public void ApplyAirlineRule(AirlineLuggageRule rule)
    {
        AllowanceGrams = rule.AllowanceGrams;
        HeightCentimetres = rule.HeightCentimetres;
        WidthCentimetres = rule.WidthCentimetres;
        DepthCentimetres = rule.DepthCentimetres;
    }

    public TripLuggage ToDomain() => new(Id, Type, AllowanceGrams, HeightCentimetres, WidthCentimetres, DepthCentimetres, Name);
}

public sealed record TravellerInput(string Name, string PackingNotes, string MedicalNotes);
public sealed class TripDayPlanInput
{
    public TripDayPlanInput(DateOnly date, TripActivity activity) : this(date, [activity]) { }

    public TripDayPlanInput(DateOnly date, IEnumerable<TripActivity> activities)
    {
        Date = date;
        Activities = activities.Any() ? activities.ToHashSet() : [TripActivity.Sightseeing];
    }

    public DateOnly Date { get; set; }
    public HashSet<TripActivity> Activities { get; }
    public void SetActivity(TripActivity activity, bool selected)
    {
        if (selected && (Activities.Contains(activity) || Activities.Count < 3))
        {
            Activities.Add(activity);
        }
        else if (Activities.Count > 1)
        {
            Activities.Remove(activity);
        }
    }
}
