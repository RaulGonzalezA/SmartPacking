using SmartPacking.Domain;

#pragma warning disable S3881

namespace SmartPacking.Web.Components.Pages;

public partial class Home
{
    private Task CreateTripAsync(TripFormInput input) => RunAsync(async () =>
    {
        var created = await Api.CreateTripAsync(input.ToTrip(Guid.NewGuid()), CancellationToken.None);
        await RefreshTripsAsync();
        State.SelectTrip(created.Id);
        await RefreshTripDetailsAsync();
        State.Feedback = "Viaje creado. Ya puedes completar sus detalles.";
    });

    private Task SaveTripAsync(Trip trip) => RunAsync(async () =>
    {
        await Api.UpdateTripAsync(trip, CancellationToken.None);
        await RefreshTripsAsync();
        await RefreshTripDetailsAsync();
        State.Feedback = "Viaje actualizado.";
    });

    private Task DeleteTripAsync() => RunAsync(async () =>
    {
        var tripId = State.SelectedTripId;
        if (tripId == Guid.Empty)
        {
            return;
        }

        BeginLoad();
        State.SelectTrip(Guid.Empty);
        State.ClearSelectedTripData();
        await Api.DeleteTripAsync(tripId, LoadCancellationToken);
        await RefreshTripsAsync();
        await RefreshTripDetailsAsync();
        State.Feedback = "Viaje eliminado. Selecciona otro viaje para continuar.";
    });

    private Task AddTravellerAsync(TravellerInput input) => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            State.Feedback = "Escribe el nombre del viajero.";
            return;
        }

        var profile = await Api.CreateProfileAsync(input.Name.Trim(), input.PackingNotes, input.MedicalNotes, CancellationToken.None);
        await Api.SetTripProfilesAsync(State.SelectedTripId, State.TripProfiles.Select(item => item.Id).Append(profile.Id).ToArray(), CancellationToken.None);
        await RefreshTripsAsync();
        await RefreshTripDetailsAsync();
        State.Feedback = $"{profile.Name} se ha añadido como viajero.";
    });

    private Task SaveTravellersAsync(IReadOnlyCollection<Guid> ids) => RunAsync(async () =>
    {
        await Api.SetTripProfilesAsync(State.SelectedTripId, ids, CancellationToken.None);
        await RefreshTripDetailsAsync();
        State.Feedback = "Viajeros guardados.";
    });

    private Task SaveTravellerAsync(FamilyProfile profile) => RunAsync(async () =>
    {
        await Api.UpdateProfileAsync(profile.Id, profile.Name, profile.PackingNotes, profile.MedicalNotes, CancellationToken.None);
        await RefreshTripsAsync();
        await RefreshTripDetailsAsync();
        State.Feedback = "Viajero actualizado.";
    });

    private Task ArchiveTravellerAsync(Guid id) => RunAsync(async () =>
    {
        await Api.ArchiveProfileAsync(id, CancellationToken.None);
        await RefreshTripsAsync();
        await RefreshTripDetailsAsync();
        State.Feedback = "Viajero archivado. Sus maletas anteriores se conservan.";
    });
}
