using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartPacking.Infrastructure;

public sealed class SqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SmartPackingDbContext>
{
    public SmartPackingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SmartPackingDbContext>()
            .UseSqlite("Data Source=smartpacking-design.db")
            .Options;
        return new SmartPackingDbContext(options);
    }
}
