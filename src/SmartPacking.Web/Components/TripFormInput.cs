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
    }

    public Trip ToTrip(Guid id) => new(id, Destination, StartDate, EndDate, MinimumTemperatureCelsius, MaximumTemperatureCelsius, [Style.Casual], TemplateKey, LuggageAllowanceGrams, CabinOnly);
}

public sealed record TravellerInput(string Name, string PackingNotes, string MedicalNotes);
