using Microsoft.EntityFrameworkCore;
using Azure.Storage.Blobs;
using SmartPacking.Api;
using SmartPacking.Application;
using SmartPacking.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddScoped<PackingListService>();
builder.Services.AddScoped<ProfilePackingListService>();
builder.Services.AddScoped<ISmartPackingStore, EfSmartPackingStore>();
builder.Services.AddHttpClient<OpenMeteoWeatherProvider>(client => client.Timeout = TimeSpan.FromSeconds(10));
var connectionString = builder.Configuration.GetConnectionString("SmartPacking") ?? "Data Source=smartpacking.db";
builder.Services.AddDbContext<SmartPackingDbContext>(options =>
{
    if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString);
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
        await dbContext.Database.EnsureCreatedAsync();
    }
    await scope.ServiceProvider.GetRequiredService<ISmartPackingStore>().SeedAsync(CancellationToken.None);
}
app.UseStaticFiles();
app.UseExceptionHandler();
app.UseAntiforgery();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/scalar", options => options.Title = "SmartPacking API");
}

app.MapControllers();

await app.RunAsync();

public partial class Program { private Program() { } }
