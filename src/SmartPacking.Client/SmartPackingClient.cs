using System.Net.Http.Json;
using SmartPacking.Application;
using SmartPacking.Contracts;
using SmartPacking.Domain;

namespace SmartPacking.Client;

public sealed class SmartPackingClient(HttpClient httpClient) : ISmartPackingClient
{
    public async Task<UserProfile> GetCurrentUserAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<UserProfile>("api/me", cancellationToken)
        ?? throw new InvalidOperationException("La API no devolvió el usuario actual.");

    public async Task<IReadOnlyList<Trip>> GetTripsAsync(CancellationToken cancellationToken) =>
        (await httpClient.GetFromJsonAsync<TripResponse[]>("api/trips", cancellationToken) ?? [])
            .Select(ToTrip)
            .ToArray();

    public Task<TripDashboard?> GetTripDashboardAsync(Guid tripId, Guid? selectedProfileId, CancellationToken cancellationToken)
    {
        var suffix = selectedProfileId.HasValue ? $"?profileId={selectedProfileId.Value}" : string.Empty;
        return httpClient.GetFromJsonAsync<TripDashboard>($"api/trips/{tripId}/dashboard{suffix}", cancellationToken);
    }

    public async Task SetChecklistPackedAsync(Guid itemId, bool isPacked, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/checklist/{itemId}", new { isPacked }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static Trip ToTrip(TripResponse trip) => new(
        trip.Id,
        trip.Destination,
        trip.StartDate,
        trip.EndDate,
        trip.MinimumTemperatureCelsius,
        trip.MaximumTemperatureCelsius,
        trip.Activities.Select(activity => (Style)activity).ToArray(),
        trip.TemplateKey,
        trip.LuggageAllowanceGrams,
        trip.CabinOnly,
        (LuggageType)trip.LuggageType,
        trip.LuggageHeightCentimetres,
        trip.LuggageWidthCentimetres,
        trip.LuggageDepthCentimetres,
        trip.DayPlans?.Select(plan => new TripDayPlan(plan.Date, plan.Activities.Select(activity => (TripActivity)activity).ToArray())).ToArray(),
        trip.AirlineCode,
        trip.TransportTypes?.Select(type => (TransportType)type).ToArray(),
        trip.Luggages?.Select(luggage => new TripLuggage(luggage.Id, (LuggageType)luggage.Type, luggage.AllowanceGrams, luggage.HeightCentimetres, luggage.WidthCentimetres, luggage.DepthCentimetres, luggage.Name)).ToArray(),
        trip.Origin,
        trip.TransportPlan is null ? null : new TransportPlan(trip.TransportPlan.Summary, trip.TransportPlan.Legs.Select(leg => new TransportLeg((TransportType)leg.Type, leg.From, leg.To, leg.EstimatedMinutes, leg.Description)).ToArray()),
        trip.Latitude,
        trip.Longitude);
}
