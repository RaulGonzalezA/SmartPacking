using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SmartPacking.Api;
using SmartPacking.Api.Controllers;
using SmartPacking.Api.Validation;
using SmartPacking.Application;
using SmartPacking.Domain;
using Xunit;

namespace SmartPacking.Api.UnitTests;

public sealed class SystemControllerTests
{
    [Fact]
    public async Task DeleteCurrentUserAsyncDeletesEveryOwnedPhotoBeforeDeletingTheUserData()
    {
        var store = Substitute.For<ISmartPackingStore>();
        var photoStorage = Substitute.For<IPhotoStorage>();
        var user = new UserProfile(Guid.NewGuid(), "Raúl", true);
        var photoId = Guid.NewGuid();
        var noPhotoId = Guid.NewGuid();
        store.GetDefaultUserAsync(Arg.Any<CancellationToken>()).Returns(user);
        store.GetWardrobeAsync(user.Id, Arg.Any<CancellationToken>()).Returns(
        [
            Garment(photoId, "/api/wardrobe/photo"),
            Garment(noPhotoId, null),
        ]);
        store.DeleteUserDataAsync(user.Id, Arg.Any<CancellationToken>()).Returns(true);
        var controller = new SystemController(
            store,
            photoStorage,
            new CompleteUserOnboardingRequestValidator(),
            new UpdateCurrentUserRequestValidator(),
            new DeleteCurrentUserRequestValidator());

        var result = await controller.DeleteCurrentUserAsync(new DeleteCurrentUserRequest("ELIMINAR"), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await photoStorage.Received(1).DeleteAsync(photoId, Arg.Any<CancellationToken>());
        await photoStorage.DidNotReceive().DeleteAsync(noPhotoId, Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            photoStorage.DeleteAsync(photoId, Arg.Any<CancellationToken>());
            store.DeleteUserDataAsync(user.Id, Arg.Any<CancellationToken>());
        });
    }

    private static ClothingItem Garment(Guid id, string? photoUrl) => new(
        id,
        "Camiseta",
        ClothingType.TShirt,
        Season.AllYear,
        "Azul",
        2,
        false,
        Style.Casual,
        180,
        true,
        true,
        70,
        [],
        false,
        null,
        photoUrl);
}
