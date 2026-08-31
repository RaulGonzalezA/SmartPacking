using FluentAssertions;
using SmartPacking.Application;
using SmartPacking.Domain;
using Xunit;

namespace SmartPacking.Application.Tests;

public sealed class PackingRecommendationServiceTests
{
    [Fact]
    public void Recommend_WhenItemIsDirty_DoesNotIncludeItInPackingList()
    {
        var dirtyFavourite = DemoData.Wardrobe[0] with { IsClean = false };
        var wardrobe = DemoData.Wardrobe.Select(item => item.Id == dirtyFavourite.Id ? dirtyFavourite : item);

        var result = new PackingRecommendationService().Recommend(DemoData.RomeTrip, wardrobe);

        result.Items.Select(item => item.Item.Id).Should().NotContain(dirtyFavourite.Id);
    }

    [Fact]
    public void Recommend_ForHotTrip_IncludesSummerShoesAndShorts()
    {
        var result = new PackingRecommendationService().Recommend(DemoData.RomeTrip, DemoData.Wardrobe);

        result.Items.Select(item => item.Item.Type).Should().Contain([ClothingType.Shorts, ClothingType.Sandals]);
        result.TotalWeightGrams.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Recommend_ForFormalActivity_PrefersFormalTrousers()
    {
        var formalTrousers = DemoData.Wardrobe.Single(item => item.Type == ClothingType.Trousers) with { Style = Style.Formal, PreferenceScore = 70, CombinesWith = [] };
        var casualTrousers = formalTrousers with { Id = Guid.NewGuid(), Name = "Pantalón casual", Style = Style.Casual };
        var wardrobe = DemoData.Wardrobe.Where(item => item.Type != ClothingType.Trousers).Append(formalTrousers).Append(casualTrousers);
        var trip = DemoData.RomeTrip with { Activities = [Style.Formal] };

        var result = new PackingRecommendationService().Recommend(trip, wardrobe);

        result.Items.Where(item => item.Item.Type == ClothingType.Trousers).First().Item.Style.Should().Be(Style.Formal);
    }
}
