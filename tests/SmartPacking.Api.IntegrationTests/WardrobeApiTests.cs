using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartPacking.Api.DependencyInjection;
using SmartPacking.Application;
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
    [Fact]
    public async Task UserAddressRequiresCityWhenAnyAddressFieldIsProvided()
    {
        var response = await client.PutAsJsonAsync("/api/me", new { name = "Lucía", street = "Calle Mayor, 12" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        var errors = problem?.Errors ?? new Dictionary<string, string[]>();
        errors.Should().ContainKey("city");
    }

    [Fact]
    public async Task CreatingTripPersistsAnExplainableTransportPlan()
    {
        var request = new SaveTripRequest("Roma", new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 14), 15, 24, [(int)Style.Casual], null, 10000, true, TransportTypes: [(int)TransportType.Plane], Origin: "Ocaña, Toledo");

        var response = await client.PostAsJsonAsync("/api/trips", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var trip = await response.Content.ReadFromJsonAsync<TripResponse>();
        trip!.TransportPlan.Should().NotBeNull();
        trip.TransportPlan!.Legs.Should().Contain(leg => leg.From == "Ocaña, Toledo" && leg.To == "Madrid-Barajas");
        trip.TransportPlan.Legs.Should().Contain(leg => leg.Type == (int)TransportType.Plane && leg.To == "Roma Fiumicino");
    }

    private SqliteConnection databaseConnection = null!;
    private WebApplicationFactory<Program> factory = null!;
    private HttpClient client = null!;
    private TestGarmentRecognizer recognizer = null!;

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
                services.RemoveAll<IExternalIdentityAccessor>();
                services.AddDbContext<SmartPackingDbContext>(options => options.UseSqlite(databaseConnection));
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddScoped<IExternalIdentityAccessor, TestExternalIdentityAccessor>();
                services.RemoveAll<IGarmentRecognizer>();
                services.AddSingleton<TestGarmentRecognizer>();
                services.AddSingleton<IGarmentRecognizer>(provider => provider.GetRequiredService<TestGarmentRecognizer>());
            });
        });
        client = factory.CreateClient();
        recognizer = factory.Services.GetRequiredService<TestGarmentRecognizer>();
    }

    [Fact]
    public async Task ValidGarmentPhotoReturnsTheProposalFromTheRecognizer()
    {
        using var form = CreatePhotoForm("photo.jpg", "image/jpeg");

        var response = await client.PostAsync("/api/wardrobe/recognition", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var proposal = await response.Content.ReadFromJsonAsync<GarmentRecognitionSuggestion>();
        proposal!.Category.Should().Be("Jersey");
        recognizer.Calls.Should().Be(1);
    }

    [Fact]
    public async Task InvalidGarmentPhotoIsRejectedWithoutCallingTheRecognizer()
    {
        using var form = CreatePhotoForm("photo.png", "image/png");

        var response = await client.PostAsync("/api/wardrobe/recognition", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        recognizer.Calls.Should().Be(0);
    }

    [Fact]
    public async Task UnavailableRecognizerReturnsAServiceUnavailableProblem()
    {
        recognizer.Exception = new HttpRequestException("Gemini no disponible");
        using var form = CreatePhotoForm("photo.jpg", "image/jpeg");

        var response = await client.PostAsync("/api/wardrobe/recognition", form);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task RecognitionProposalDoesNotPersistAClothingItemUntilTheUserConfirmsIt()
    {
        using var form = CreatePhotoForm("photo.jpg", "image/jpeg");
        (await client.PostAsync("/api/wardrobe/recognition", form)).EnsureSuccessStatusCode();

        var wardrobe = await client.GetFromJsonAsync<ApiResult<ClothingItemResponse[]>>("/api/wardrobe");

        wardrobe!.Data.Should().BeEmpty("la propuesta solo rellena el editor y requiere guardar manualmente");
    }

    [Fact]
    public async Task AuthenticatedUserMustCompleteOnboardingBeforeUsingTheirProfile()
    {
        var currentUser = await client.GetFromJsonAsync<UserProfile>("/api/me");
        currentUser.Should().NotBeNull();
        currentUser!.IsOnboarded.Should().BeFalse();
        currentUser.Name.Should().BeEmpty();

        var response = await client.PostAsJsonAsync("/api/me/onboarding", new { name = "Lucía" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var completedUser = await response.Content.ReadFromJsonAsync<UserProfile>();
        completedUser.Should().Be(new UserProfile(currentUser.Id, "Lucía", true));

        var profiles = await client.GetFromJsonAsync<FamilyProfile[]>("/api/profiles");
        profiles.Should().ContainSingle(profile => profile.Id == currentUser.Id && profile.Name == "Lucía");
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

    [Fact]
    public async Task UserCannotModifyAnotherUsersWardrobeOrPackingList()
    {
        client.DefaultRequestHeaders.Add("X-Test-User", "user-a");
        (await client.PostAsJsonAsync("/api/me/onboarding", new { name = "Usuario A" })).EnsureSuccessStatusCode();
        var clothing = new UpsertClothingItemRequest("Chaqueta privada", ClothingType.Jacket, Season.AllYear, "Negro", 5, true, Style.Casual, 450, true, true, 70, [], null);
        var createClothing = await client.PostAsJsonAsync("/api/wardrobe", clothing);
        createClothing.StatusCode.Should().Be(HttpStatusCode.Created);
        var clothingId = (await createClothing.Content.ReadFromJsonAsync<ApiResult<ClothingItemResponse>>())!.Data.Id;

        var tripResponse = await client.PostAsJsonAsync("/api/trips", new
        {
            destination = "Bilbao",
            startDate = new DateOnly(2026, 10, 10),
            endDate = new DateOnly(2026, 10, 12),
            minimumTemperatureCelsius = 10,
            maximumTemperatureCelsius = 18,
            activities = new[] { Style.Casual }
        });
        var tripId = (await tripResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var profile = (await client.GetFromJsonAsync<FamilyProfile[]>($"/api/trips/{tripId}/profiles"))!.Single();
        var plan = await client.GetFromJsonAsync<ProfileTripPackingPlan>($"/api/trips/{tripId}/profiles/{profile.Id}/packing-list");
        plan.Should().NotBeNull();

        client.DefaultRequestHeaders.Remove("X-Test-User");
        client.DefaultRequestHeaders.Add("X-Test-User", "user-b");
        (await client.DeleteAsync($"/api/wardrobe/{clothingId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync($"/api/profile-packing-lists/{plan!.Plan.PackingListId}/items/{clothingId}", new { isPacked = true })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UserCanUpdateAndPermanentlyDeleteTheirLocalData()
    {
        var currentUser = await client.GetFromJsonAsync<UserProfile>("/api/me");
        var update = await client.PutAsJsonAsync("/api/me", new { name = "María", street = "Calle Mayor, 12", postalCode = "45300", city = "Ocaña", region = "Toledo, España" });
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedUser = await update.Content.ReadFromJsonAsync<UserProfile>();
        updatedUser!.Name.Should().Be("María");
        updatedUser.Address.Should().Be("Calle Mayor, 12 · 45300 · Ocaña · Toledo, España");
        updatedUser.AddressDetails.Should().Be(new UserAddress("Calle Mayor, 12", "45300", "Ocaña", "Toledo, España"));

        (await client.PostAsJsonAsync("/api/wardrobe", new UpsertClothingItemRequest("Abrigo", ClothingType.Jacket, Season.Winter, "Gris", 7, true, Style.Casual, 600, true, true, 70, [], null))).EnsureSuccessStatusCode();
        (await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/me") { Content = JsonContent.Create(new { confirmation = "ELIMINAR" }) })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var recreatedUser = await client.GetFromJsonAsync<UserProfile>("/api/me");
        recreatedUser.Should().NotBeNull();
        recreatedUser!.Id.Should().NotBe(currentUser!.Id);
        recreatedUser.IsOnboarded.Should().BeFalse();
        (await client.GetFromJsonAsync<ApiResult<ClothingItemResponse[]>>("/api/wardrobe"))!.Data.Should().BeEmpty();
    }

    [Fact]
    public void AzureSqlConfigurationUsesTheSqlServerProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "SqlServer",
                ["ConnectionStrings:SmartPacking"] = "Server=tcp:smartpacking.database.windows.net,1433;Initial Catalog=smartpacking;Encrypt=True"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddSmartPackingPersistence(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SmartPackingDbContext>();

        database.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.SqlServer");
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
        await databaseConnection.DisposeAsync();
    }

    private static MultipartFormDataContent CreatePhotoForm(string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var photo = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xD9]);
        photo.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(photo, "photo", fileName);
        return form;
    }

    private sealed class TestGarmentRecognizer : IGarmentRecognizer
    {
        public int Calls { get; private set; }
        public Exception? Exception { get; set; }

        public Task<GarmentRecognitionSuggestion> RecognizeAsync(Stream photo, string contentType, CancellationToken cancellationToken)
        {
            Calls++;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(new GarmentRecognitionSuggestion("Jersey", "Verde", "Lana", ["Otoño", "Invierno"], "Casual", 350, ["Turismo"]));
        }
    }

    private sealed class TestExternalIdentityAccessor(IHttpContextAccessor httpContextAccessor) : IExternalIdentityAccessor
    {
        public ExternalIdentity? GetCurrent()
        {
            var subject = httpContextAccessor.HttpContext?.Request.Headers["X-Test-User"].FirstOrDefault() ?? "auth0|integration-user";
            return new ExternalIdentity("https://issuer.example", subject, "Perfil externo", true);
        }
    }
}
