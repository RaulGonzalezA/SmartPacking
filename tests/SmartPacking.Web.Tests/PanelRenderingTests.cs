using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using SmartPacking.Domain;
using SmartPacking.Web.Components;
using Xunit;

namespace SmartPacking.Web.Tests;

public sealed class PanelRenderingTests : BunitContext
{
    [Fact]
    public void TripsPanelWithoutTripsExplainsHowToStartAndDisablesTripActions()
    {
        var cut = Render<TripsPanel>(parameters => parameters
            .Add(component => component.IsActive, true));

        cut.Markup.Should().Contain("Crea un viaje para empezar a organizarlo.");
        cut.FindAll("button[disabled]").Should().HaveCount(2);
    }

    [Fact]
    public void PackingPanelWithoutPlanExplainsWhatTheUserNeedsToDo()
    {
        var cut = Render<PackingPanel>(parameters => parameters
            .Add(component => component.IsActive, true));

        cut.Markup.Should().Contain("Selecciona un viaje y un viajero para preparar la maleta.");
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
