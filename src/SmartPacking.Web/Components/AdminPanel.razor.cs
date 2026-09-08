using System.Globalization;
using Microsoft.AspNetCore.Components;
using SmartPacking.Application;

namespace SmartPacking.Web.Components;

public partial class AdminPanel
{
    [Inject]
    private IWebSmartPackingClient Api { get; set; } = default!;

    private readonly AdminPanelViewModel state = new();
    private IReadOnlyList<AdminUserSummary>? users { get => state.Users; set => state.Users = value; }
    private IReadOnlyList<AdminAuditEntry>? audit { get => state.Audit; set => state.Audit = value; }
    private string? error { get => state.Error; set => state.Error = value; }
    private string? success { get => state.Success; set => state.Success = value; }
    private bool isLoading { get => state.IsLoading; set => state.IsLoading = value; }
    private string search { get => state.Search; set => state.Search = value; }
    private string statusFilter { get => state.StatusFilter; set => state.StatusFilter = value; }
    private string planFilter { get => state.PlanFilter; set => state.PlanFilter = value; }
    private DateTimeOffset? lastUpdated { get => state.LastUpdated; set => state.LastUpdated = value; }
    private Dictionary<Guid, string> selectedPlans => state.SelectedPlans;
    private Dictionary<Guid, int> creditAmounts => state.CreditAmounts;

    [Parameter] public bool IsActive { get; set; }
    [Parameter] public bool CanManageUsers { get; set; }
    [Parameter] public bool CanManagePlans { get; set; }
    [Parameter] public bool CanManageCredits { get; set; }
    [Parameter] public bool CanViewAudit { get; set; }
    [Parameter] public bool CanViewBilling { get; set; }

    private IReadOnlyList<AdminUserSummary> GetFilteredUsers() => state.GetFilteredUsers();

    protected override async Task OnParametersSetAsync()
    {
        if (IsActive && users is null && audit is null)
        {
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        isLoading = true;
        error = null;
        success = null;
        try
        {
            var dashboard = await AdminPanelCoordinator.LoadAsync(Api, CanManageUsers, CanViewAudit, CancellationToken.None);
            users = dashboard.Users;
            audit = dashboard.Audit;
            state.InitializeEditors();
            lastUpdated = DateTimeOffset.Now;
        }
        catch
        {
            error = "No se ha podido cargar una sección administrativa. Comprueba que tu token contiene el permiso correspondiente.";
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task SavePlanAsync(AdminUserSummary user)
    {
        var plan = state.SelectedPlans[user.Id];
        await SaveUserChangeAsync(user.Id, async () =>
        {
            await Api.SetAdminPlanAsync(user.Id, plan, CancellationToken.None);
            success = $"Plan de {DisplayName(user)} guardado.";
        });
    }

    private async Task AddCreditsAsync(Guid userId)
    {
        var credits = state.CreditAmounts[userId];
        await SaveUserChangeAsync(userId, async () =>
        {
            await Api.AddAdminCreditsAsync(userId, credits, CancellationToken.None);
            success = $"Se han añadido {credits} créditos.";
        });
    }

    private async Task SaveUserChangeAsync(Guid userId, Func<Task> action)
    {
        state.SavingUserIds.Add(userId);
        error = null;
        success = null;
        try
        {
            await action();
            var confirmation = success;
            await LoadAsync();
            success = confirmation;
        }
        catch (ApiProblemException exception)
        {
            error = exception.Message;
        }
        catch (HttpRequestException)
        {
            error = "No se ha podido guardar el cambio. Inténtalo de nuevo.";
        }
        finally
        {
            state.SavingUserIds.Remove(userId);
        }
    }

    private bool IsSaving(Guid userId) => state.SavingUserIds.Contains(userId);
    private bool CanSavePlan(AdminUserSummary user) => state.SelectedPlans.TryGetValue(user.Id, out var plan) && !plan.Equals(user.AiPlan, StringComparison.OrdinalIgnoreCase);
    private bool CanAddCredits(Guid userId) => state.CreditAmounts.TryGetValue(userId, out var credits) && credits is > 0 and <= 10_000;
    private static string DisplayName(AdminUserSummary user) => string.IsNullOrWhiteSpace(user.Name) ? "Cuenta sin perfil" : user.Name;
    private static string Initial(string name) => string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[0].ToString().ToUpperInvariant();
    private static string AuditActionLabel(string action) => action switch { "user.onboarded" => "Perfil completado", "user.profile_updated" => "Perfil actualizado", "family_profile.archived" => "Viajero archivado", "clothing.deleted" => "Prenda retirada", "trip.deleted" => "Viaje eliminado", "admin.plan_updated" => "Plan actualizado por administración", "admin.credits_added" => "Créditos añadidos por administración", _ => action.Replace('.', ' ') };
    private static string AuditDateLabel(DateTimeOffset occurredAt) => occurredAt.Year < 2000 ? "Histórico" : occurredAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
}

public sealed class AdminPanelViewModel
{
    public IReadOnlyList<AdminUserSummary>? Users { get; set; }
    public IReadOnlyList<AdminAuditEntry>? Audit { get; set; }
    public string? Error { get; set; }
    public string? Success { get; set; }
    public bool IsLoading { get; set; }
    public string Search { get; set; } = string.Empty;
    public string StatusFilter { get; set; } = "all";
    public string PlanFilter { get; set; } = "all";
    public DateTimeOffset? LastUpdated { get; set; }
    public Dictionary<Guid, string> SelectedPlans { get; } = [];
    public Dictionary<Guid, int> CreditAmounts { get; } = [];
    public HashSet<Guid> SavingUserIds { get; } = [];

    public IReadOnlyList<AdminUserSummary> GetFilteredUsers() => (Users ?? [])
        .Where(user => string.IsNullOrWhiteSpace(Search) || DisplayName(user).Contains(Search, StringComparison.OrdinalIgnoreCase))
        .Where(user => StatusFilter == "all" || (StatusFilter == "active" && user.IsOnboarded) || (StatusFilter == "pending" && !user.IsOnboarded))
        .Where(user => PlanFilter == "all" || user.AiPlan.Equals(PlanFilter, StringComparison.OrdinalIgnoreCase))
        .ToArray();

    public void InitializeEditors()
    {
        foreach (var user in Users ?? [])
        {
            SelectedPlans[user.Id] = user.AiPlan;
            CreditAmounts.TryAdd(user.Id, 10);
        }
    }

    private static string DisplayName(AdminUserSummary user) => string.IsNullOrWhiteSpace(user.Name) ? "Cuenta sin perfil" : user.Name;
}

public static class AdminPanelCoordinator
{
    public static async Task<AdminDashboardData> LoadAsync(IWebSmartPackingClient api, bool includeUsers, bool includeAudit, CancellationToken cancellationToken)
    {
        var users = includeUsers ? api.GetAdminUsersAsync(cancellationToken) : Task.FromResult<IReadOnlyList<AdminUserSummary>>([]);
        var audit = includeAudit ? api.GetAdminAuditAsync(cancellationToken) : Task.FromResult<IReadOnlyList<AdminAuditEntry>>([]);
        await Task.WhenAll(users, audit);
        return new(await users, await audit);
    }
}

public sealed record AdminDashboardData(IReadOnlyList<AdminUserSummary> Users, IReadOnlyList<AdminAuditEntry> Audit);
