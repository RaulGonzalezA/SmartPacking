using FluentAssertions;
using SmartPacking.Application;
using SmartPacking.Domain;
using Xunit;

namespace SmartPacking.Application.Tests;

public sealed class PackingRecommendationServiceTests
{
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
}
