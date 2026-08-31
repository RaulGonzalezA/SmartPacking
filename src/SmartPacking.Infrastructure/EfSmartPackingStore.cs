using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartPacking.Application;
using SmartPacking.Domain;

namespace SmartPacking.Infrastructure;

public sealed class EfSmartPackingStore(SmartPackingDbContext dbContext) : ISmartPackingStore
{
    private static readonly Guid DefaultUserId = Guid.Parse("90ae4435-5a54-42dc-a0a4-4f8aa4d96f90");

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        if (await dbContext.Users.AnyAsync(cancellationToken)) return;

        dbContext.Users.Add(new UserEntity { Id = DefaultUserId, Name = "Raúl" });
        dbContext.Trips.Add(ToEntity(DefaultUserId, DemoData.RomeTrip));
        dbContext.ClothingItems.AddRange(DemoData.Wardrobe.Select(item => ToEntity(DefaultUserId, item)));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserProfile> GetDefaultUserAsync(CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleAsync(cancellationToken);
        return new UserProfile(user.Id, user.Name);
    }

    public async Task<IReadOnlyList<ClothingItem>> GetWardrobeAsync(Guid userId, CancellationToken cancellationToken) =>
        (await dbContext.ClothingItems.Where(item => item.UserId == userId).OrderBy(item => item.Name).ToListAsync(cancellationToken)).Select(ToDomain).ToArray();

    public async Task<ClothingItem> AddClothingItemAsync(Guid userId, ClothingItem item, CancellationToken cancellationToken)
    {
        var entity = ToEntity(userId, item with { Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id });
        dbContext.ClothingItems.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDomain(entity);
    }

    public async Task<bool> UpdateClothingStatusAsync(Guid userId, Guid clothingItemId, bool isClean, bool isAvailable, CancellationToken cancellationToken)
    {
        var item = await dbContext.ClothingItems.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == clothingItemId, cancellationToken);
        if (item is null) return false;

        item.IsClean = isClean;
        item.IsAvailable = isAvailable;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<Trip>> GetTripsAsync(Guid userId, CancellationToken cancellationToken) =>
        (await dbContext.Trips.Where(item => item.UserId == userId).OrderBy(item => item.StartDate).ToListAsync(cancellationToken)).Select(ToDomain).ToArray();

    public async Task<Trip> AddTripAsync(Guid userId, Trip trip, CancellationToken cancellationToken)
    {
        var entity = ToEntity(userId, trip with { Id = trip.Id == Guid.Empty ? Guid.NewGuid() : trip.Id });
        dbContext.Trips.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDomain(entity);
    }

    public async Task<Trip?> GetTripAsync(Guid userId, Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await dbContext.Trips.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == tripId, cancellationToken);
        return trip is null ? null : ToDomain(trip);
    }

    public async Task<PackingList?> GetPackingListAsync(Guid userId, Guid tripId, CancellationToken cancellationToken)
    {
        var list = await dbContext.PackingLists.SingleOrDefaultAsync(item => item.UserId == userId && item.TripId == tripId, cancellationToken);
        if (list is null) return null;
        var items = await dbContext.PackingListItems.Where(item => item.PackingListId == list.Id).Select(item => new PackingListItem(item.ClothingItemId, item.IsPacked)).ToListAsync(cancellationToken);
        return new PackingList(list.Id, list.TripId, list.UserId, list.CreatedAt, items);
    }

    public async Task<PackingList> SavePackingListAsync(PackingList packingList, CancellationToken cancellationToken)
    {
        dbContext.PackingLists.Add(new PackingListEntity { Id = packingList.Id, TripId = packingList.TripId, UserId = packingList.UserId, CreatedAt = packingList.CreatedAt });
        dbContext.PackingListItems.AddRange(packingList.Items.Select(item => new PackingListItemEntity { PackingListId = packingList.Id, ClothingItemId = item.ClothingItemId, IsPacked = item.IsPacked }));
        await dbContext.SaveChangesAsync(cancellationToken);
        return packingList;
    }

    public async Task SetPackedAsync(Guid userId, Guid packingListId, Guid clothingItemId, bool isPacked, CancellationToken cancellationToken)
    {
        var belongsToUser = await dbContext.PackingLists.AnyAsync(list => list.Id == packingListId && list.UserId == userId, cancellationToken);
        if (!belongsToUser) return;
        var item = await dbContext.PackingListItems.SingleOrDefaultAsync(entry => entry.PackingListId == packingListId && entry.ClothingItemId == clothingItemId, cancellationToken);
        if (item is null) return;
        item.IsPacked = isPacked;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ClothingItemEntity ToEntity(Guid userId, ClothingItem item) => new()
    {
        Id = item.Id, UserId = userId, Name = item.Name, Type = (int)item.Type, Season = (int)item.Season, Color = item.Color,
        WarmthLevel = item.WarmthLevel, Waterproof = item.Waterproof, Style = (int)item.Style, WeightGrams = item.WeightGrams,
        IsClean = item.IsClean, IsAvailable = item.IsAvailable, PreferenceScore = item.PreferenceScore, CombinationIds = JsonSerializer.Serialize(item.CombinesWith)
    };
    private static ClothingItem ToDomain(ClothingItemEntity item) => new(item.Id, item.Name, (ClothingType)item.Type, (Season)item.Season, item.Color, item.WarmthLevel, item.Waterproof, (Style)item.Style, item.WeightGrams, item.IsClean, item.IsAvailable, item.PreferenceScore, JsonSerializer.Deserialize<Guid[]>(item.CombinationIds) ?? []);
    private static TripEntity ToEntity(Guid userId, Trip trip) => new() { Id = trip.Id, UserId = userId, Destination = trip.Destination, StartDate = trip.StartDate, EndDate = trip.EndDate, MinimumTemperatureCelsius = trip.MinimumTemperatureCelsius, MaximumTemperatureCelsius = trip.MaximumTemperatureCelsius, Activities = JsonSerializer.Serialize(trip.Activities) };
    private static Trip ToDomain(TripEntity trip) => new(trip.Id, trip.Destination, trip.StartDate, trip.EndDate, trip.MinimumTemperatureCelsius, trip.MaximumTemperatureCelsius, JsonSerializer.Deserialize<Style[]>(trip.Activities) ?? []);
}
