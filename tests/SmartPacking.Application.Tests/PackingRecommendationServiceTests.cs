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
}
