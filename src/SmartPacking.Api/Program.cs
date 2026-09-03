using Microsoft.EntityFrameworkCore;
using Serilog;
using SmartPacking.Api;
using SmartPacking.Api.DependencyInjection;
using SmartPacking.Application;
using SmartPacking.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddSmartPackingObservability();

var authenticationEnabled = builder.Services.AddSmartPackingAuthentication(builder.Configuration);

builder.Services
    .AddSmartPackingApi()
    .AddSmartPackingApplication()
    .AddSmartPackingPersistence(builder.Configuration)
    .AddSmartPackingCache(builder.Configuration)
    .AddSmartPackingExternalServices()
    .AddSmartPackingPhotoStorage(builder.Configuration)
    .AddSmartPackingHealthChecks();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SmartPackingDbContext>();
    if (dbContext.Database.IsSqlite())
    {
        await SmartPackingDatabaseMigrator.MigrateAsync(dbContext, CancellationToken.None);
    }
    else
    {
        await dbContext.Database.MigrateAsync();
    }
    await scope.ServiceProvider.GetRequiredService<ISmartPackingStore>().SeedAsync(CancellationToken.None);
}
app.UseStaticFiles();
app.UseExceptionHandler();
app.UseAntiforgery();
app.UseSerilogRequestLogging();
if (authenticationEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar", options => options.Title = "SmartPacking API");
}

var controllers = app.MapControllers();
if (authenticationEnabled)
{
    controllers.RequireAuthorization();
}
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = registration => registration.Tags.Contains("ready") });
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });

await app.RunAsync();

public partial class Program { private Program() { } }
