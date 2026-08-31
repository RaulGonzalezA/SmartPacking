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
        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            if (!await dbContext.FamilyProfiles.AnyAsync(profile => profile.UserId == DefaultUserId, cancellationToken))
            {
                dbContext.FamilyProfiles.Add(new FamilyProfileEntity { Id = DefaultUserId, UserId = DefaultUserId, Name = "Raúl" });
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        dbContext.Users.Add(new UserEntity { Id = DefaultUserId, Name = "Raúl" });
        dbContext.FamilyProfiles.Add(new FamilyProfileEntity { Id = DefaultUserId, UserId = DefaultUserId, Name = "Raúl" });
        dbContext.Trips.Add(ToEntity(DefaultUserId, DemoData.RomeTrip));
        dbContext.ClothingItems.AddRange(DemoData.Wardrobe.Select(item => ToEntity(DefaultUserId, item)));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserProfile> GetDefaultUserAsync(CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleAsync(cancellationToken);
        return new UserProfile(user.Id, user.Name);
    }

    public async Task<IReadOnlyList<FamilyProfile>> GetFamilyProfilesAsync(Guid userId, CancellationToken cancellationToken) =>
        (await dbContext.FamilyProfiles.Where(profile => profile.UserId == userId).OrderBy(profile => profile.Name).ToListAsync(cancellationToken)).Select(profile => new FamilyProfile(profile.Id, profile.Name)).ToArray();

    public async Task<FamilyProfile> AddFamilyProfileAsync(Guid userId, FamilyProfile profile, CancellationToken cancellationToken)
    {
        var entity = new FamilyProfileEntity { Id = profile.Id == Guid.Empty ? Guid.NewGuid() : profile.Id, UserId = userId, Name = profile.Name };
        dbContext.FamilyProfiles.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new FamilyProfile(entity.Id, entity.Name);
    }

    public async Task<IReadOnlyList<FamilyProfile>> GetTripProfilesAsync(Guid userId, Guid tripId, CancellationToken cancellationToken) =>
        (await (from link in dbContext.TripProfiles
                join profile in dbContext.FamilyProfiles on link.ProfileId equals profile.Id
                where link.UserId == userId && link.TripId == tripId && profile.UserId == userId
                orderby profile.Name
                select new FamilyProfile(profile.Id, profile.Name)).ToListAsync(cancellationToken));

    public async Task SetTripProfilesAsync(Guid userId, Guid tripId, IReadOnlyCollection<Guid> profileIds, CancellationToken cancellationToken)
    {
        var validIds = await dbContext.FamilyProfiles.Where(profile => profile.UserId == userId && profileIds.Contains(profile.Id)).Select(profile => profile.Id).ToListAsync(cancellationToken);
        var current = dbContext.TripProfiles.Where(link => link.UserId == userId && link.TripId == tripId);
        dbContext.TripProfiles.RemoveRange(current);
        dbContext.TripProfiles.AddRange(validIds.Distinct().Select(profileId => new TripProfileEntity { UserId = userId, TripId = tripId, ProfileId = profileId }));
        await dbContext.SaveChangesAsync(cancellationToken);
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

    public async Task<bool> DeleteClothingItemAsync(Guid userId, Guid clothingItemId, CancellationToken cancellationToken)
    {
        var item = await dbContext.ClothingItems.SingleOrDefaultAsync(candidate => candidate.UserId == userId && candidate.Id == clothingItemId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        item.IsDeleted = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RestoreClothingItemAsync(Guid userId, Guid clothingItemId, CancellationToken cancellationToken)
    {
        var item = await dbContext.ClothingItems.SingleOrDefaultAsync(candidate => candidate.UserId == userId && candidate.Id == clothingItemId && candidate.IsDeleted, cancellationToken);
        if (item is null)
        {
            return false;
        }

        item.IsDeleted = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ClothingItem?> UpdateClothingItemAsync(Guid userId, ClothingItem item, CancellationToken cancellationToken)
    {
        var entity = await dbContext.ClothingItems.SingleOrDefaultAsync(candidate => candidate.UserId == userId && candidate.Id == item.Id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Name = item.Name;
        entity.Type = (int)item.Type;
        entity.Season = (int)item.Season;
        entity.Color = item.Color;
        entity.WarmthLevel = item.WarmthLevel;
        entity.Waterproof = item.Waterproof;
        entity.Style = (int)item.Style;
        entity.WeightGrams = item.WeightGrams;
        entity.IsClean = item.IsClean;
        entity.IsAvailable = item.IsAvailable;
        entity.PreferenceScore = item.PreferenceScore;
        entity.IsDeleted = item.IsDeleted;
        entity.OwnerProfileId = item.OwnerProfileId;
        entity.CombinationIds = JsonSerializer.Serialize(item.CombinesWith);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDomain(entity);
    }

    public async Task<bool> UpdateClothingStatusAsync(Guid userId, Guid clothingItemId, bool isClean, bool isAvailable, CancellationToken cancellationToken)
    {
        var item = await dbContext.ClothingItems.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == clothingItemId, cancellationToken);
        if (item is null)
        {
            return false;
        }

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

    public async Task<bool> DeleteTripAsync(Guid userId, Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await dbContext.Trips.SingleOrDefaultAsync(candidate => candidate.UserId == userId && candidate.Id == tripId, cancellationToken);
        if (trip is null)
        {
            return false;
        }

        var lists = await dbContext.PackingLists.Where(list => list.UserId == userId && list.TripId == tripId).Select(list => list.Id).ToListAsync(cancellationToken);
        var profileLists = await dbContext.ProfilePackingLists.Where(list => list.UserId == userId && list.TripId == tripId).Select(list => list.Id).ToListAsync(cancellationToken);
        dbContext.PackingListItems.RemoveRange(dbContext.PackingListItems.Where(item => lists.Contains(item.PackingListId)));
        dbContext.ProfilePackingListItems.RemoveRange(dbContext.ProfilePackingListItems.Where(item => profileLists.Contains(item.PackingListId)));
        dbContext.PackingLists.RemoveRange(dbContext.PackingLists.Where(list => lists.Contains(list.Id)));
        dbContext.ProfilePackingLists.RemoveRange(dbContext.ProfilePackingLists.Where(list => profileLists.Contains(list.Id)));
        dbContext.ChecklistItems.RemoveRange(dbContext.ChecklistItems.Where(item => item.UserId == userId && item.TripId == tripId));
        dbContext.ClothingUsage.RemoveRange(dbContext.ClothingUsage.Where(item => item.UserId == userId && item.TripId == tripId));
        dbContext.TripProfiles.RemoveRange(dbContext.TripProfiles.Where(item => item.UserId == userId && item.TripId == tripId));
        dbContext.Trips.Remove(trip);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Trip?> GetTripAsync(Guid userId, Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await dbContext.Trips.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == tripId, cancellationToken);
        return trip is null ? null : ToDomain(trip);
    }

    public async Task<PackingList?> GetPackingListAsync(Guid userId, Guid tripId, CancellationToken cancellationToken)
    {
        var list = await dbContext.PackingLists.SingleOrDefaultAsync(item => item.UserId == userId && item.TripId == tripId, cancellationToken);
        if (list is null)
        {
            return null;
        }

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
        if (!belongsToUser)
        {
            return;
        }

        var item = await dbContext.PackingListItems.SingleOrDefaultAsync(entry => entry.PackingListId == packingListId && entry.ClothingItemId == clothingItemId, cancellationToken);
        if (item is null)
        {
            return;
        }

        item.IsPacked = isPacked;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProfilePackingList?> GetProfilePackingListAsync(Guid userId, Guid tripId, Guid profileId, CancellationToken cancellationToken)
    {
        var list = await dbContext.ProfilePackingLists.SingleOrDefaultAsync(item => item.UserId == userId && item.TripId == tripId && item.ProfileId == profileId, cancellationToken);
        if (list is null)
        {
            return null;
        }

        var items = await dbContext.ProfilePackingListItems.Where(item => item.PackingListId == list.Id).Select(item => new PackingListItem(item.ClothingItemId, item.IsPacked)).ToListAsync(cancellationToken);
        return new ProfilePackingList(list.Id, list.TripId, list.ProfileId, list.UserId, list.CreatedAt, items);
    }

    public async Task<ProfilePackingList> SaveProfilePackingListAsync(ProfilePackingList packingList, CancellationToken cancellationToken)
    {
        dbContext.ProfilePackingLists.Add(new ProfilePackingListEntity { Id = packingList.Id, TripId = packingList.TripId, ProfileId = packingList.ProfileId, UserId = packingList.UserId, CreatedAt = packingList.CreatedAt });
        dbContext.ProfilePackingListItems.AddRange(packingList.Items.Select(item => new ProfilePackingListItemEntity { PackingListId = packingList.Id, ClothingItemId = item.ClothingItemId, IsPacked = item.IsPacked }));
        await dbContext.SaveChangesAsync(cancellationToken);
        return packingList;
    }

    public async Task SetProfilePackedAsync(Guid userId, Guid packingListId, Guid clothingItemId, bool isPacked, CancellationToken cancellationToken)
    {
        var belongsToUser = await dbContext.ProfilePackingLists.AnyAsync(list => list.Id == packingListId && list.UserId == userId, cancellationToken);
        if (!belongsToUser)
        {
            return;
        }

        var item = await dbContext.ProfilePackingListItems.SingleOrDefaultAsync(entry => entry.PackingListId == packingListId && entry.ClothingItemId == clothingItemId, cancellationToken);
        if (item is null)
        {
            return;
        }

        item.IsPacked = isPacked;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChecklistItem>> GetChecklistAsync(Guid userId, Guid tripId, CancellationToken cancellationToken) =>
        (await dbContext.ChecklistItems.Where(item => item.UserId == userId && item.TripId == tripId).OrderBy(item => item.Category).ThenBy(item => item.Name).ToListAsync(cancellationToken)).Select(item => new ChecklistItem(item.Id, item.TripId, (ChecklistCategory)item.Category, item.Name, item.IsPacked)).ToArray();
    public async Task<IReadOnlyList<ChecklistItem>> AddChecklistItemsAsync(Guid userId, IReadOnlyCollection<ChecklistItem> items, CancellationToken cancellationToken)
    {
        dbContext.ChecklistItems.AddRange(items.Select(item => new ChecklistItemEntity { Id = item.Id, UserId = userId, TripId = item.TripId, Category = (int)item.Category, Name = item.Name, IsPacked = item.IsPacked }));
        await dbContext.SaveChangesAsync(cancellationToken);
        return items.ToArray();
    }
    public async Task SetChecklistPackedAsync(Guid userId, Guid checklistItemId, bool isPacked, CancellationToken cancellationToken)
    {
        var item = await dbContext.ChecklistItems.SingleOrDefaultAsync(item => item.UserId == userId && item.Id == checklistItemId, cancellationToken);
        if (item is null)
        {
            return;
        }

        item.IsPacked = isPacked;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    public async Task SaveUsageAsync(Guid userId, Guid tripId, IReadOnlyCollection<ClothingUsage> usage, CancellationToken cancellationToken)
    { var old = dbContext.ClothingUsage.Where(item => item.UserId == userId && item.TripId == tripId); dbContext.ClothingUsage.RemoveRange(old); dbContext.ClothingUsage.AddRange(usage.Select(item => new ClothingUsageEntity { UserId = userId, TripId = tripId, ClothingItemId = item.ClothingItemId, WasUsed = item.WasUsed })); await dbContext.SaveChangesAsync(cancellationToken); }
    public async Task<IReadOnlyList<ClothingUsage>> GetUsageAsync(Guid userId, Guid tripId, CancellationToken cancellationToken) =>
        (await dbContext.ClothingUsage.Where(item => item.UserId == userId && item.TripId == tripId).ToListAsync(cancellationToken)).Select(item => new ClothingUsage(item.TripId, item.ClothingItemId, item.WasUsed)).ToArray();

#pragma warning disable S4136 // Entity/domain conversions remain grouped by their related type.
    private static ClothingItemEntity ToEntity(Guid userId, ClothingItem item) => new()
    {
        Id = item.Id,
        UserId = userId,
        Name = item.Name,
        Type = (int)item.Type,
        Season = (int)item.Season,
        Color = item.Color,
        WarmthLevel = item.WarmthLevel,
        Waterproof = item.Waterproof,
        Style = (int)item.Style,
        WeightGrams = item.WeightGrams,
        IsClean = item.IsClean,
        IsAvailable = item.IsAvailable,
        PreferenceScore = item.PreferenceScore,
        IsDeleted = item.IsDeleted,
        OwnerProfileId = item.OwnerProfileId,
        CombinationIds = JsonSerializer.Serialize(item.CombinesWith)
    };
    private static ClothingItem ToDomain(ClothingItemEntity item) => new(item.Id, item.Name, (ClothingType)item.Type, (Season)item.Season, item.Color, item.WarmthLevel, item.Waterproof, (Style)item.Style, item.WeightGrams, item.IsClean, item.IsAvailable, item.PreferenceScore, JsonSerializer.Deserialize<Guid[]>(item.CombinationIds) ?? [], item.IsDeleted, item.OwnerProfileId);
    private static TripEntity ToEntity(Guid userId, Trip trip) => new() { Id = trip.Id, UserId = userId, Destination = trip.Destination, StartDate = trip.StartDate, EndDate = trip.EndDate, MinimumTemperatureCelsius = trip.MinimumTemperatureCelsius, MaximumTemperatureCelsius = trip.MaximumTemperatureCelsius, Activities = JsonSerializer.Serialize(trip.Activities) };
    private static Trip ToDomain(TripEntity trip) => new(trip.Id, trip.Destination, trip.StartDate, trip.EndDate, trip.MinimumTemperatureCelsius, trip.MaximumTemperatureCelsius, JsonSerializer.Deserialize<Style[]>(trip.Activities) ?? []);
#pragma warning restore S4136
}
