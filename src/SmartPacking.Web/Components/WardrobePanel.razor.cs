using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using SmartPacking.Application;
using SmartPacking.Domain;

namespace SmartPacking.Web.Components;

#pragma warning disable S3358
public partial class WardrobePanel
{
    private readonly Input input = new();
    private readonly Dictionary<Guid, string> photoUrls = [];
    private IBrowserFile? newPhoto;
    private string? newPhotoPreview;
    private string? recognitionError;
    private bool isAnalyzing;
    private Guid? pendingRemovalId;
    private bool showForm;
    private string search = string.Empty;
    private ClothingType? selectedType;
    private ClothingItem? selectedItem;

    [Parameter] public bool IsActive { get; set; }
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public GarmentRecognitionUsageResult? AiUsage { get; set; }
    [Parameter, EditorRequired] public IReadOnlyList<ClothingItem> Items { get; set; } = [];
    [Parameter, EditorRequired] public IReadOnlyList<ClothingItem> DeletedItems { get; set; } = [];
    [Parameter, EditorRequired] public IReadOnlyList<FamilyProfile> Profiles { get; set; } = [];
    [Parameter, EditorRequired] public EventCallback<(string Name, string Color, Guid OwnerId, ClothingType Type, Season Season, Style Style, string? Material, int WeightGrams, IBrowserFile? File)> Created { get; set; }
    [Parameter] public Func<IBrowserFile, Task<GarmentRecognitionSuggestion>>? RecognitionRequested { get; set; }
    [Parameter, EditorRequired] public EventCallback<(ClothingItem Item, bool Clean, bool Available)> Changed { get; set; }
    [Parameter, EditorRequired] public EventCallback<Guid> Removed { get; set; }
    [Parameter, EditorRequired] public EventCallback<Guid> Restored { get; set; }
    [Parameter, EditorRequired] public EventCallback<(Guid ItemId, IBrowserFile File)> PhotoSelected { get; set; }

    private ClothingItem[] GetVisibleItems() => Items.Where(item => (selectedType is null || item.Type == selectedType) && (string.IsNullOrWhiteSpace(search) || item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || item.Color.Contains(search, StringComparison.OrdinalIgnoreCase))).ToArray();
    private void OpenCreate() => showForm = true;
    private void CloseCreate() => showForm = false;
    private void CloseDetails() => selectedItem = null;
    private async Task SaveAsync() { await Created.InvokeAsync((input.Name, input.Color, input.OwnerId, input.Type, input.Season, input.Style, string.IsNullOrWhiteSpace(input.Material) ? null : input.Material.Trim(), input.WeightGrams, newPhoto)); input.Name = input.Color = input.Material = string.Empty; newPhoto = null; newPhotoPreview = null; showForm = false; }
    private async Task SelectNewPhoto(InputFileChangeEventArgs args)
    {
        newPhoto = args.File;
        recognitionError = null;
        await using var previewStream = newPhoto.OpenReadStream(5 * 1024 * 1024);
        using var preview = new MemoryStream();
        await previewStream.CopyToAsync(preview);
        newPhotoPreview = $"data:{newPhoto.ContentType};base64,{Convert.ToBase64String(preview.ToArray())}";
        if (RecognitionRequested is null) { return; }
        isAnalyzing = true;
        try { ApplyRecognition(await RecognitionRequested(newPhoto)); }
        catch (Exception) { recognitionError = "No se pudo reconocer la prenda. Puedes completar los campos manualmente."; }
        finally { isAnalyzing = false; }
    }
    private void ApplyRecognition(GarmentRecognitionSuggestion suggestion) { input.Type = TypeFromGemini(suggestion.Category); input.Color = suggestion.Color; input.Material = suggestion.Material ?? string.Empty; input.WeightGrams = suggestion.EstimatedWeightGrams; input.Season = suggestion.Seasons.Contains("Verano") ? Season.Summer : suggestion.Seasons.Contains("Invierno") ? Season.Winter : Season.MidSeason; input.Style = suggestion.Style switch { "Formal" => Style.Formal, "Deportivo" => Style.Sport, _ => Style.Casual }; input.Name = string.IsNullOrWhiteSpace(input.Name) ? suggestion.Category : input.Name; }
    private static ClothingType TypeFromGemini(string category) => category switch { "Camiseta" => ClothingType.TShirt, "Camisa" => ClothingType.Shirt, "Jersey" => ClothingType.Sweater, "Sudadera" => ClothingType.Hoodie, "Abrigo" => ClothingType.Coat, "Chaqueta" => ClothingType.Jacket, "Vestido" => ClothingType.Dress, "Falda" => ClothingType.Skirt, "Pantalón" => ClothingType.Trousers, "Pantalón corto" => ClothingType.Shorts, "Ropa interior" => ClothingType.Underwear, "Calcetines" => ClothingType.Socks, "Bañador" => ClothingType.Swimwear, "Pijama" => ClothingType.Pyjamas, "Cinturón" => ClothingType.Belt, "Bolso" => ClothingType.Bag, "Zapatos" => ClothingType.Shoes, "Sandalias" => ClothingType.Sandals, _ => ClothingType.Accessory };
    private async Task ChangeItemAsync(ClothingItem item, bool clean, bool available) => await Changed.InvokeAsync((item, clean, available));
    private async Task UploadPhotoAsync(Guid itemId, InputFileChangeEventArgs args) => await PhotoSelected.InvokeAsync((itemId, args.File));
    private void RequestRemoval(Guid itemId) { selectedItem = null; pendingRemovalId = itemId; }
    private void CancelRemoval() => pendingRemovalId = null;
    private async Task ConfirmRemovalAsync() { if (pendingRemovalId is Guid id) { pendingRemovalId = null; await Removed.InvokeAsync(id); } }
    public void SetPhotoUrl(Guid itemId, string url) => photoUrls[itemId] = url;
    private void ApplyDefaultWeight() => input.WeightGrams = DefaultWeight(input.Type);
    private static int DefaultWeight(ClothingType type) => type switch { ClothingType.TShirt => 180, ClothingType.Shirt => 220, ClothingType.Sweater => 450, ClothingType.Hoodie => 550, ClothingType.Coat => 1200, ClothingType.Dress => 350, ClothingType.Skirt => 220, ClothingType.Underwear => 80, ClothingType.Socks => 60, ClothingType.Swimwear => 120, ClothingType.Pyjamas => 350, ClothingType.Belt => 180, ClothingType.Bag => 500, ClothingType.Trousers => 500, ClothingType.Shorts => 250, ClothingType.Jacket => 800, ClothingType.Shoes => 900, ClothingType.Sandals => 450, _ => 100 };
    private static string TypeName(ClothingType type) => type switch { ClothingType.TShirt => "Camiseta", ClothingType.Shirt => "Camisa", ClothingType.Sweater => "Jersey", ClothingType.Hoodie => "Sudadera", ClothingType.Coat => "Abrigo", ClothingType.Dress => "Vestido", ClothingType.Skirt => "Falda", ClothingType.Underwear => "Ropa interior", ClothingType.Socks => "Calcetines", ClothingType.Swimwear => "Bañador", ClothingType.Pyjamas => "Pijama", ClothingType.Belt => "Cinturón", ClothingType.Bag => "Bolso", ClothingType.Trousers => "Pantalón", ClothingType.Shorts => "Pantalón corto", ClothingType.Jacket => "Chaqueta", ClothingType.Shoes => "Zapatos", ClothingType.Sandals => "Sandalias", _ => "Accesorio" };
    private static string GarmentIcon(ClothingType type) => type switch { ClothingType.TShirt or ClothingType.Shirt => "👕", ClothingType.Trousers => "👖", ClothingType.Shorts => "🩳", ClothingType.Jacket or ClothingType.Coat => "🧥", ClothingType.Sweater or ClothingType.Hoodie => "🧶", ClothingType.Dress or ClothingType.Skirt => "👗", ClothingType.Shoes => "👟", ClothingType.Sandals => "🩴", ClothingType.Socks => "🧦", ClothingType.Bag => "👜", _ => "🧢" };
    private static string ColorStyle(string color) => $"background-color:{color}";
    private string OwnerName(Guid? ownerId) => Profiles.FirstOrDefault(profile => profile.Id == ownerId)?.Name ?? "Sin asignar";

    private sealed class Input { public string Name { get; set; } = string.Empty; public string Color { get; set; } = string.Empty; public string Material { get; set; } = string.Empty; public Guid OwnerId { get; set; } public ClothingType Type { get; set; } = ClothingType.TShirt; public Season Season { get; set; } = Season.AllYear; public Style Style { get; set; } = Style.Casual; public int WeightGrams { get; set; } = 180; }
}
