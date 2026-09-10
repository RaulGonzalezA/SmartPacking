using Microsoft.EntityFrameworkCore;

namespace SmartPacking.Infrastructure;

public sealed class SmartPackingDbContext(DbContextOptions<SmartPackingDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<UserAuditEventEntity> UserAuditEvents => Set<UserAuditEventEntity>();
    public DbSet<GarmentRecognitionEventEntity> GarmentRecognitionEvents => Set<GarmentRecognitionEventEntity>();
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
    public DbSet<UserTripTemplateEntity> UserTripTemplates => Set<UserTripTemplateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartPackingDbContext).Assembly);
    }
}
