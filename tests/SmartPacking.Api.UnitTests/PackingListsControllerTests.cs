using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SmartPacking.Api;
using SmartPacking.Api.Controllers;
using SmartPacking.Application;
using SmartPacking.Domain;
using Xunit;

namespace SmartPacking.Api.UnitTests;

public sealed class PackingListsControllerTests
{
    [Fact]
    public async Task SetChecklistPackedAsyncReturnsNoContentWhenTheItemBelongsToTheUser()
    {
        var store = Substitute.For<ISmartPackingStore>();
        var user = new UserProfile(Guid.NewGuid(), "Raúl", true);
        var itemId = Guid.NewGuid();
        store.GetDefaultUserAsync(Arg.Any<CancellationToken>()).Returns(user);
        store.SetChecklistPackedAsync(user.Id, itemId, true, Arg.Any<CancellationToken>()).Returns(true);
        var controller = new PackingListsController(store);

        var result = await controller.SetChecklistPackedAsync(itemId, new SetPackedRequest(true), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await store.Received(1).SetChecklistPackedAsync(user.Id, itemId, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddProfilePackedItemAsyncReturnsNotFoundWhenThePackingListIsUnavailable()
    {
        var store = Substitute.For<ISmartPackingStore>();
        var user = new UserProfile(Guid.NewGuid(), "Raúl", true);
        var packingListId = Guid.NewGuid();
        var garmentId = Guid.NewGuid();
        store.GetDefaultUserAsync(Arg.Any<CancellationToken>()).Returns(user);
        store.AddProfilePackingListItemAsync(user.Id, packingListId, garmentId, Arg.Any<CancellationToken>()).Returns(false);
        var controller = new PackingListsController(store);

        var result = await controller.AddProfilePackedItemAsync(packingListId, new AddPackingListItemRequest(garmentId), CancellationToken.None);

        var problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ApplyRecommendationChangeUsesTheAuthenticatedUsersPackingList()
    {
        var store = Substitute.For<ISmartPackingStore>();
        var user = new UserProfile(Guid.NewGuid(), "Raúl", true);
        var packingListId = Guid.NewGuid();
        var garmentId = Guid.NewGuid();
        store.GetDefaultUserAsync(Arg.Any<CancellationToken>()).Returns(user);
        store.ApplyProfileRecommendationChangeAsync(user.Id, packingListId, garmentId, RecommendationChangeKind.Add, Arg.Any<CancellationToken>()).Returns(true);
        var controller = new PackingListsController(store);

        var result = await controller.ApplyRecommendationChangeAsync(packingListId, new ResolveRecommendationChangeRequest(garmentId, RecommendationChangeKind.Add), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await store.Received(1).ApplyProfileRecommendationChangeAsync(user.Id, packingListId, garmentId, RecommendationChangeKind.Add, Arg.Any<CancellationToken>());
    }
}
