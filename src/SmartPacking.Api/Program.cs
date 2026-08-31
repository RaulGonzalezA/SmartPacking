using Microsoft.EntityFrameworkCore;
using Azure.Storage.Blobs;
using System.Globalization;
using Microsoft.Extensions.Http.Resilience;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using SmartPacking.Api;
using SmartPacking.Application;
using SmartPacking.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var seqUrl = builder.Configuration["Observability:SeqUrl"];
var otlpEndpoint = builder.Configuration["Observability:OtlpEndpoint"];
builder.Host.UseSerilog((_, _, loggerConfiguration) =>
{
    loggerConfiguration.Enrich.FromLogContext().WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);
    if (!string.IsNullOrWhiteSpace(seqUrl))
    {
        loggerConfiguration.WriteTo.Seq(seqUrl, formatProvider: CultureInfo.InvariantCulture);
    }
});
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("SmartPacking.Api"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(exporter => exporter.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(exporter => exporter.Endpoint = new Uri(otlpEndpoint));
        }
    });
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SmartPackingDbContext>("database", tags: ["ready"])
    .AddCheck<StorageHealthCheck>("storage", tags: ["ready"]);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddScoped<PackingListService>();
builder.Services.AddScoped<ProfilePackingListService>();
builder.Services.AddScoped<ISmartPackingStore, EfSmartPackingStore>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<OpenMeteoWeatherProvider>(client => client.Timeout = TimeSpan.FromSeconds(10)).AddStandardResilienceHandler();
var connectionString = builder.Configuration.GetConnectionString("SmartPacking") ?? "Data Source=smartpacking.db";
builder.Services.AddDbContext<SmartPackingDbContext>(options =>
{
    if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure();
            npgsqlOptions.MigrationsAssembly("SmartPacking.Infrastructure.PostgreSql");
        });
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});
var blobConnectionString = builder.Configuration["Storage:ConnectionString"];
if (string.IsNullOrWhiteSpace(blobConnectionString))
{
    builder.Services.AddSingleton<IPhotoStorage, LocalPhotoStorage>();
}
else
{
    builder.Services.AddSingleton(new BlobServiceClient(blobConnectionString));
    builder.Services.AddSingleton<IPhotoStorage, BlobPhotoStorage>();
}

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar", options => options.Title = "SmartPacking API");
}

app.MapControllers();
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = registration => registration.Tags.Contains("ready") });
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });

await app.RunAsync();

public partial class Program { private Program() { } }
