using Microsoft.EntityFrameworkCore;

namespace SmartPacking.Infrastructure;

public sealed class SmartPackingDbContext(DbContextOptions<SmartPackingDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<ClothingItemEntity> ClothingItems => Set<ClothingItemEntity>();
    public DbSet<TripEntity> Trips => Set<TripEntity>();
    public DbSet<PackingListEntity> PackingLists => Set<PackingListEntity>();
    public DbSet<PackingListItemEntity> PackingListItems => Set<PackingListItemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<ClothingItemEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<ClothingItemEntity>().HasIndex(entity => new { entity.UserId, entity.Name }).IsUnique();
        modelBuilder.Entity<TripEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<PackingListEntity>().HasKey(entity => entity.Id);
        modelBuilder.Entity<PackingListEntity>().HasIndex(entity => new { entity.UserId, entity.TripId }).IsUnique();
        modelBuilder.Entity<PackingListItemEntity>().HasKey(entity => new { entity.PackingListId, entity.ClothingItemId });
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
