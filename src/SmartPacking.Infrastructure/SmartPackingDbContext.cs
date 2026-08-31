using Microsoft.EntityFrameworkCore;

namespace SmartPacking.Infrastructure;

public sealed class SmartPackingDbContext(DbContextOptions<SmartPackingDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<ClothingItemEntity> ClothingItems => Set<ClothingItemEntity>();
    public DbSet<TripEntity> Trips => Set<TripEntity>();
    public DbSet<PackingListEntity> PackingLists => Set<PackingListEntity>();
    public DbSet<PackingListItemEntity> PackingListItems => Set<PackingListItemEntity>();
    public DbSet<ChecklistItemEntity> ChecklistItems => Set<ChecklistItemEntity>();
    public DbSet<ClothingUsageEntity> ClothingUsage => Set<ClothingUsageEntity>();
    public DbSet<FamilyProfileEntity> FamilyProfiles => Set<FamilyProfileEntity>();
    public DbSet<TripProfileEntity> TripProfiles => Set<TripProfileEntity>();
    public DbSet<ProfilePackingListEntity> ProfilePackingLists => Set<ProfilePackingListEntity>();
    public DbSet<ProfilePackingListItemEntity> ProfilePackingListItems => Set<ProfilePackingListItemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<ClothingItemEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<ClothingItemEntity>().HasIndex(entity => new { entity.UserId, entity.Name }).IsUnique();
        modelBuilder.Entity<TripEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<PackingListEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<PackingListEntity>().HasIndex(entity => new { entity.UserId, entity.TripId }).IsUnique();
        modelBuilder.Entity<PackingListItemEntity>().HasKey(entity => new { entity.PackingListId, entity.ClothingItemId });
        modelBuilder.Entity<ChecklistItemEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<ClothingUsageEntity>().HasKey(entity => new { entity.TripId, entity.ClothingItemId });
        modelBuilder.Entity<FamilyProfileEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<TripProfileEntity>().HasKey(entity => new { entity.TripId, entity.ProfileId });
        modelBuilder.Entity<ProfilePackingListEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<ProfilePackingListEntity>().HasIndex(entity => new { entity.UserId, entity.TripId, entity.ProfileId }).IsUnique();
        modelBuilder.Entity<ProfilePackingListItemEntity>().HasKey(entity => new { entity.PackingListId, entity.ClothingItemId });
    }
}

public sealed class UserEntity { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; }
public sealed class ClothingItemEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public int Season { get; set; }
    public string Color { get; set; } = string.Empty;
    public int WarmthLevel { get; set; }
    public bool Waterproof { get; set; }
    public int Style { get; set; }
    public int? WeightGrams { get; set; }
    public bool IsClean { get; set; }
    public bool IsAvailable { get; set; }
    public int PreferenceScore { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? OwnerProfileId { get; set; }
    public string CombinationIds { get; set; } = "[]";
}
public sealed class TripEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Destination { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int MinimumTemperatureCelsius { get; set; }
    public int MaximumTemperatureCelsius { get; set; }
    public string Activities { get; set; } = "[]";
}
public sealed class PackingListEntity { public Guid Id { get; set; } public Guid TripId { get; set; } public Guid UserId { get; set; } public DateTimeOffset CreatedAt { get; set; } }
public sealed class PackingListItemEntity { public Guid PackingListId { get; set; } public Guid ClothingItemId { get; set; } public bool IsPacked { get; set; } }
public sealed class ChecklistItemEntity { public Guid Id { get; set; } public Guid UserId { get; set; } public Guid TripId { get; set; } public int Category { get; set; } public string Name { get; set; } = string.Empty; public bool IsPacked { get; set; } }
public sealed class ClothingUsageEntity { public Guid TripId { get; set; } public Guid ClothingItemId { get; set; } public Guid UserId { get; set; } public bool WasUsed { get; set; } }
public sealed class FamilyProfileEntity { public Guid Id { get; set; } public Guid UserId { get; set; } public string Name { get; set; } = string.Empty; }
public sealed class TripProfileEntity { public Guid TripId { get; set; } public Guid ProfileId { get; set; } public Guid UserId { get; set; } }
public sealed class ProfilePackingListEntity { public Guid Id { get; set; } public Guid TripId { get; set; } public Guid ProfileId { get; set; } public Guid UserId { get; set; } public DateTimeOffset CreatedAt { get; set; } }
public sealed class ProfilePackingListItemEntity { public Guid PackingListId { get; set; } public Guid ClothingItemId { get; set; } public bool IsPacked { get; set; } }
