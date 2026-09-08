using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using SmartPacking.Application;
using SmartPacking.Domain;

#pragma warning disable IDE0011, S3881, S8949, CA1816

namespace SmartPacking.Web.Components.Pages;

public partial class Home : ComponentBase, IDisposable
{
    private static readonly string[] AdminPermissions = ["admin:users", "admin:plans", "admin:credits", "admin:billing", "admin:audit"];
    [Inject]
    private IWebSmartPackingClient Api { get; set; } = default!;

    private readonly CancellationTokenSource lifetimeCancellation = new();
    private CancellationTokenSource? loadCancellation;
    private WardrobePanel? wardrobePanel;
    private UserProfile? currentUser;
    private GarmentRecognitionUsageResult? aiUsage;
    private bool authenticationResolved;
    private bool emailVerified = true;
    private string? email;
    private readonly HashSet<string> permissions = new(StringComparer.Ordinal);
    private readonly HashSet<string> roles = new(StringComparer.OrdinalIgnoreCase);
    private bool HasPermission(string permission) => permissions.Contains(permission);
    private bool HasAdminAccess => roles.Contains("Admin");
    private string? DefaultOrigin
    {
        get
        {
            var city = currentUser?.AddressDetails?.City;
            if (!string.IsNullOrWhiteSpace(city))
            {
                return city.Trim();
            }

            return null;
        }
    }

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    protected HomeViewModel State { get; } = new();

    protected override Task OnInitializedAsync()
    {
        BeginLoad();
        return RunAsync(InitializeAsync, true);
    }

    private async Task InitializeAsync()
    {
        if (AuthenticationStateTask is not null)
        {
            var principal = (await AuthenticationStateTask).User;
            foreach (var permission in principal.FindAll("permissions").Concat(principal.FindAll("permission")).Concat(principal.FindAll("https://smartpacking.app/permissions")).SelectMany(claim => Auth0ClaimValues.Deserialize(claim.Value)).SelectMany(permission => permission == "admin:*" ? AdminPermissions : [permission])) permissions.Add(permission);
            foreach (var role in principal.FindAll(System.Security.Claims.ClaimTypes.Role).Concat(principal.FindAll("roles")).Concat(principal.FindAll("https://smartpacking.app/roles")).SelectMany(claim => Auth0ClaimValues.Deserialize(claim.Value))) roles.Add(role);
            email = principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                ?? principal.FindFirst("email")?.Value;
            var emailVerifiedValue = principal.FindFirst("email_verified")?.Value
                ?? principal.FindFirst("https://smartpacking.app/email_verified")?.Value;
            emailVerified = bool.TryParse(emailVerifiedValue, out var isEmailVerified) && isEmailVerified;
        }

        authenticationResolved = true;
        if (!emailVerified)
        {
            return;
        }

        currentUser = await Api.GetCurrentUserAsync(LoadCancellationToken);
        aiUsage = await Api.GetAiUsageAsync(LoadCancellationToken);
        if (currentUser.IsOnboarded)
        {
            await LoadAsync();
        }
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
            ? State.Plan?.Plan.Items.Select(item => item.Recommendation.Item.Id).ToHashSet() ?? []
            : usage.Select(item => item.ClothingItemId).ToHashSet();
        State.UsedItemIds = usage.Where(item => item.WasUsed).Select(item => item.ClothingItemId).ToHashSet();
        var profileDetails = await Task.WhenAll(State.TripProfiles.Select(async profile =>
        {
            var plan = await Api.GetProfilePackingListAsync(State.SelectedTripId, profile.Id, LoadCancellationToken);
            var checklist = await Api.GetChecklistAsync(State.SelectedTripId, profile.Id, LoadCancellationToken);
            return (Plan: plan, Progress: new PreparationProgressItem(profile.Name, plan?.Plan.Items.Count(item => item.IsPacked) ?? 0, plan?.Plan.Items.Count ?? 0, checklist.Count(item => item.IsPacked), checklist.Count));
        }));
        State.FamilyPlans = profileDetails.Where(item => item.Plan is not null).Select(item => item.Plan!).ToArray();
        State.PreparationProgress = profileDetails.Select(item => item.Progress).ToArray();
        RefreshPackingInsights();
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
        RefreshPackingInsights();
    }

    private void RefreshPackingInsights() => State.PackingInsights = PackingInsightsService.Analyze(State.Plan, State.FamilyPlans, State.Wardrobe, State.Weather);

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
        catch (ApiProblemException exception) when (exception.StatusCode == StatusCodes.Status401Unauthorized)
        {
            Navigation.NavigateTo("/login?error=session_expired", forceLoad: true);
        }
        catch (ApiProblemException exception) when (exception.StatusCode == StatusCodes.Status403Forbidden) { State.Feedback = "No tienes permisos para realizar esta acción."; }
        catch (ApiProblemException exception) { State.Feedback = exception.Message; }
        catch (HttpRequestException) { State.Feedback = "No se ha podido conectar con el servicio. Inténtalo de nuevo."; }
        catch (Exception) { State.Feedback = "Ha ocurrido un error inesperado. Inténtalo de nuevo."; }
        finally { if (isLoadOperation) State.IsLoading = false; else State.IsSubmitting = false; }
    }

    private Task CreateTripAsync(TripFormInput input) => RunAsync(async () => { var created = await Api.CreateTripAsync(input.ToTrip(Guid.NewGuid()), CancellationToken.None); State.SelectTrip(created.Id); State.Feedback = "Viaje creado. Ya puedes completar sus detalles."; await LoadAsync(); });
    private Task CompleteOnboardingAsync(string name) => RunAsync(async () => { currentUser = await Api.CompleteOnboardingAsync(name, lifetimeCancellation.Token); State.Feedback = $"Perfil de {currentUser.Name} creado."; BeginLoad(); await LoadAsync(); });
    private async Task<IReadOnlyDictionary<string, string[]>> UpdateCurrentUserAsync(UserProfile profile)
    {
        State.IsSubmitting = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            currentUser = await Api.UpdateCurrentUserAsync(profile.Name, profile.AddressDetails, lifetimeCancellation.Token);
            State.Feedback = "Perfil actualizado.";
            await LoadAsync();
            return new Dictionary<string, string[]>();
        }
        catch (ApiProblemException exception) when (exception.StatusCode == StatusCodes.Status401Unauthorized)
        {
            Navigation.NavigateTo("/login?error=session_expired", forceLoad: true);
            return new Dictionary<string, string[]> { ["form"] = ["Tu sesión ha caducado. Inicia sesión de nuevo."] };
        }
        catch (ApiProblemException exception) when (exception.Errors.Count > 0)
        {
            return exception.Errors;
        }
        catch (ApiProblemException exception)
        {
            return new Dictionary<string, string[]> { ["form"] = [exception.Message] };
        }
        catch (HttpRequestException)
        {
            return new Dictionary<string, string[]> { ["form"] = ["No se ha podido conectar con el servicio. Inténtalo de nuevo."] };
        }
        finally
        {
            State.IsSubmitting = false;
        }
    }
    private Task DeleteCurrentUserAsync() => RunAsync(async () => { await Api.DeleteCurrentUserAsync("ELIMINAR", lifetimeCancellation.Token); Navigation.NavigateTo("/auth/logout", forceLoad: true); });
    private Task SaveTripAsync(Trip trip) => RunAsync(async () => { await Api.UpdateTripAsync(trip, CancellationToken.None); State.Feedback = "Viaje actualizado."; await LoadAsync(); });
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
        State.Trips = await Api.GetTripsAsync(LoadCancellationToken);
        State.Feedback = "Viaje eliminado. Selecciona otro viaje para continuar.";
    });
    private Task AddTravellerAsync(TravellerInput input) => RunAsync(async () => { if (string.IsNullOrWhiteSpace(input.Name)) { State.Feedback = "Escribe el nombre del viajero."; return; } var profile = await Api.CreateProfileAsync(input.Name.Trim(), input.PackingNotes, input.MedicalNotes, CancellationToken.None); await Api.SetTripProfilesAsync(State.SelectedTripId, State.TripProfiles.Select(item => item.Id).Append(profile.Id).ToArray(), CancellationToken.None); State.Feedback = $"{profile.Name} se ha añadido como viajero."; await LoadAsync(); });
    private Task SaveTravellersAsync(IReadOnlyCollection<Guid> ids) => RunAsync(async () => { await Api.SetTripProfilesAsync(State.SelectedTripId, ids, CancellationToken.None); State.Feedback = "Viajeros guardados."; await LoadTripAsync(); });
    private Task SaveTravellerAsync(FamilyProfile profile) => RunAsync(async () => { await Api.UpdateProfileAsync(profile.Id, profile.Name, profile.PackingNotes, profile.MedicalNotes, CancellationToken.None); State.Feedback = "Viajero actualizado."; await LoadAsync(); });
    private Task ArchiveTravellerAsync(Guid id) => RunAsync(async () => { await Api.ArchiveProfileAsync(id, CancellationToken.None); State.Feedback = "Viajero archivado. Sus maletas anteriores se conservan."; await LoadAsync(); });
    private Task CreateClothingAsync(string name, string color, Guid ownerId, ClothingType type, Season season, Style style, string? material, int weightGrams, IBrowserFile? file) => RunAsync(async () =>
    {
        var item = await Api.CreateClothingAsync(new ClothingItem(Guid.NewGuid(), name, type, season, color, 2, false, style, weightGrams, true, true, 70, [], false, ownerId, null, material), CancellationToken.None);
        if (file is not null)
        {
            await UploadClothingPhotoAsync(item.Id, file);
        }

        State.Feedback = "Prenda guardada.";
        await LoadAsync();
    });
    private async Task UploadClothingPhotoAsync(Guid id, IBrowserFile file) { await using var content = file.OpenReadStream(5 * 1024 * 1024); var url = await Api.UploadClothingPhotoAsync(id, content, file.ContentType, file.Name, CancellationToken.None); wardrobePanel?.SetPhotoUrl(id, url); State.Feedback = "Foto de la prenda actualizada."; }
    private async Task<GarmentRecognitionSuggestion> RecognizeGarmentAsync(IBrowserFile file)
    {
        await using var content = file.OpenReadStream(5 * 1024 * 1024);
        var suggestion = await Api.RecognizeGarmentAsync(content, file.ContentType, file.Name, lifetimeCancellation.Token);
        aiUsage = await Api.GetAiUsageAsync(lifetimeCancellation.Token);
        return suggestion;
    }
    private async Task UpdateStatusAsync(ClothingItem item, bool clean, bool available) { await Api.UpdateClothingStatusAsync(item.Id, clean, available, CancellationToken.None); await LoadAsync(); }
    private async Task DeleteClothingAsync(Guid id) { await Api.DeleteClothingAsync(id, CancellationToken.None); await LoadAsync(); }
    private async Task RestoreClothingAsync(Guid id) { await Api.RestoreClothingAsync(id, CancellationToken.None); await LoadAsync(); }
    private Task SetPackedAsync((PlannedItem Item, bool IsPacked) input) => RunAsync(async () => { if (State.Plan is not null) { await Api.SetProfilePackedAsync(State.Plan.Plan.PackingListId, input.Item.Recommendation.Item.Id, input.IsPacked, CancellationToken.None); await LoadPlanAsync(); } });
    private Task AddManualClothingAsync(Guid id) => RunAsync(async () => { if (State.Plan is null) { State.Feedback = "Selecciona una prenda para añadirla."; return; } await Api.AddProfilePackingListItemAsync(State.Plan.Plan.PackingListId, id, CancellationToken.None); await LoadPlanAsync(); });
    private Task AddToiletryAsync((string Name, ChecklistCategory Category) input) => RunAsync(async () => { if (State.SelectedTripId == Guid.Empty || State.SelectedProfileId == Guid.Empty || string.IsNullOrWhiteSpace(input.Name)) { return; } await Api.AddProfileChecklistItemAsync(State.SelectedTripId, State.SelectedProfileId, input.Category, input.Name.Trim(), CancellationToken.None); await LoadPlanAsync(); });
    private Task SetChecklistPackedAsync((ChecklistItem Item, bool IsPacked) input) => RunAsync(async () => { await Api.SetChecklistPackedAsync(input.Item.Id, input.IsPacked, CancellationToken.None); await LoadTripAsync(); });
    private Task SaveUsageAsync(IReadOnlyCollection<Guid> usedIds) => RunAsync(async () => { await Api.SaveUsageAsync(State.SelectedTripId, State.UsageItemIds.Select(id => new ClothingUsage(State.SelectedTripId, id, usedIds.Contains(id))).ToArray(), CancellationToken.None); State.Feedback = "Uso real guardado."; });
    public void Dispose() { loadCancellation?.Cancel(); loadCancellation?.Dispose(); lifetimeCancellation.Cancel(); lifetimeCancellation.Dispose(); }
}
