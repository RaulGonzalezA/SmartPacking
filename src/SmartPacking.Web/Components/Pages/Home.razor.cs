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

    [Inject]
    private HomeDataCoordinator DataCoordinator { get; set; } = default!;

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
    // Support can access the administration area when Auth0 grants at least one
    // administrative permission. The individual panels still check their own
    // permission before rendering or invoking an endpoint.
    private bool HasAdminAccess => roles.Contains("Admin") || permissions.Overlaps(AdminPermissions);
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
            if (principal.Identity?.IsAuthenticated is true)
            {
                foreach (var permission in principal.FindAll("permissions").Concat(principal.FindAll("permission")).Concat(principal.FindAll("https://smartpacking.app/permissions")).SelectMany(claim => Auth0ClaimValues.Deserialize(claim.Value)).SelectMany(permission => permission == "admin:*" ? AdminPermissions : [permission])) permissions.Add(permission);
                foreach (var role in principal.FindAll(System.Security.Claims.ClaimTypes.Role).Concat(principal.FindAll("roles")).Concat(principal.FindAll("https://smartpacking.app/roles")).SelectMany(claim => Auth0ClaimValues.Deserialize(claim.Value))) roles.Add(role);
                email = principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                    ?? principal.FindFirst("email")?.Value;
                var emailVerifiedValue = principal.FindFirst("email_verified")?.Value
                    ?? principal.FindFirst("https://smartpacking.app/email_verified")?.Value;
                emailVerified = bool.TryParse(emailVerifiedValue, out var isEmailVerified) && isEmailVerified;
            }
        }

        authenticationResolved = true;
        if (!emailVerified)
        {
            return;
        }

        currentUser = await Api.GetCurrentUserAsync(LoadCancellationToken);
        await RefreshAiUsageAsync();
        if (currentUser.IsOnboarded)
        {
            await LoadInitialAsync();
        }
    }

    private bool IsSelectedTripCompleted => State.Trips.SingleOrDefault(trip => trip.Id == State.SelectedTripId)?.GetStatus(DateOnly.FromDateTime(DateTime.Today)) == TripStatus.Completed;

    private string WeatherUnavailableMessage
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(State.WeatherFeedback))
            {
                return State.WeatherFeedback;
            }

            var trip = State.Trips.SingleOrDefault(candidate => candidate.Id == State.SelectedTripId);
            if (trip is null)
            {
                return "No hay previsión disponible para estas fechas.";
            }

            var firstForecastDate = DateOnly.FromDateTime(DateTime.Today).AddDays(15);
            return trip.StartDate > firstForecastDate
                ? $"Es muy pronto para una previsión fiable. Podrás consultar la previsión detallada a partir del {trip.StartDate.AddDays(-15).ToString("d", CultureInfo.CurrentCulture)}."
                : "No se ha podido obtener la previsión para este destino en este momento.";
        }
    }

    private CancellationToken LoadCancellationToken => loadCancellation?.Token ?? lifetimeCancellation.Token;

    private void BeginLoad()
    {
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
    }

    private Task LoadInitialAsync() => DataCoordinator.LoadInitialAsync(State, LoadCancellationToken);
    private Task RefreshTripsAsync() => DataCoordinator.RefreshTripsAsync(State, LoadCancellationToken);
    private Task RefreshTripDetailsAsync() => DataCoordinator.RefreshTripDetailsAsync(State, LoadCancellationToken);
    private Task RefreshWeatherAsync()
    {
        BeginLoad();
        return RunAsync(() => DataCoordinator.RefreshWeatherAsync(State, LoadCancellationToken), true);
    }
    private Task RefreshPackingAsync() => DataCoordinator.RefreshPackingAsync(State, LoadCancellationToken);
    private Task RefreshWardrobeAsync() => DataCoordinator.RefreshWardrobeAsync(State, LoadCancellationToken);
    private async Task RefreshAiUsageAsync() => aiUsage = await DataCoordinator.RefreshAiUsageAsync(LoadCancellationToken);

    private Task SelectTabAsync(string tab)
    {
        State.SelectTab(tab);

        switch (tab)
        {
            case "packing":
                BeginLoad();
                return RunAsync(RefreshPackingAsync, true);
            case "wardrobe":
                BeginLoad();
                return RunAsync(RefreshWardrobeAsync, true);
            default:
                return Task.CompletedTask;
        }
    }

    private async Task SelectTripAsync(Guid id)
    {
        BeginLoad();
        State.SelectTrip(id);
        State.Weather = null;
        State.WeatherFeedback = null;
        State.Plan = null;
        State.LuggageRules = null;
        State.Checklist = [];
        await RunAsync(RefreshTripDetailsAsync, true);
    }

    private Task SelectProfileAsync(Guid id)
    {
        BeginLoad();
        State.SelectProfile(id);
        return RunAsync(RefreshPackingAsync, true);
    }

    private async Task RunAsync(Func<Task> operation, bool isLoadOperation = false)
    {
        if (isLoadOperation) State.IsLoading = true; else State.IsSubmitting = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            var result = await ApiOperationResult.ExecuteAsync(operation);
            if (result.IsSuccess)
            {
                return;
            }

            if (result.Status == ApiOperationStatus.Unauthorized)
            {
                Navigation.NavigateTo("/login?error=session_expired", forceLoad: true);
                return;
            }

            State.Feedback = result.Message;
        }
        catch (OperationCanceledException) { /* Superseded load. */ }
        finally { if (isLoadOperation) State.IsLoading = false; else State.IsSubmitting = false; }
    }

    private Task CompleteOnboardingAsync(string name) => RunAsync(async () => { currentUser = await Api.CompleteOnboardingAsync(name, lifetimeCancellation.Token); State.Feedback = $"Perfil de {currentUser.Name} creado."; BeginLoad(); await LoadInitialAsync(); });
    private async Task<IReadOnlyDictionary<string, string[]>> UpdateCurrentUserAsync(UserProfile profile)
    {
        State.IsSubmitting = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            currentUser = await Api.UpdateCurrentUserAsync(profile.Name, profile.AddressDetails, lifetimeCancellation.Token);
            State.Feedback = "Perfil actualizado.";
            return new Dictionary<string, string[]>();
        }
        catch (Exception exception)
        {
            var result = ApiOperationResult.FromException(exception);
            if (result.Status == ApiOperationStatus.Unauthorized)
            {
                Navigation.NavigateTo("/login?error=session_expired", forceLoad: true);
            }

            return result.Errors.Count > 0
                ? result.Errors
                : new Dictionary<string, string[]> { ["form"] = [result.Message ?? "No se pudo actualizar el perfil."] };
        }
        finally
        {
            State.IsSubmitting = false;
        }
    }
    private Task DeleteCurrentUserAsync() => RunAsync(async () => { await Api.DeleteCurrentUserAsync("ELIMINAR", lifetimeCancellation.Token); Navigation.NavigateTo("/auth/logout", forceLoad: true); });
    public void Dispose() { loadCancellation?.Cancel(); loadCancellation?.Dispose(); lifetimeCancellation.Cancel(); lifetimeCancellation.Dispose(); }
}
