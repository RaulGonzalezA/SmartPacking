using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SmartPacking.Infrastructure;

namespace SmartPacking.Infrastructure.PostgreSql;

public sealed class PostgreSqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SmartPackingDbContext>
{
    public SmartPackingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SmartPackingDbContext>()
            .UseNpgsql("Host=localhost;Database=smartpacking;Username=postgres", npgsql => npgsql.MigrationsAssembly(typeof(PostgreSqlDesignTimeDbContextFactory).Assembly.FullName))
            .Options;
        return new SmartPackingDbContext(options);
    }
}
