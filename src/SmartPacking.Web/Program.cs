using Microsoft.AspNetCore.DataProtection;
using SmartPacking.Application;
using SmartPacking.Web;
using SmartPacking.Api.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "data-protection-keys")));
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("La configuración Api:BaseUrl es obligatoria.");
builder.Services.AddHttpClient<SmartPackingApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddScoped<IWebSmartPackingClient>(provider => provider.GetRequiredService<SmartPackingApiClient>());

var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
await app.RunAsync();
