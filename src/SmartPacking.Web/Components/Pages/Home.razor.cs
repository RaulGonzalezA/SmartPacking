using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using SmartPacking.Application;
using SmartPacking.Domain;

#pragma warning disable IDE0011, S3881, S8949, CA1816

namespace SmartPacking.Web.Components.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Inject]
    private IWebSmartPackingClient Api { get; set; } = default!;

    private readonly CancellationTokenSource lifetimeCancellation = new();
    private CancellationTokenSource? loadCancellation;
    private WardrobePanel? wardrobePanel;

    protected HomeViewModel State { get; } = new();

    protected override Task OnInitializedAsync()
    {
        BeginLoad();
        return RunAsync(LoadAsync, true);
    }

    private bool IsSelectedTripCompleted => State.Trips.SingleOrDefault(trip => trip.Id == State.SelectedTripId)?.GetStatus(DateOnly.FromDateTime(DateTime.Today)) == TripStatus.Completed;

    private string WeatherUnavailableMessage
    {
        get
        {
            var trip = State.Trips.SingleOrDefault(candidate => candidate.Id == State.SelectedTripId);
            if (trip is null || trip.StartDate <= DateOnly.FromDateTime(DateTime.Today).AddDays(16))
            {
                return "No hay previsión disponible para estas fechas.";
            }

            return $"Es muy pronto para una previsión fiable. Podrás consultar la previsión detallada a partir del {trip.StartDate.AddDays(-15).ToString("d", CultureInfo.CurrentCulture)}.";
        }
    }

    private CancellationToken LoadCancellationToken => loadCancellation?.Token ?? lifetimeCancellation.Token;

    private void BeginLoad()
    {
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
    }

    private async Task LoadAsync()
    {
        State.Trips = await Api.GetTripsAsync(LoadCancellationToken);
        State.Profiles = await Api.GetProfilesAsync(LoadCancellationToken);
        State.Templates = await Api.GetTripTemplatesAsync(LoadCancellationToken);
        State.Wardrobe = await Api.GetWardrobeAsync(false, LoadCancellationToken);
        State.DeletedWardrobe = await Api.GetWardrobeAsync(true, LoadCancellationToken);
        State.SelectInitialTrip();
        await LoadTripAsync();
    }

    private async Task LoadTripAsync()
    {
        if (State.SelectedTripId == Guid.Empty)
        {
            State.ClearSelectedTripData();
            return;
        }

        State.TripProfiles = await Api.GetTripProfilesAsync(State.SelectedTripId, LoadCancellationToken);
        State.EnsureSelectedProfile();
        await LoadPlanAsync();
        State.Weather = await Api.GetWeatherAsync(State.SelectedTripId, LoadCancellationToken);
        var usage = await Api.GetUsageAsync(State.SelectedTripId, LoadCancellationToken);
        State.UsageItemIds = usage.Count == 0
            ? State.Plan?.Plan.Items.Select(item => item.Recommendation.Item.Id).ToHashSet() ?? new HashSet<Guid>()
            : usage.Select(item => item.ClothingItemId).ToHashSet();
        State.UsedItemIds = usage.Where(item => item.WasUsed).Select(item => item.ClothingItemId).ToHashSet();
        State.PreparationProgress = await Task.WhenAll(State.TripProfiles.Select(async profile =>
        {
            var plan = await Api.GetProfilePackingListAsync(State.SelectedTripId, profile.Id, LoadCancellationToken);
            var checklist = await Api.GetChecklistAsync(State.SelectedTripId, profile.Id, LoadCancellationToken);
            return new PreparationProgressItem(profile.Name, plan?.Plan.Items.Count(item => item.IsPacked) ?? 0, plan?.Plan.Items.Count ?? 0, checklist.Count(item => item.IsPacked), checklist.Count);
        }));
    }

    private async Task LoadPlanAsync()
    {
        if (State.SelectedTripId == Guid.Empty || State.SelectedProfileId == Guid.Empty)
        {
            return;
        }

        State.Plan = await Api.GetProfilePackingListAsync(State.SelectedTripId, State.SelectedProfileId, LoadCancellationToken);
        State.LuggageRules = await Api.GetLuggageRulesAsync(State.SelectedTripId, State.SelectedProfileId, LoadCancellationToken);
        State.Checklist = await Api.GetChecklistAsync(State.SelectedTripId, State.SelectedProfileId, LoadCancellationToken);
    }

    private async Task SelectTripAsync(Guid id)
    {
        BeginLoad();
        State.SelectTrip(id);
        State.Weather = null;
        State.Plan = null;
        State.LuggageRules = null;
        State.Checklist = [];
        await RunAsync(LoadTripAsync, true);
    }

    private Task SelectProfileAsync(Guid id)
    {
        BeginLoad();
        State.SelectProfile(id);
        return RunAsync(LoadPlanAsync, true);
    }

    private async Task RunAsync(Func<Task> operation, bool isLoadOperation = false)
    {
        if (isLoadOperation) State.IsLoading = true; else State.IsSubmitting = true;
        await InvokeAsync(StateHasChanged);
        try { await operation(); }
        catch (OperationCanceledException) { /* Superseded load. */ }
        catch (ApiProblemException exception) { State.Feedback = exception.Message; }
        catch (HttpRequestException) { State.Feedback = "No se ha podido conectar con el servicio. Inténtalo de nuevo."; }
        catch (Exception) { State.Feedback = "Ha ocurrido un error inesperado. Inténtalo de nuevo."; }
        finally { if (isLoadOperation) State.IsLoading = false; else State.IsSubmitting = false; }
    }

    private Task CreateTripAsync(TripFormInput input) => RunAsync(async () => { await Api.CreateTripAsync(input.Destination, input.StartDate, input.EndDate, input.MinimumTemperatureCelsius, input.MaximumTemperatureCelsius, input.TemplateKey, input.LuggageAllowanceGrams, input.CabinOnly, CancellationToken.None); State.Feedback = "Viaje creado."; await LoadAsync(); });
    private Task SaveTripAsync(Trip trip) => RunAsync(async () => { await Api.UpdateTripAsync(trip, CancellationToken.None); State.Feedback = "Viaje actualizado."; await LoadAsync(); });
    private Task DeleteTripAsync() => RunAsync(async () => { await Api.DeleteTripAsync(State.SelectedTripId, CancellationToken.None); State.SelectTrip(Guid.Empty); State.Feedback = "Viaje eliminado."; await LoadAsync(); });
    private Task AddTravellerAsync(TravellerInput input) => RunAsync(async () => { if (string.IsNullOrWhiteSpace(input.Name)) { State.Feedback = "Escribe el nombre del viajero."; return; } var profile = await Api.CreateProfileAsync(input.Name.Trim(), input.PackingNotes, input.MedicalNotes, CancellationToken.None); await Api.SetTripProfilesAsync(State.SelectedTripId, State.TripProfiles.Select(item => item.Id).Append(profile.Id).ToArray(), CancellationToken.None); State.Feedback = $"{profile.Name} se ha añadido como viajero."; await LoadAsync(); });
    private Task SaveTravellersAsync(IReadOnlyCollection<Guid> ids) => RunAsync(async () => { await Api.SetTripProfilesAsync(State.SelectedTripId, ids, CancellationToken.None); State.Feedback = "Viajeros guardados."; await LoadTripAsync(); });
    private Task SaveTravellerAsync(FamilyProfile profile) => RunAsync(async () => { await Api.UpdateProfileAsync(profile.Id, profile.Name, profile.PackingNotes, profile.MedicalNotes, CancellationToken.None); State.Feedback = "Viajero actualizado."; await LoadAsync(); });
    private Task ArchiveTravellerAsync(Guid id) => RunAsync(async () => { await Api.ArchiveProfileAsync(id, CancellationToken.None); State.Feedback = "Viajero archivado. Sus maletas anteriores se conservan."; await LoadAsync(); });
    private Task CreateClothingAsync(string name, string color, Guid ownerId, ClothingType type, int weightGrams) => RunAsync(async () => { await Api.CreateClothingAsync(new ClothingItem(Guid.NewGuid(), name, type, Season.AllYear, color, 2, false, Style.Casual, weightGrams, true, true, 70, [], false, ownerId), CancellationToken.None); State.Feedback = "Prenda guardada."; await LoadAsync(); });
    private async Task UploadClothingPhotoAsync(Guid id, IBrowserFile file) { await using var content = file.OpenReadStream(5 * 1024 * 1024); var url = await Api.UploadClothingPhotoAsync(id, content, file.ContentType, file.Name, CancellationToken.None); wardrobePanel?.SetPhotoUrl(id, url); State.Feedback = "Foto de la prenda actualizada."; }
    private async Task UpdateStatusAsync(ClothingItem item, object? clean, object? available) { await Api.UpdateClothingStatusAsync(item.Id, clean is bool c && c, available is bool a && a, CancellationToken.None); await LoadAsync(); }
    private async Task DeleteClothingAsync(Guid id) { await Api.DeleteClothingAsync(id, CancellationToken.None); await LoadAsync(); }
    private async Task RestoreClothingAsync(Guid id) { await Api.RestoreClothingAsync(id, CancellationToken.None); await LoadAsync(); }
    private Task SetPackedAsync((PlannedItem Item, bool IsPacked) input) => RunAsync(async () => { if (State.Plan is not null) { await Api.SetProfilePackedAsync(State.Plan.Plan.PackingListId, input.Item.Recommendation.Item.Id, input.IsPacked, CancellationToken.None); await LoadPlanAsync(); } });
    private Task AddManualClothingAsync(Guid id) => RunAsync(async () => { if (State.Plan is null) { State.Feedback = "Selecciona una prenda para añadirla."; return; } await Api.AddProfilePackingListItemAsync(State.Plan.Plan.PackingListId, id, CancellationToken.None); await LoadPlanAsync(); });
    private Task SetChecklistPackedAsync((ChecklistItem Item, bool IsPacked) input) => RunAsync(async () => { await Api.SetChecklistPackedAsync(input.Item.Id, input.IsPacked, CancellationToken.None); await LoadTripAsync(); });
    private Task SaveUsageAsync(IReadOnlyCollection<Guid> usedIds) => RunAsync(async () => { await Api.SaveUsageAsync(State.SelectedTripId, State.UsageItemIds.Select(id => new ClothingUsage(State.SelectedTripId, id, usedIds.Contains(id))).ToArray(), CancellationToken.None); State.Feedback = "Uso real guardado."; });
    public void Dispose() { loadCancellation?.Cancel(); loadCancellation?.Dispose(); lifetimeCancellation.Cancel(); lifetimeCancellation.Dispose(); }
}
