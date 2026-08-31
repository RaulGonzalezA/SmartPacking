using Microsoft.EntityFrameworkCore;
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
builder.Services.AddDbContext<SmartPackingDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("SmartPacking") ?? "Data Source=smartpacking.db"));

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    await SmartPackingDatabaseMigrator.MigrateAsync(scope.ServiceProvider.GetRequiredService<SmartPackingDbContext>(), CancellationToken.None);
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
