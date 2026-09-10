using FluentAssertions;
using SmartPacking.Application;
using SmartPacking.Domain;
using Xunit;

namespace SmartPacking.Application.Tests;

public sealed class PackingRecommendationServiceTests
{
    [Fact]
    public void TransportPlannerBuildsAirportTransferForOcanaToRome()
    {
        var plan = TransportPlanner.Build("Ocaña, Toledo", "Roma", [TransportType.Plane]);
        var legs = plan.Legs.ToArray();

        plan.Legs.Should().HaveCount(2);
        legs[0].Should().BeEquivalentTo(new TransportLeg(TransportType.Car, "Ocaña, Toledo", "Madrid-Barajas", 55, "Traslado recomendado al aeropuerto de salida."));
        legs[1].Type.Should().Be(TransportType.Plane);
        legs[1].To.Should().Be("Roma Fiumicino");
    }

    [Fact]
    public void TransportPlannerBuildsRailPlanFromMedinaDelCampoToMadrid()
    {
        var plan = TransportPlanner.Build("Medina del Campo", "Madrid", [TransportType.Train]);
        var leg = plan.Legs.Single();

        plan.Legs.Should().ContainSingle();
        leg.Should().BeEquivalentTo(new TransportLeg(TransportType.Train, "Medina del Campo", "Madrid", 75, "Conexión ferroviaria orientativa; confirma transbordos y horarios."));
    }

    [Fact]
    public void TransportAdvisorExplainsAirportTransferFromOcanaToRome()
    {
        var options = TransportAdvisor.GetOptions("Ocaña, Toledo", "Roma");

        options.Should().Contain(option => option.Type == TransportType.Car && option.IsAvailable);
        options.Should().Contain(option => option.Type == TransportType.Bus && option.IsAvailable);
        options.Should().Contain(option => option.Type == TransportType.Plane && option.IsAvailable && option.Reason.Contains("Madrid-Barajas"));
        options.Should().Contain(option => option.Type == TransportType.Train && !option.IsAvailable);
    }

    [Fact]
    public void TransportAdvisorMarksTrainAsAvailableFromMedinaDelCampo()
    {
        var options = TransportAdvisor.GetOptions("Medina del Campo", "Madrid");

        options.Should().Contain(option => option.Type == TransportType.Train && option.IsAvailable);
    }

    [Fact]
    public void AnalyzeWhenLuggageIsOverweightSuggestsALighterReplacement()
    {
        var profile = new FamilyProfile(Guid.NewGuid(), "Ana");
        var heavyJacket = new ClothingItem(Guid.NewGuid(), "Abrigo", ClothingType.Jacket, Season.Winter, "Azul", 8, false, Style.Casual, 900, true, true, 50, [], false, profile.Id);
        var lightJacket = heavyJacket with { Id = Guid.NewGuid(), Name = "Chaqueta ligera", WeightGrams = 350 };
        var trip = DemoData.RomeTrip with { LuggageAllowanceGrams = 700 };
        var plan = new ProfileTripPackingPlan(profile, new TripPackingPlan(trip, Guid.NewGuid(), [new(new RecommendedItem(heavyJacket, 30, []), false)], 900));

        var insights = PackingInsightsService.Analyze(plan, [plan], [heavyJacket, lightJacket], null);

        insights.WeightSuggestions.Should().ContainSingle();
        insights.WeightSuggestions[0].Replacement.Should().Be(lightJacket);
        insights.WeightSuggestions[0].SavedGrams.Should().Be(550);
    }

    [Fact]
    public void AnalyzeFamilyPlansIdentifiesSharedItemsAndDuplicates()
    {
        var ana = new FamilyProfile(Guid.NewGuid(), "Ana");
        var leo = new FamilyProfile(Guid.NewGuid(), "Leo");
        var shared = new ClothingItem(Guid.NewGuid(), "Protector solar", ClothingType.Accessory, Season.Summer, "Blanco", 1, false, Style.Casual, 200, true, true, 70, [], false);
        var anaShirt = new ClothingItem(Guid.NewGuid(), "Camiseta Ana", ClothingType.TShirt, Season.AllYear, "Azul", 2, false, Style.Casual, 180, true, true, 70, [], false, ana.Id);
        var leoShirt = anaShirt with { Id = Guid.NewGuid(), Name = "Camiseta Leo", OwnerProfileId = leo.Id, WeightGrams = 160 };
        var trip = DemoData.RomeTrip;
        var anaPlan = new ProfileTripPackingPlan(ana, new TripPackingPlan(trip, Guid.NewGuid(), [new(new RecommendedItem(shared, 1, []), false), new(new RecommendedItem(anaShirt, 1, []), false)], 380));
        var leoPlan = new ProfileTripPackingPlan(leo, new TripPackingPlan(trip, Guid.NewGuid(), [new(new RecommendedItem(shared, 1, []), false), new(new RecommendedItem(leoShirt, 1, []), false)], 360));

        var insights = PackingInsightsService.Analyze(anaPlan, [anaPlan, leoPlan], [shared, anaShirt, leoShirt], null);

        insights.SharedItems.Should().ContainSingle();
        insights.Duplicates.Should().ContainSingle();
    }

    [Fact]
    public void RecommendWithLiveColdForecastAddsJacketThatWasNotNeededByTheOriginalPlan()
    {
        var trip = DemoData.RomeTrip with { MinimumTemperatureCelsius = 22, MaximumTemperatureCelsius = 28 };
        var forecast = new TripWeatherForecast("Roma", 8, 14, 20, trip.StartDate, trip.EndDate, []);

        var result = PackingRecommendationService.Recommend(trip, DemoData.Wardrobe, forecast);

        result.Items.Select(item => item.Item.Type).Should().Contain(ClothingType.Jacket);
    }

    [Fact]
    public void AnalyzeLongTripCreatesExplainableLaundryAndReusePlan()
    {
        var profile = new FamilyProfile(Guid.NewGuid(), "Ana");
        var shirt = new ClothingItem(Guid.NewGuid(), "Camiseta azul", ClothingType.TShirt, Season.AllYear, "Azul", 2, false, Style.Casual, 180, true, true, 90, [], false, profile.Id);
        var trousers = new ClothingItem(Guid.NewGuid(), "Pantalón", ClothingType.Trousers, Season.AllYear, "Negro", 3, false, Style.Casual, 500, true, true, 80, [], false, profile.Id);
        var trip = DemoData.RomeTrip with { EndDate = DemoData.RomeTrip.StartDate.AddDays(7) };
        var plan = new ProfileTripPackingPlan(profile, new TripPackingPlan(trip, Guid.NewGuid(), [new(new RecommendedItem(shirt, 80, []), false), new(new RecommendedItem(trousers, 70, []), false)], 680));

        var insights = PackingInsightsService.Analyze(plan, [plan], [shirt, trousers], null);

        insights.LaundryReuse.Should().ContainSingle();
        insights.LaundryReuse[0].EstimatedSavedGrams.Should().Be(680);
    }

    [Fact]
    public void RecommendWhenItemIsDirtyDoesNotIncludeItInPackingList()
    {
        var dirtyFavourite = DemoData.Wardrobe[0] with { IsClean = false };
        var wardrobe = DemoData.Wardrobe.Select(item => item.Id == dirtyFavourite.Id ? dirtyFavourite : item);

        var result = PackingRecommendationService.Recommend(DemoData.RomeTrip, wardrobe);

        result.Items.Select(item => item.Item.Id).Should().NotContain(dirtyFavourite.Id);
    }

    [Fact]
    public void RecommendForHotTripIncludesSummerShoesAndShorts()
    {
        var result = PackingRecommendationService.Recommend(DemoData.RomeTrip, DemoData.Wardrobe);

        result.Items.Select(item => item.Item.Type).Should().Contain([ClothingType.Shorts, ClothingType.Sandals]);
        result.TotalWeightGrams.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RecommendForFormalActivityPrefersFormalTrousers()
    {
        var formalTrousers = DemoData.Wardrobe.Single(item => item.Type == ClothingType.Trousers) with { Style = Style.Formal, PreferenceScore = 70, CombinesWith = [] };
        var casualTrousers = formalTrousers with { Id = Guid.NewGuid(), Name = "Pantalón casual", Style = Style.Casual };
        var wardrobe = DemoData.Wardrobe.Where(item => item.Type != ClothingType.Trousers).Append(formalTrousers).Append(casualTrousers);
        var trip = DemoData.RomeTrip with { Activities = [Style.Formal] };

        var result = PackingRecommendationService.Recommend(trip, wardrobe);

        result.Items.First(item => item.Item.Type == ClothingType.Trousers).Item.Style.Should().Be(Style.Formal);
    }

    [Fact]
    public void RecommendTreatsDailyEssentialsAndSwimwearAsGarments()
    {
        var trip = DemoData.RomeTrip with { EndDate = DemoData.RomeTrip.StartDate.AddDays(4), Activities = [Style.Sport] };
        var underwear = new ClothingItem(Guid.NewGuid(), "Ropa interior", ClothingType.Underwear, Season.AllYear, "Blanco", 1, false, Style.Casual, 70, true, true, 50, []);
        var socks = new ClothingItem(Guid.NewGuid(), "Calcetines", ClothingType.Socks, Season.AllYear, "Blanco", 1, false, Style.Casual, 50, true, true, 50, []);
        var swimwear = new ClothingItem(Guid.NewGuid(), "Bañador", ClothingType.Swimwear, Season.Summer, "Azul", 1, false, Style.Sport, 120, true, true, 50, []);

        var result = PackingRecommendationService.Recommend(trip, DemoData.Wardrobe.Append(underwear).Append(socks).Append(swimwear));

        result.Items.Select(item => item.Item.Type).Should().Contain([ClothingType.Underwear, ClothingType.Socks, ClothingType.Swimwear]);
        result.Items.Where(item => item.Item.Type is ClothingType.Underwear or ClothingType.Socks).Should().OnlyContain(item => item.Reasons.Contains("prenda esencial diaria para la duración del viaje"));
    }

    [Fact]
    public void RecommendAddsSweaterAndCoatForColdForecast()
    {
        var sweater = new ClothingItem(Guid.NewGuid(), "Jersey", ClothingType.Sweater, Season.Winter, "Gris", 6, false, Style.Casual, 450, true, true, 50, []);
        var coat = new ClothingItem(Guid.NewGuid(), "Abrigo", ClothingType.Coat, Season.Winter, "Negro", 9, false, Style.Casual, 1100, true, true, 50, []);
        var forecast = new TripWeatherForecast("Roma", 5, 12, 20, DemoData.RomeTrip.StartDate, DemoData.RomeTrip.EndDate, []);

        var result = PackingRecommendationService.Recommend(DemoData.RomeTrip, DemoData.Wardrobe.Append(sweater).Append(coat), forecast);

        result.Items.Select(item => item.Item.Type).Should().Contain([ClothingType.Sweater, ClothingType.Coat]);
    }

    [Fact]
    public void CreateOutfitsUsesTheForecastForEachDayInsteadOfTheGlobalTripMinimum()
    {
        var trip = new Trip(Guid.NewGuid(), "Roma", new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 15), 9, 32, [Style.Casual]);
        var tShirt = new ClothingItem(Guid.NewGuid(), "Camiseta", ClothingType.TShirt, Season.AllYear, "Azul", 2, false, Style.Casual, 180, true, true, 90, []);
        var trousers = new ClothingItem(Guid.NewGuid(), "Pantalón", ClothingType.Trousers, Season.AllYear, "Negro", 3, false, Style.Casual, 500, true, true, 80, []);
        var shoes = new ClothingItem(Guid.NewGuid(), "Zapatillas", ClothingType.Shoes, Season.AllYear, "Blanco", 3, false, Style.Casual, 700, true, true, 80, []);
        var sweater = new ClothingItem(Guid.NewGuid(), "Jersey", ClothingType.Sweater, Season.Winter, "Gris", 6, false, Style.Casual, 450, true, true, 75, []);
        var coat = new ClothingItem(Guid.NewGuid(), "Abrigo", ClothingType.Coat, Season.Winter, "Negro", 9, false, Style.Casual, 1100, true, true, 70, []);
        var items = new[] { tShirt, trousers, shoes, sweater, coat }.Select(item => new PlannedItem(new RecommendedItem(item, 50, []), false)).ToArray();
        var forecast = new TripWeatherForecast("Roma", 9, 32, 20, trip.StartDate, trip.EndDate,
        [
            new DailyTripForecast(trip.StartDate, 22, 32, 5, 0, 23, 33),
            new DailyTripForecast(trip.EndDate, 9, 18, 20, 3, 8, 17)
        ]);

        var outfits = OutfitRecommendationService.Create(trip, items, forecast);

        outfits.Single(outfit => outfit.Date == trip.StartDate).Items.Select(item => item.Type).Should().NotContain([ClothingType.Sweater, ClothingType.Coat]);
        outfits.Single(outfit => outfit.Date == trip.EndDate).Items.Select(item => item.Type).Should().Contain([ClothingType.Sweater, ClothingType.Coat]);
    }
}
