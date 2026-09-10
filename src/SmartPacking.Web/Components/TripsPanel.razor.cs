using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.ComponentModel.DataAnnotations;
using SmartPacking.Application;
using SmartPacking.Domain;

#pragma warning disable IDE0011, S2681, S1144

namespace SmartPacking.Web.Components;

public partial class TripsPanel
{
    private static readonly Guid DefaultProfileId = Guid.Parse("90ae4435-5a54-42dc-a0a4-4f8aa4d96f90");
    private TripFormInput newTrip = new();
    private readonly TripFormInput editTrip = new();
    private readonly HashSet<Guid> tripProfileIds = [];
    private readonly HashSet<Guid> usedItemIds = [];
    private bool showTripForm;
    private bool editCreatedTrip;
    private bool showTravellerForm;
    private bool confirmTripDeletion;
    private string? lastDefaultOrigin;
    private Guid? confirmTravellerArchiveId;
    private Trip? editingTrip;
    private Guid synchronizedTripId;
    private IReadOnlySet<Guid>? synchronizedUsageItemIds;
    private readonly TravellerFormInput travellerInput = new();
    private readonly Dictionary<string, string[]> travellerFieldErrors = new(StringComparer.Ordinal);
    private FamilyProfile? editingTraveller;

    [Parameter] public bool IsActive { get; set; }
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public string? Feedback { get; set; }
    [Parameter] public IReadOnlyList<Trip> Trips { get; set; } = [];
    [Parameter] public IReadOnlyList<FamilyProfile> Profiles { get; set; } = [];
    [Parameter] public IReadOnlyList<TripTemplate> Templates { get; set; } = [];
    [Parameter] public IReadOnlyList<FamilyProfile> TripProfiles { get; set; } = [];
    [Parameter] public IReadOnlyList<ClothingItem> Wardrobe { get; set; } = [];
    [Parameter] public string? DefaultOrigin { get; set; }
    [Parameter] public Guid SelectedTripId { get; set; }
    [Parameter] public TripWeatherForecast? Weather { get; set; }
    [Parameter] public IReadOnlySet<Guid> UsageItemIds { get; set; } = new HashSet<Guid>();
    [Parameter] public IReadOnlySet<Guid> UsedItemIds { get; set; } = new HashSet<Guid>();
    [Parameter] public bool IsCompleted { get; set; }
    [Parameter] public string WeatherUnavailableMessage { get; set; } = string.Empty;
    [Parameter] public EventCallback WeatherRefreshRequested { get; set; }
    [Parameter] public EventCallback<Guid> SelectedTripChanged { get; set; }
    [Parameter] public EventCallback<TripFormInput> Created { get; set; }
    [Parameter] public EventCallback<Trip> Updated { get; set; }
    [Parameter] public EventCallback Deleted { get; set; }
    [Parameter] public EventCallback<IReadOnlyCollection<Guid>> TravellersSaved { get; set; }
    [Parameter] public EventCallback<TravellerInput> TravellerAdded { get; set; }
    [Parameter] public EventCallback<FamilyProfile> TravellerUpdated { get; set; }
    [Parameter] public EventCallback<Guid> Archived { get; set; }
    [Parameter] public EventCallback<IReadOnlyCollection<Guid>> UsageSaved { get; set; }

    protected override void OnParametersSet()
    {
        if (string.IsNullOrWhiteSpace(newTrip.Origin) || string.Equals(newTrip.Origin, lastDefaultOrigin, StringComparison.Ordinal))
        {
            newTrip.Origin = DefaultOrigin;
        }
        lastDefaultOrigin = DefaultOrigin;
        if (synchronizedTripId != SelectedTripId) { synchronizedTripId = SelectedTripId; tripProfileIds.Clear(); tripProfileIds.UnionWith(TripProfiles.Select(profile => profile.Id)); }
        if (!ReferenceEquals(synchronizedUsageItemIds, UsedItemIds)) { synchronizedUsageItemIds = UsedItemIds; usedItemIds.Clear(); usedItemIds.UnionWith(UsedItemIds); }
        if (editCreatedTrip && SelectedTripId != Guid.Empty && Trips.Any(trip => trip.Id == SelectedTripId))
        {
            editCreatedTrip = false;
            StartEditingTrip();
        }
    }
    private Task SelectTrip(ChangeEventArgs args) => SelectedTripChanged.InvokeAsync(Guid.TryParse(args.Value?.ToString(), out var id) ? id : Guid.Empty);
    private void ApplyTemplate()
    {
        var template = Templates.SingleOrDefault(candidate => candidate.Key == newTrip.TemplateKey);
        if (template is null) return;

        newTrip.MinimumTemperatureCelsius = template.DefaultMinimumTemperatureCelsius;
        newTrip.MaximumTemperatureCelsius = template.DefaultMaximumTemperatureCelsius;
        newTrip.LuggageAllowanceGrams = template.DefaultLuggageAllowanceGrams;
        newTrip.CabinOnly = template.CabinOnly;

        var primaryLuggage = newTrip.Luggages[0];
        primaryLuggage.Type = template.CabinOnly ? LuggageType.Cabin : LuggageType.Checked;
        primaryLuggage.ApplyDefaults();
        primaryLuggage.AllowanceGrams = template.DefaultLuggageAllowanceGrams;
    }
    private async Task CreateTrip()
    {
        await Created.InvokeAsync(newTrip);
        showTripForm = false;
        editCreatedTrip = true;
    }
    private void CancelNewTrip()
    {
        showTripForm = false;
        newTrip = new TripFormInput { Origin = DefaultOrigin };
    }
    private void StartEditingTrip() { editingTrip = Trips.SingleOrDefault(trip => trip.Id == SelectedTripId); if (editingTrip is not null) editTrip.CopyFrom(editingTrip); }
    private void CancelEditingTrip() => editingTrip = null;
    private async Task SaveTrip()
    {
        if (editingTrip is null)
        {
            return;
        }

        await Updated.InvokeAsync(editTrip.ToTrip(editingTrip.Id));
        CancelEditingTrip();
    }
    private void SetTraveller(Guid id, bool selected) { if (selected) tripProfileIds.Add(id); else tripProfileIds.Remove(id); }
    private Task SaveTravellers() => TravellersSaved.InvokeAsync(tripProfileIds);
    private async Task AddTraveller() { if (!string.IsNullOrWhiteSpace(travellerInput.Name)) { await TravellerAdded.InvokeAsync(new(travellerInput.Name, travellerInput.PackingNotes, travellerInput.MedicalNotes)); travellerInput.Clear(); } }
    private void OpenTravellerForm() { travellerInput.Clear(); showTravellerForm = true; }
    private async Task SaveTravellerDialog()
    {
        travellerFieldErrors.Clear();
        if (editingTraveller is null)
        {
            await AddTraveller();
            showTravellerForm = false;
            return;
        }

        await SaveTraveller();
    }
    private void StartEditingTraveller(FamilyProfile profile) { editingTraveller = profile; travellerInput.Name = profile.Name; travellerInput.PackingNotes = profile.PackingNotes ?? string.Empty; travellerInput.MedicalNotes = profile.MedicalNotes ?? string.Empty; }
    private void CancelEditingTraveller() { editingTraveller = null; travellerInput.Clear(); }
    private void CancelTravellerDialog() { showTravellerForm = false; CancelEditingTraveller(); }
    private void CaptureTravellerValidationErrors(EditContext context)
    {
        travellerFieldErrors.Clear();
        foreach (var field in new[] { nameof(TravellerFormInput.Name), nameof(TravellerFormInput.PackingNotes), nameof(TravellerFormInput.MedicalNotes) })
        {
            var messages = context.GetValidationMessages(new FieldIdentifier(travellerInput, field)).ToArray();
            if (messages.Length > 0) { travellerFieldErrors[field] = messages; }
        }
    }
    private async Task SaveTraveller()
    {
        if (editingTraveller is null || string.IsNullOrWhiteSpace(travellerInput.Name))
        {
            return;
        }

        await TravellerUpdated.InvokeAsync(editingTraveller with { Name = travellerInput.Name.Trim(), PackingNotes = travellerInput.PackingNotes, MedicalNotes = travellerInput.MedicalNotes });
        CancelEditingTraveller();
    }
    private void SetUsage(Guid id, bool used) { if (used) usedItemIds.Add(id); else usedItemIds.Remove(id); }
    private Task SaveUsage() => UsageSaved.InvokeAsync(usedItemIds);
    private void RequestTripDeletion() => confirmTripDeletion = true;
    private async Task ConfirmTripDeletionAsync() { confirmTripDeletion = false; await Deleted.InvokeAsync(); }
    private void RequestTravellerArchive(Guid id) => confirmTravellerArchiveId = id;
    private async Task ConfirmTravellerArchiveAsync() { if (confirmTravellerArchiveId is Guid id) { confirmTravellerArchiveId = null; await Archived.InvokeAsync(id); } }
    private void CancelConfirmation() { confirmTripDeletion = false; confirmTravellerArchiveId = null; }
    private static string LuggageTypeName(LuggageType type) => type switch { LuggageType.Backpack => "Mochila", LuggageType.Cabin => "Cabina", _ => "Facturada" };
    private static string TransportSummary(Trip trip) => trip.TransportTypesOrEmpty.Count == 0 ? "🚗 Transporte por decidir" : string.Join(" · ", trip.TransportTypesOrEmpty.Select(type => type switch { TransportType.Car => "🚗 Coche", TransportType.Plane => "✈️ Avión", TransportType.Train => "🚆 Tren", TransportType.Bus => "🚌 Autobús", _ => "🛳️ Barco" }));
    private static string TransportIcon(TransportType type) => type switch { TransportType.Car => "🚗", TransportType.Plane => "✈️", TransportType.Train => "🚆", TransportType.Bus => "🚌", _ => "🛳️" };
    private static string ActivityName(TripActivity activity) => activity switch { TripActivity.Sightseeing => "Turismo", TripActivity.Beach => "Playa", TripActivity.Hiking => "Senderismo", TripActivity.Business => "Negocios", TripActivity.FormalEvent => "Evento formal", TripActivity.Sport => "Deporte", TripActivity.Nightlife => "Ocio nocturno", _ => "Relax" };

    private sealed class TravellerFormInput
    {
        [Required(ErrorMessage = "Indica el nombre del viajero.")]
        [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
        public string Name { get; set; } = string.Empty;
        [StringLength(500, ErrorMessage = "Las notas de equipaje no pueden superar los 500 caracteres.")]
        public string PackingNotes { get; set; } = string.Empty;
        [StringLength(500, ErrorMessage = "Las notas de salud no pueden superar los 500 caracteres.")]
        public string MedicalNotes { get; set; } = string.Empty;
        public void Clear() { Name = string.Empty; PackingNotes = string.Empty; MedicalNotes = string.Empty; }
    }
}
