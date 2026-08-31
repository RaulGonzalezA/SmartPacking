using Microsoft.EntityFrameworkCore;

namespace SmartPacking.Infrastructure;

public static class SmartPackingDatabaseMigrator
{
    private const string InitialMigrationId = "20260831111529_InitialCreate";
    private const string EfProductVersion = "10.0.9";

    public static async Task MigrateAsync(SmartPackingDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await HasLegacySchemaAsync(dbContext, cancellationToken))
        {
            await dbContext.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (MigrationId TEXT NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY, ProductVersion TEXT NOT NULL)", cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({InitialMigrationId}, {EfProductVersion})", cancellationToken);
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    private static async Task<bool> HasLegacySchemaAsync(SmartPackingDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return false;
        }

        return await dbContext.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table' AND name = 'Users'").AnyAsync(cancellationToken);
    }
}
