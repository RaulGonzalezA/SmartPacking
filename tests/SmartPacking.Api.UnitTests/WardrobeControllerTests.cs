using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SmartPacking.Api.Contracts;
using SmartPacking.Api.Controllers;
using SmartPacking.Application;
using SmartPacking.Contracts;
using SmartPacking.Domain;
using Xunit;

namespace SmartPacking.Api.UnitTests;

public sealed class WardrobeControllerTests
{
    [Fact]
    public async Task GetAsyncReturnsBadRequestWithoutAccessingTheStoreForInvalidPagination()
    {
        var store = Substitute.For<ISmartPackingStore>();
        var controller = new WardrobeController(store);

        var result = await controller.GetAsync(page: 0);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = badRequest.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Errors.Should().ContainKey("pagination");
        await store.DidNotReceive().GetDefaultUserAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsyncIncludesActiveAndRetiredGarmentsWhenRequested()
    {
        var store = Substitute.For<ISmartPackingStore>();
        var user = new UserProfile(Guid.NewGuid(), "Raúl", true);
        var active = Garment("Camisa", isDeleted: false);
        var retired = Garment("Jersey", isDeleted: true);
        store.GetDefaultUserAsync(Arg.Any<CancellationToken>()).Returns(user);
        store.GetWardrobeCollectionAsync(user.Id, 2, 20, Arg.Any<CancellationToken>())
            .Returns(new WardrobeSnapshot([active], [retired], 2, 20));
        var controller = new WardrobeController(store);

        var result = await controller.GetAsync(includeDeleted: true, page: 2, pageSize: 20);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<WardrobeCollectionResponse>().Subject;
        response.Items.Should().ContainSingle(item => item.Id == active.Id && !item.IsDeleted);
        response.DeletedItems.Should().ContainSingle(item => item.Id == retired.Id && item.IsDeleted);
        response.Page.Should().Be(2);
        response.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task CreateAsyncRejectsAnInvalidGarmentBeforeAccessingTheStore()
    {
        var store = Substitute.For<ISmartPackingStore>();
        var controller = new WardrobeController(store);
        var request = new UpsertClothingItemRequest("", ClothingType.TShirt, Season.AllYear, "Azul", 0, false, Style.Casual, 180, true, true, 70, [], null);

        var result = await controller.CreateAsync(request, CancellationToken.None);

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<ValidationProblemDetails>();
        await store.DidNotReceive().GetDefaultUserAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStatusAsyncReturnsNotFoundWhenTheGarmentDoesNotExist()
    {
        var store = Substitute.For<ISmartPackingStore>();
        var user = new UserProfile(Guid.NewGuid(), "Raúl", true);
        var garmentId = Guid.NewGuid();
        store.GetDefaultUserAsync(Arg.Any<CancellationToken>()).Returns(user);
        store.UpdateClothingStatusAsync(user.Id, garmentId, true, false, Arg.Any<CancellationToken>()).Returns(false);
        var controller = new WardrobeController(store);

        var result = await controller.UpdateStatusAsync(garmentId, new UpdateClothingStatusRequest(true, false), CancellationToken.None);

        var problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    private static ClothingItem Garment(string name, bool isDeleted) => new(Guid.NewGuid(), name, ClothingType.TShirt, Season.AllYear, "Azul", 2, false, Style.Casual, 180, true, true, 70, [], isDeleted, null, null);
}
