using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SmartPacking.Api;
using SmartPacking.Api.Controllers;
using SmartPacking.Application;
using Xunit;

namespace SmartPacking.Api.UnitTests;

public sealed class GarmentRecognitionControllerTests
{
    [Fact]
    public async Task RecognizeAsyncRejectsAnInvalidPhotoBeforeConsumingAnAiAttempt()
    {
        var recognizer = Substitute.For<IGarmentRecognizer>();
        var usage = Substitute.For<IGarmentRecognitionUsageService>();
        var controller = new GarmentRecognitionController(recognizer, usage);
        using var content = new MemoryStream([1]);
        var photo = Photo(content, "image/png");

        var result = await controller.RecognizeAsync(photo, CancellationToken.None);

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<ValidationProblemDetails>();
        await usage.DidNotReceive().RegisterAttemptAsync(Arg.Any<CancellationToken>());
        await recognizer.DidNotReceive().RecognizeAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecognizeAsyncReturnsTooManyRequestsWhenTheQuotaIsExhausted()
    {
        var recognizer = Substitute.For<IGarmentRecognizer>();
        var usage = Substitute.For<IGarmentRecognitionUsageService>();
        usage.RegisterAttemptAsync(Arg.Any<CancellationToken>()).Returns(new GarmentRecognitionUsageResult(false, 20, 20, 0, 0, true, "Free", new DateOnly(2026, 9, 1)));
        var controller = new GarmentRecognitionController(recognizer, usage);
        using var content = new MemoryStream([1]);

        var result = await controller.RecognizeAsync(Photo(content, "image/jpeg"), CancellationToken.None);

        var problem = result.Result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        await recognizer.DidNotReceive().RecognizeAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecognizeAsyncReturnsTheRecognizerSuggestionWhenTheQuotaAllowsIt()
    {
        var recognizer = Substitute.For<IGarmentRecognizer>();
        var usage = Substitute.For<IGarmentRecognitionUsageService>();
        var suggestion = new GarmentRecognitionSuggestion("Jersey", "Verde", "Lana", ["Invierno"], "Casual", 350, ["Turismo"]);
        usage.RegisterAttemptAsync(Arg.Any<CancellationToken>()).Returns(new GarmentRecognitionUsageResult(true, 1, 20, 0, 19, false, "Free", new DateOnly(2026, 9, 1)));
        recognizer.RecognizeAsync(Arg.Any<Stream>(), "image/jpeg", Arg.Any<CancellationToken>()).Returns(suggestion);
        var controller = new GarmentRecognitionController(recognizer, usage);
        using var content = new MemoryStream([1]);

        var result = await controller.RecognizeAsync(Photo(content, "image/jpeg"), CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(suggestion);
    }

    private static FormFile Photo(Stream content, string contentType) => new(content, 0, content.Length, "photo", "garment.jpg")
    {
        Headers = new HeaderDictionary(),
        ContentType = contentType
    };
}
