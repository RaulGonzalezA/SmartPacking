using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SmartPacking.Api.Contracts;
using SmartPacking.Contracts;
using SmartPacking.Domain;
using SmartPacking.Infrastructure;
using Xunit;

namespace SmartPacking.Api.IntegrationTests;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "xUnit invoca IAsyncLifetime.DisposeAsync para liberar los recursos de cada prueba.")]
public sealed class WardrobeApiTests : IAsyncLifetime
{
    private SqliteConnection databaseConnection = null!;
    private WebApplicationFactory<Program> factory = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        databaseConnection = new SqliteConnection("Data Source=:memory:");
        await databaseConnection.OpenAsync();
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<SmartPackingDbContext>>();
                services.AddDbContext<SmartPackingDbContext>(options => options.UseSqlite(databaseConnection));
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        });
        client = factory.CreateClient();
    }

    [Fact]
    public async Task WardrobeDeleteAndRestorePreservesItemThroughApi()
    {
        var request = new UpsertClothingItemRequest("Camisa integración", ClothingType.TShirt, Season.AllYear, "Azul", 2, false, Style.Casual, 150, true, true, 70, [], null);
        var create = await client.PostAsJsonAsync("/api/wardrobe", request);
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdResult = await create.Content.ReadFromJsonAsync<ApiResult<ClothingItemResponse>>();
        createdResult.Should().NotBeNull();
        var item = createdResult!.Data;

        (await client.DeleteAsync($"/api/wardrobe/{item.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var deleted = await client.GetFromJsonAsync<ApiResult<ClothingItemResponse[]>>("/api/wardrobe/deleted");
        deleted!.Data.Should().Contain(candidate => candidate.Id == item.Id && candidate.IsDeleted);

        (await client.PostAsync($"/api/wardrobe/{item.Id}/restore", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var active = await client.GetFromJsonAsync<ApiResult<ClothingItemResponse[]>>("/api/wardrobe");
        active!.Data.Should().Contain(candidate => candidate.Id == item.Id && !candidate.IsDeleted);
    }

    [Fact]
    public async Task DatabaseMigrationsProfilesAndPackingListsAreAvailablePerPerson()
    {
        (await client.GetAsync("/api/me")).EnsureSuccessStatusCode();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<SmartPackingDbContext>();
            var appliedMigrations = await database.Database.GetAppliedMigrationsAsync();
            appliedMigrations.Should().NotBeEmpty();
        }

        var tripResponse = await client.PostAsJsonAsync("/api/trips", new
        {
            destination = "Lisboa",
            startDate = new DateOnly(2026, 9, 15),
            endDate = new DateOnly(2026, 9, 18),
            minimumTemperatureCelsius = 18,
            maximumTemperatureCelsius = 27,
            activities = new[] { Style.Casual }
        });
        tripResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var tripId = (await tripResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var profileResponse = await client.PostAsJsonAsync("/api/profiles", new { name = "Perfil integración" });
        profileResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var profileId = (await profileResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var assignResponse = await client.PutAsJsonAsync($"/api/trips/{tripId}/profiles", new { profileIds = new[] { profileId } });
        assignResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var packingList = await client.GetAsync($"/api/trips/{tripId}/profiles/{profileId}/packing-list");
        packingList.StatusCode.Should().Be(HttpStatusCode.OK);

        var checklist = await client.GetFromJsonAsync<ChecklistItem[]>($"/api/trips/{tripId}/profiles/{profileId}/checklist");
        checklist.Should().NotBeNull();
        checklist.Should().Contain(item => item.Category == ChecklistCategory.Toiletries && item.Name.Contains("pasta", StringComparison.OrdinalIgnoreCase));
        checklist.Should().OnlyContain(item => item.ProfileId == profileId);
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
        await databaseConnection.DisposeAsync();
    }
}
