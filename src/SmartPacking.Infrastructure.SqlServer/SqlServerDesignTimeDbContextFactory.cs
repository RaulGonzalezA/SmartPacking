using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SmartPacking.Infrastructure;

namespace SmartPacking.Infrastructure.SqlServer;

public sealed class SqlServerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SmartPackingDbContext>
{
    public SmartPackingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SmartPackingDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=SmartPacking;Trusted_Connection=True;TrustServerCertificate=True",
                sqlServer => sqlServer.MigrationsAssembly(typeof(SqlServerDesignTimeDbContextFactory).Assembly.FullName))
            .Options;
        return new SmartPackingDbContext(options);
    }
}
