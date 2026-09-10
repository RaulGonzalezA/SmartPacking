using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using SmartPacking.Application;
using SmartPacking.Domain;
using SmartPacking.Web.Components;
using Xunit;

namespace SmartPacking.Web.Tests;

public sealed class PanelRenderingTests : BunitContext
{
    [Fact]
    public async Task OnboardingPanelSendsTheProvidedName()
    {
        string? completedName = null;
        var cut = Render<OnboardingPanel>(parameters => parameters
            .Add(component => component.Completed, EventCallback.Factory.Create<string>(this, value => completedName = value)));

        await cut.Find("input").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "  Lucía  " });
        await cut.Find("button").ClickAsync();

        completedName.Should().Be("Lucía");
    }

    [Fact]
    public async Task AccountPanelRequiresAnExplicitConfirmationBeforeDeletingData()
    {
        var deleted = false;
        var cut = Render<AccountPanel>(parameters => parameters
            .Add(component => component.User, new UserProfile(Guid.NewGuid(), "Lucía", true))
            .Add(component => component.AccountDeleted, EventCallback.Factory.Create(this, () => deleted = true)));

        await cut.FindAll("button").Single(button => button.TextContent == "Mi cuenta").ClickAsync();
        cut.FindAll("button").Single(button => button.TextContent == "Eliminar mis datos").HasAttribute("disabled").Should().BeTrue();
        await cut.Find("#delete-account").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "ELIMINAR" });
        await cut.FindAll("button").Single(button => button.TextContent == "Eliminar mis datos").ClickAsync();

        deleted.Should().BeTrue();
    }

    [Fact]
    public async Task AccountPanelShowsTheSameCityRequirementAsTheApi()
    {
        var cut = Render<AccountPanel>(parameters => parameters
            .Add(component => component.User, new UserProfile(Guid.NewGuid(), "Lucía", true)));

        await cut.FindAll("button").Single(button => button.TextContent == "Mi cuenta").ClickAsync();
        await cut.Find("#account-street").ChangeAsync(new ChangeEventArgs { Value = "Calle Mayor, 12" });
        await cut.FindAll("button").Single(button => button.TextContent == "Guardar perfil").ClickAsync();

        cut.Markup.Should().Contain("La población es obligatoria cuando se informa una dirección.");
    }

    [Fact]
    public async Task TripsPanelSavesAnEditedTraveller()
    {
        var profile = new FamilyProfile(Guid.NewGuid(), "Ana", false, "Gafas", "Ninguna");
        FamilyProfile? saved = null;
        var cut = Render<TripsPanel>(parameters => parameters
            .Add(component => component.IsActive, true)
            .Add(component => component.Profiles, new[] { profile })
            .Add(component => component.TravellerUpdated, EventCallback.Factory.Create<FamilyProfile>(this, value => saved = value)));

        await cut.FindAll("button").Single(button => button.TextContent.StartsWith("Editar", StringComparison.Ordinal)).ClickAsync();
        await cut.FindAll("button").Single(button => button.TextContent == "Guardar viajero").ClickAsync();

        saved.Should().Be(profile);
    }

    [Fact]
    public void TripFormInputBuildsOneActivityForEachTripDayAndAppliesLuggageDefaults()
    {
        var input = new TripFormInput
        {
            StartDate = new DateOnly(2026, 9, 10),
            EndDate = new DateOnly(2026, 9, 12),
            LuggageType = LuggageType.Checked
        };

        input.ApplyLuggageDefaults();
        input.EnsureDayPlans();
        input.DayPlans[0].SetActivity(TripActivity.Beach, true);
        input.DayPlans[0].SetActivity(TripActivity.Hiking, true);
        input.DayPlans[0].SetActivity(TripActivity.Business, true);

        input.DayPlans.Should().HaveCount(3);
        input.DayPlans.Skip(1).Should().OnlyContain(day => day.Activities.Count == 1 && day.Activities.Contains(TripActivity.Sightseeing));
        input.LuggageAllowanceGrams.Should().Be(23000);
        input.ToTrip(Guid.NewGuid()).DayPlansOrEmpty.Should().Contain(plan => plan.Activities.Count == 3 && !plan.Activities.Contains(TripActivity.Business));
    }

    [Fact]
    public void TripFormInputAllowsMultipleLuggagesAndOnlyAppliesAirlineLimitsToCabinLuggage()
    {
        var input = new TripFormInput { AirlineCode = "iberia" };
        input.SetTransport(TransportType.Plane, true);
        input.AddLuggage();
        var checkedLuggage = input.Luggages.Single(luggage => luggage.Type == LuggageType.Checked);
        checkedLuggage.AllowanceGrams = 18000;

        input.ApplyAirlineRule();

        input.ToTrip(Guid.NewGuid()).TransportTypesOrEmpty.Should().Contain(TransportType.Plane);
        input.ToTrip(Guid.NewGuid()).LuggagesOrDefault.Should().ContainSingle(luggage => luggage.Type == LuggageType.Cabin && luggage.AllowanceGrams == 10000);
        input.ToTrip(Guid.NewGuid()).LuggagesOrDefault.Should().ContainSingle(luggage => luggage.Type == LuggageType.Checked && luggage.AllowanceGrams == 18000);
    }

    [Fact]
    public void TripsPanelWithoutTripsExplainsHowToStartAndDisablesTripActions()
    {
        var cut = Render<TripsPanel>(parameters => parameters
            .Add(component => component.IsActive, true));

        cut.Markup.Should().Contain("Crea un viaje para empezar a organizarlo.");
        cut.FindAll("button").Should().Contain(button => button.TextContent.Contains("Nuevo viaje", StringComparison.Ordinal));
    }

    [Fact]
    public void PackingPanelWithoutPlanExplainsWhatTheUserNeedsToDo()
    {
        var cut = Render<PackingPanel>(parameters => parameters
            .Add(component => component.IsActive, true));

        cut.Markup.Should().Contain("Selecciona un viaje y un viajero para preparar la maleta.");
        cut.Markup.Should().Contain("Tu maleta está esperando");
    }

    [Fact]
    public void WardrobePanelWithoutItemsOffersTheFirstGarmentCallToAction()
    {
        var cut = Render<WardrobePanel>(parameters => parameters
            .Add(component => component.IsActive, true));

        cut.Markup.Should().Contain("Aún no has añadido prendas");
        cut.FindAll("button").Should().Contain(button => button.TextContent.Contains("Añadir mi primera prenda", StringComparison.Ordinal));
    }

    [Fact]
    public void GarmentRecognitionPreviewOnlyDisplaysAnErrorWhenItReceivesOne()
    {
        var withoutError = Render<GarmentRecognitionPreview>();
        var withError = Render<GarmentRecognitionPreview>(parameters => parameters
            .Add(component => component.Error, "No se pudo reconocer la prenda."));

        withoutError.Markup.Should().BeEmpty();
        withError.Markup.Should().Contain("No se pudo reconocer la prenda.");
    }

    [Fact]
    public void ApiOperationResultClassifiesValidationFailuresWithTheirFieldErrors()
    {
        var result = ApiOperationResult.FromException(new ApiProblemException(
            StatusCodes.Status400BadRequest,
            "Datos no válidos",
            null,
            new Dictionary<string, string[]> { ["name"] = ["Indica un nombre."] }));

        result.Status.Should().Be(ApiOperationStatus.ValidationError);
        result.Errors.Should().ContainKey("name");
    }

    [Fact]
    public async Task TripsPanelAllowsRetryingUnavailableWeather()
    {
        var retried = false;
        var trip = new Trip(Guid.NewGuid(), "Roma", new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), 12, 26, [Style.Casual]);
        var cut = Render<TripsPanel>(parameters => parameters
            .Add(component => component.IsActive, true)
            .Add(component => component.Trips, new[] { trip })
            .Add(component => component.SelectedTripId, trip.Id)
            .Add(component => component.WeatherRefreshRequested, EventCallback.Factory.Create(this, () => retried = true)));

        cut.Markup.Should().Contain("La previsión aún no está disponible");
        await cut.FindAll("button").Single(button => button.TextContent.Contains("Reintentar previsión", StringComparison.Ordinal)).ClickAsync();

        retried.Should().BeTrue();
    }

    [Fact]
    public void TripsPanelDoesNotShowTheUnavailableStateWhenForecastExists()
    {
        var trip = new Trip(Guid.NewGuid(), "Roma", new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), 12, 26, [Style.Casual]);
        var forecast = new TripWeatherForecast(
            "Roma",
            14,
            25,
            20,
            trip.StartDate,
            trip.EndDate,
            [new DailyTripForecast(trip.StartDate, 14, 25, 20, 1)]);

        var cut = Render<TripsPanel>(parameters => parameters
            .Add(component => component.IsActive, true)
            .Add(component => component.Trips, new[] { trip })
            .Add(component => component.SelectedTripId, trip.Id)
            .Add(component => component.Weather, forecast));

        cut.Markup.Should().Contain("Previsión del viaje");
        cut.Markup.Should().NotContain("La previsión aún no está disponible");
    }

    [Fact]
    public void PackingPanelShowsTheThreeHighestPreparationPriorities()
    {
        var trip = new Trip(Guid.NewGuid(), "Roma", new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), 12, 26, [Style.Casual]);
        var profile = new FamilyProfile(Guid.NewGuid(), "Ana");
        var checklist = new[]
        {
            new ChecklistItem(Guid.NewGuid(), trip.Id, ChecklistCategory.Documents, "Pasaporte", false, profile.Id),
            new ChecklistItem(Guid.NewGuid(), trip.Id, ChecklistCategory.Toiletries, "Cepillo de dientes", false, profile.Id),
            new ChecklistItem(Guid.NewGuid(), trip.Id, ChecklistCategory.Health, "Analgésico", false, profile.Id)
        };
        var plan = new ProfileTripPackingPlan(profile, new TripPackingPlan(trip, Guid.NewGuid(), [], 0));

        var cut = Render<PackingPanel>(parameters => parameters
            .Add(component => component.IsActive, true)
            .Add(component => component.Plan, plan)
            .Add(component => component.SelectedProfileId, profile.Id)
            .Add(component => component.Checklist, checklist));

        cut.Markup.Should().Contain("Las 3 tareas más importantes");
        cut.FindAll(".preparation-priorities li").Should().HaveCount(3);
    }

    [Fact]
    public void TripsPanelMarksPreviouslyUsedClothing()
    {
        var clothingId = Guid.NewGuid();
        var clothing = new ClothingItem(clothingId, "Chaqueta", ClothingType.Jacket, Season.AllYear, "Azul", 2, false, Style.Casual, 800, true, true, 70, [], false);

        var cut = Render<TripsPanel>(parameters => parameters
            .Add(component => component.IsActive, true)
            .Add(component => component.SelectedTripId, Guid.NewGuid())
            .Add(component => component.IsCompleted, true)
            .Add(component => component.Wardrobe, new[] { clothing })
            .Add(component => component.UsageItemIds, new HashSet<Guid> { clothingId })
            .Add(component => component.UsedItemIds, new HashSet<Guid> { clothingId }));

        cut.Find("input[type=checkbox]").HasAttribute("checked").Should().BeTrue();
    }

    [Fact]
    public async Task TripsPanelNotifiesTheSelectedTrip()
    {
        var expectedTripId = Guid.NewGuid();
        Guid actualTripId = Guid.Empty;
        var trip = new Trip(expectedTripId, "Madrid", new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12), 12, 26, [Style.Casual]);

        var cut = Render<TripsPanel>(parameters => parameters
            .Add(component => component.IsActive, true)
            .Add(component => component.Trips, new[] { trip })
            .Add(component => component.SelectedTripChanged, EventCallback.Factory.Create<Guid>(this, id => actualTripId = id)));

        await cut.Find("select").ChangeAsync(expectedTripId.ToString());

        actualTripId.Should().Be(expectedTripId);
    }

    [Fact]
    public async Task TripsPanelSavesTheChangedUsageSelection()
    {
        var clothingId = Guid.NewGuid();
        IReadOnlyCollection<Guid>? savedIds = null;
        var clothing = new ClothingItem(clothingId, "Chaqueta", ClothingType.Jacket, Season.AllYear, "Azul", 2, false, Style.Casual, 800, true, true, 70, [], false);

        var cut = Render<TripsPanel>(parameters => parameters
            .Add(component => component.IsActive, true)
            .Add(component => component.SelectedTripId, Guid.NewGuid())
            .Add(component => component.IsCompleted, true)
            .Add(component => component.Wardrobe, new[] { clothing })
            .Add(component => component.UsageItemIds, new HashSet<Guid> { clothingId })
            .Add(component => component.UsageSaved, EventCallback.Factory.Create<IReadOnlyCollection<Guid>>(this, ids => savedIds = ids)));

        await cut.Find("input[type=checkbox]").ChangeAsync(true);
        var saveButton = cut.FindAll("button").Single(button => button.TextContent.Contains("Guardar uso real", StringComparison.Ordinal));
        await saveButton.ClickAsync();

        savedIds.Should().ContainSingle().Which.Should().Be(clothingId);
    }
}
