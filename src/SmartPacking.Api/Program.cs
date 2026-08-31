using Microsoft.EntityFrameworkCore;
using SmartPacking.Api;
using SmartPacking.Application;
using SmartPacking.Domain;
using SmartPacking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<PackingListService>();
builder.Services.AddScoped<ProfilePackingListService>();
builder.Services.AddScoped<ISmartPackingStore, EfSmartPackingStore>();
builder.Services.AddHttpClient<OpenMeteoWeatherProvider>(client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddDbContext<SmartPackingDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SmartPacking") ?? "Data Source=smartpacking.db"));

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<ISmartPackingStore>().SeedAsync(CancellationToken.None);
}
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");
api.MapGet("/me", async (ISmartPackingStore store, CancellationToken cancellationToken) => Results.Ok(await store.GetDefaultUserAsync(cancellationToken)));
api.MapGet("/wardrobe", async (ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    return Results.Ok((await store.GetWardrobeAsync(user.Id, cancellationToken)).Where(item => !item.IsDeleted));
});
api.MapPost("/wardrobe", async (ClothingItem item, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    var created = await store.AddClothingItemAsync(user.Id, item, cancellationToken);
    return Results.Created($"/api/wardrobe/{created.Id}", created);
});
api.MapDelete("/wardrobe/{clothingItemId:guid}", async (Guid clothingItemId, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    return await store.DeleteClothingItemAsync(user.Id, clothingItemId, cancellationToken) ? Results.NoContent() : Results.NotFound();
});
api.MapGet("/wardrobe/deleted", async (ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    return Results.Ok((await store.GetWardrobeAsync(user.Id, cancellationToken)).Where(item => item.IsDeleted));
});
api.MapPost("/wardrobe/{clothingItemId:guid}/restore", async (Guid clothingItemId, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    return await store.RestoreClothingItemAsync(user.Id, clothingItemId, cancellationToken) ? Results.NoContent() : Results.NotFound();
});
api.MapPut("/wardrobe/{clothingItemId:guid}", async (Guid clothingItemId, ClothingItem item, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    if (clothingItemId != item.Id || string.IsNullOrWhiteSpace(item.Name) || item.WarmthLevel is < 1 or > 10)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["clothingItem"] = ["Introduce datos válidos para la prenda."] });

    var user = await store.GetDefaultUserAsync(cancellationToken);
    var updated = await store.UpdateClothingItemAsync(user.Id, item, cancellationToken);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
});
api.MapPost("/wardrobe/{clothingItemId:guid}/photo", async (Guid clothingItemId, IFormFile photo, IWebHostEnvironment environment, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    if (photo.Length == 0 || photo.Length > 5 * 1024 * 1024 || !string.Equals(photo.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["photo"] = ["Selecciona una foto JPEG de hasta 5 MB."] });

    var user = await store.GetDefaultUserAsync(cancellationToken);
    var wardrobe = await store.GetWardrobeAsync(user.Id, cancellationToken);
    if (!wardrobe.Any(item => item.Id == clothingItemId)) return Results.NotFound();

    var uploadDirectory = Path.Combine(environment.WebRootPath, "uploads");
    Directory.CreateDirectory(uploadDirectory);
    var destination = Path.Combine(uploadDirectory, $"{clothingItemId}.jpg");
    await using var output = File.Create(destination);
    await photo.CopyToAsync(output, cancellationToken);
    return Results.Ok(new { imageUrl = $"/uploads/{clothingItemId}.jpg" });
});
api.MapPut("/wardrobe/{clothingItemId:guid}/status", async (Guid clothingItemId, UpdateClothingStatusRequest request, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    var updated = await store.UpdateClothingStatusAsync(user.Id, clothingItemId, request.IsClean, request.IsAvailable, cancellationToken);
    return updated ? Results.NoContent() : Results.NotFound();
});
api.MapGet("/trips", async (ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    return Results.Ok(await store.GetTripsAsync(user.Id, cancellationToken));
});
api.MapPost("/trips", async (CreateTripRequest request, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    if (request.EndDate < request.StartDate || string.IsNullOrWhiteSpace(request.Destination))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["trip"] = ["Introduce un destino y fechas válidas."] });

    var user = await store.GetDefaultUserAsync(cancellationToken);
    var trip = new Trip(Guid.NewGuid(), request.Destination.Trim(), request.StartDate, request.EndDate, request.MinimumTemperatureCelsius, request.MaximumTemperatureCelsius, request.Activities.Count == 0 ? [Style.Casual] : request.Activities);
    var created = await store.AddTripAsync(user.Id, trip, cancellationToken);
    await store.SetTripProfilesAsync(user.Id, created.Id, [user.Id], cancellationToken);
    await store.AddChecklistItemsAsync(user.Id, CreateDefaultChecklist(created.Id), cancellationToken);
    return Results.Created($"/api/trips/{created.Id}", created);
});
api.MapDelete("/trips/{tripId:guid}", async (Guid tripId, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    return await store.DeleteTripAsync(user.Id, tripId, cancellationToken) ? Results.NoContent() : Results.NotFound();
});
api.MapGet("/trips/{tripId:guid}/packing-list", async (Guid tripId, ISmartPackingStore store, PackingListService service, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    var plan = await service.GetOrCreateAsync(user.Id, tripId, cancellationToken);
    return plan is null ? Results.NotFound() : Results.Ok(plan);
});
api.MapGet("/profiles", async (ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    return Results.Ok(await store.GetFamilyProfilesAsync(user.Id, cancellationToken));
});
api.MapPost("/profiles", async (CreateFamilyProfileRequest request, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Escribe un nombre para el perfil."] });
    var user = await store.GetDefaultUserAsync(cancellationToken);
    var profile = await store.AddFamilyProfileAsync(user.Id, new FamilyProfile(Guid.NewGuid(), request.Name.Trim()), cancellationToken);
    return Results.Created($"/api/profiles/{profile.Id}", profile);
});
api.MapGet("/trips/{tripId:guid}/profiles", async (Guid tripId, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    var profiles = await store.GetTripProfilesAsync(user.Id, tripId, cancellationToken);
    if (profiles.Count == 0 && await store.GetTripAsync(user.Id, tripId, cancellationToken) is not null)
    {
        await store.SetTripProfilesAsync(user.Id, tripId, [user.Id], cancellationToken);
        profiles = await store.GetTripProfilesAsync(user.Id, tripId, cancellationToken);
    }
    return Results.Ok(profiles);
});
api.MapPut("/trips/{tripId:guid}/profiles", async (Guid tripId, SetTripProfilesRequest request, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    if (await store.GetTripAsync(user.Id, tripId, cancellationToken) is null) return Results.NotFound();
    await store.SetTripProfilesAsync(user.Id, tripId, request.ProfileIds, cancellationToken);
    return Results.NoContent();
});
api.MapGet("/trips/{tripId:guid}/profiles/{profileId:guid}/packing-list", async (Guid tripId, Guid profileId, ProfilePackingListService service, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    var plan = await service.GetOrCreateAsync(user.Id, tripId, profileId, cancellationToken);
    return plan is null ? Results.NotFound() : Results.Ok(plan);
});
api.MapPut("/profile-packing-lists/{packingListId:guid}/items/{clothingItemId:guid}", async (Guid packingListId, Guid clothingItemId, SetPackedRequest request, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    await store.SetProfilePackedAsync(user.Id, packingListId, clothingItemId, request.IsPacked, cancellationToken);
    return Results.NoContent();
});
api.MapGet("/trips/{tripId:guid}/weather", async (Guid tripId, ISmartPackingStore store, OpenMeteoWeatherProvider weatherProvider, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    var trip = await store.GetTripAsync(user.Id, tripId, cancellationToken);
    if (trip is null) return Results.NotFound();
    var forecast = await weatherProvider.GetAsync(trip.Destination, trip.StartDate, trip.EndDate, cancellationToken);
    return forecast is null ? Results.NotFound(new { message = "La previsión solo está disponible para los próximos 16 días." }) : Results.Ok(forecast);
});
api.MapGet("/trips/{tripId:guid}/checklist", async (Guid tripId, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    if (await store.GetTripAsync(user.Id, tripId, cancellationToken) is null) return Results.NotFound();
    var items = await store.GetChecklistAsync(user.Id, tripId, cancellationToken);
    if (items.Count == 0) items = await store.AddChecklistItemsAsync(user.Id, CreateDefaultChecklist(tripId), cancellationToken);
    return Results.Ok(items);
});
api.MapPost("/trips/{tripId:guid}/checklist", async (Guid tripId, CreateChecklistItemRequest request, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Escribe un elemento para la checklist."] });
    var user = await store.GetDefaultUserAsync(cancellationToken);
    if (await store.GetTripAsync(user.Id, tripId, cancellationToken) is null) return Results.NotFound();
    var item = new ChecklistItem(Guid.NewGuid(), tripId, request.Category, request.Name.Trim(), false);
    await store.AddChecklistItemsAsync(user.Id, [item], cancellationToken);
    return Results.Created($"/api/trips/{tripId}/checklist/{item.Id}", item);
});
api.MapPut("/checklist/{itemId:guid}", async (Guid itemId, SetPackedRequest request, ISmartPackingStore store, CancellationToken cancellationToken) => { var user = await store.GetDefaultUserAsync(cancellationToken); await store.SetChecklistPackedAsync(user.Id, itemId, request.IsPacked, cancellationToken); return Results.NoContent(); });
api.MapPost("/trips/{tripId:guid}/usage", async (Guid tripId, IReadOnlyCollection<ClothingUsage> usage, ISmartPackingStore store, CancellationToken cancellationToken) => { var user = await store.GetDefaultUserAsync(cancellationToken); await store.SaveUsageAsync(user.Id, tripId, usage, cancellationToken); return Results.NoContent(); });
api.MapGet("/trips/{tripId:guid}/usage", async (Guid tripId, ISmartPackingStore store, CancellationToken cancellationToken) => { var user = await store.GetDefaultUserAsync(cancellationToken); return Results.Ok(await store.GetUsageAsync(user.Id, tripId, cancellationToken)); });
api.MapPut("/packing-lists/{packingListId:guid}/items/{clothingItemId:guid}", async (Guid packingListId, Guid clothingItemId, SetPackedRequest request, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    await store.SetPackedAsync(user.Id, packingListId, clothingItemId, request.IsPacked, cancellationToken);
    return Results.NoContent();
});
api.MapGet("/recommendations/current", async (ISmartPackingStore store, PackingListService service, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    var plan = await service.GetOrCreateAsync(user.Id, DemoData.RomeTrip.Id, cancellationToken);
    return Results.Ok(plan);
});

static IReadOnlyList<ChecklistItem> CreateDefaultChecklist(Guid tripId) =>
[
    new(Guid.NewGuid(), tripId, ChecklistCategory.Documents, "DNI o pasaporte", false),
    new(Guid.NewGuid(), tripId, ChecklistCategory.Documents, "Tarjetas y reservas", false),
    new(Guid.NewGuid(), tripId, ChecklistCategory.Documents, "Seguro de viaje", false),
    new(Guid.NewGuid(), tripId, ChecklistCategory.Toiletries, "Cepillo y pasta de dientes", false),
    new(Guid.NewGuid(), tripId, ChecklistCategory.Toiletries, "Desodorante", false),
    new(Guid.NewGuid(), tripId, ChecklistCategory.Toiletries, "Protector solar", false),
    new(Guid.NewGuid(), tripId, ChecklistCategory.Technology, "Móvil y cargador", false),
    new(Guid.NewGuid(), tripId, ChecklistCategory.Technology, "Adaptador de enchufe", false),
    new(Guid.NewGuid(), tripId, ChecklistCategory.Technology, "Auriculares", false),
    new(Guid.NewGuid(), tripId, ChecklistCategory.Health, "Medicación personal", false),
    new(Guid.NewGuid(), tripId, ChecklistCategory.Health, "Tiritas y básicos", false),
    new(Guid.NewGuid(), tripId, ChecklistCategory.Other, "Gafas de sol", false),
    new(Guid.NewGuid(), tripId, ChecklistCategory.Other, "Botella reutilizable", false)
];

await app.RunAsync();

public partial class Program { private Program() { } }
