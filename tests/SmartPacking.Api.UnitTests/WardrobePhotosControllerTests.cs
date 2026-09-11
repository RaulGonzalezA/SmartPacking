using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SmartPacking.Api.Controllers;
using SmartPacking.Application;
using SmartPacking.Domain;
using Xunit;

namespace SmartPacking.Api.UnitTests;

public sealed class WardrobePhotosControllerTests
{
    [Fact]
    public async Task GetThumbnailUsesTargetedClothingLookup()
    {
        var userId = Guid.NewGuid();
        var clothingItemId = Guid.NewGuid();
        var store = Substitute.For<ISmartPackingStore>();
        var lookup = Substitute.For<IClothingItemLookup>();
        var photoStorage = Substitute.For<IPhotoStorage>();
        var item = CreateClothingItem(clothingItemId, "/api/wardrobe/item/photo");

        store.GetDefaultUserAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UserProfile(userId, "Test", true)));
        lookup.GetAsync(userId, clothingItemId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ClothingItem?>(item));
        photoStorage.OpenThumbnailReadAsync(clothingItemId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Stream?>(new MemoryStream(CreateJpeg())));

        var controller = CreateController(store, lookup, photoStorage);
        var result = await controller.GetThumbnailAsync(clothingItemId, CancellationToken.None);

        result.Should().BeOfType<FileStreamResult>();
        controller.Response.Headers.CacheControl.ToString().Should().Be("private, no-cache");
        _ = lookup.Received(1).GetAsync(userId, clothingItemId, Arg.Any<CancellationToken>());
        _ = store.DidNotReceive().GetWardrobeAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadRejectsFakeJpegContent()
    {
        var store = Substitute.For<ISmartPackingStore>();
        var lookup = Substitute.For<IClothingItemLookup>();
        var photoStorage = Substitute.For<IPhotoStorage>();
        var controller = CreateController(store, lookup, photoStorage);
        var photo = CreateFormFile([1, 2, 3, 4], "photo");

        var result = await controller.UploadAsync(Guid.NewGuid(), photo, null, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        _ = photoStorage.DidNotReceive().SaveJpegAsync(Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadReplacesThumbnailAfterPrimaryPhotoIsPersisted()
    {
        var userId = Guid.NewGuid();
        var clothingItemId = Guid.NewGuid();
        var store = Substitute.For<ISmartPackingStore>();
        var lookup = Substitute.For<IClothingItemLookup>();
        var photoStorage = Substitute.For<IPhotoStorage>();
        var item = CreateClothingItem(clothingItemId, null);
        var imageUrl = $"/api/wardrobe/{clothingItemId}/photo";

        store.GetDefaultUserAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UserProfile(userId, "Test", true)));
        lookup.GetAsync(userId, clothingItemId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ClothingItem?>(item));
        photoStorage.SaveJpegAsync(clothingItemId, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(imageUrl));
        store.UpdateClothingItemAsync(userId, Arg.Any<ClothingItem>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<ClothingItem?>((ClothingItem)call[1]));
        photoStorage.DeleteThumbnailAsync(clothingItemId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        photoStorage.SaveThumbnailJpegAsync(clothingItemId, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = CreateController(store, lookup, photoStorage);
        var photo = CreateFormFile(CreateJpeg(1280, 960), "photo");
        var thumbnail = CreateFormFile(CreateJpeg(320, 240), "thumbnail");

        var result = await controller.UploadAsync(clothingItemId, photo, thumbnail, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _ = photoStorage.Received(1).SaveJpegAsync(clothingItemId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        _ = store.Received(1).UpdateClothingItemAsync(
            userId,
            Arg.Is<ClothingItem>(candidate => candidate.Id == clothingItemId && candidate.PhotoUrl == imageUrl),
            Arg.Any<CancellationToken>());
        _ = photoStorage.Received(1).DeleteThumbnailAsync(clothingItemId, Arg.Any<CancellationToken>());
        _ = photoStorage.Received(1).SaveThumbnailJpegAsync(clothingItemId, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadDoesNotExposeAnotherUsersClothingItem()
    {
        var userId = Guid.NewGuid();
        var clothingItemId = Guid.NewGuid();
        var store = Substitute.For<ISmartPackingStore>();
        var lookup = Substitute.For<IClothingItemLookup>();
        var photoStorage = Substitute.For<IPhotoStorage>();
        store.GetDefaultUserAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UserProfile(userId, "Test", true)));
        lookup.GetAsync(userId, clothingItemId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ClothingItem?>(null));

        var controller = CreateController(store, lookup, photoStorage);
        var result = await controller.UploadAsync(
            clothingItemId,
            CreateFormFile(CreateJpeg(), "photo"),
            null,
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        _ = photoStorage.DidNotReceive().SaveJpegAsync(Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    private static WardrobePhotosController CreateController(
        ISmartPackingStore store,
        IClothingItemLookup lookup,
        IPhotoStorage photoStorage) =>
        new(store, lookup, photoStorage)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static ClothingItem CreateClothingItem(Guid id, string? photoUrl) => new(
        id,
        "Camiseta",
        ClothingType.TShirt,
        Season.AllYear,
        "Azul",
        3,
        false,
        Style.Casual,
        200,
        true,
        true,
        50,
        [],
        false,
        null,
        photoUrl,
        "Algodón");

    private static FormFile CreateFormFile(byte[] content, string name)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, name, $"{name}.jpg")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/jpeg"
        };
    }

    private static byte[] CreateJpeg(int width = 320, int height = 240) =>
    [
        0xFF, 0xD8,
        0xFF, 0xC0, 0x00, 0x11, 0x08,
        (byte)(height >> 8), (byte)height,
        (byte)(width >> 8), (byte)width,
        0x03,
        0x01, 0x11, 0x00,
        0x02, 0x11, 0x00,
        0x03, 0x11, 0x00,
        0xFF, 0xD9
    ];
}
