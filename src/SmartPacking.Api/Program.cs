using Microsoft.EntityFrameworkCore;
using SmartPacking.Application;
using SmartPacking.Domain;
using SmartPacking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<PackingRecommendationService>();
builder.Services.AddScoped<PackingListService>();
builder.Services.AddScoped<ISmartPackingStore, EfSmartPackingStore>();
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
    return Results.Ok(await store.GetWardrobeAsync(user.Id, cancellationToken));
});
api.MapPost("/wardrobe", async (ClothingItem item, ISmartPackingStore store, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    var created = await store.AddClothingItemAsync(user.Id, item, cancellationToken);
    return Results.Created($"/api/wardrobe/{created.Id}", created);
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
    return Results.Created($"/api/trips/{created.Id}", created);
});
api.MapGet("/trips/{tripId:guid}/packing-list", async (Guid tripId, ISmartPackingStore store, PackingListService service, CancellationToken cancellationToken) =>
{
    var user = await store.GetDefaultUserAsync(cancellationToken);
    var plan = await service.GetOrCreateAsync(user.Id, tripId, cancellationToken);
    return plan is null ? Results.NotFound() : Results.Ok(plan);
});
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

app.Run();

public partial class Program;
public sealed record SetPackedRequest(bool IsPacked);
public sealed record UpdateClothingStatusRequest(bool IsClean, bool IsAvailable);
public sealed record CreateTripRequest(string Destination, DateOnly StartDate, DateOnly EndDate, int MinimumTemperatureCelsius, int MaximumTemperatureCelsius, IReadOnlyCollection<Style> Activities);
