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

public sealed class ProfilesControllerTests
{
    [Fact]
    public async Task CreateAsyncRejectsAProfileWithoutANameBeforeAccessingTheStore()
    {
        var store = Substitute.For<ISmartPackingStore>();
        var controller = CreateController(store);

        var result = await controller.CreateAsync(new CreateFamilyProfileRequest(" "), CancellationToken.None);

        var badRequest = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = badRequest.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Errors.Should().ContainKey("name");
        await store.DidNotReceive().GetDefaultUserAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsyncTrimsAndPersistsAValidProfileForTheCurrentUser()
    {
        var store = Substitute.For<ISmartPackingStore>();
        var user = new UserProfile(Guid.NewGuid(), "Raúl", true);
        var profile = new FamilyProfile(Guid.NewGuid(), "Lucía", false, "Lista", "Alergia");
        store.GetDefaultUserAsync(Arg.Any<CancellationToken>()).Returns(user);
        store.AddFamilyProfileAsync(user.Id, Arg.Any<FamilyProfile>(), Arg.Any<CancellationToken>()).Returns(profile);
        var controller = CreateController(store);

        var result = await controller.CreateAsync(new CreateFamilyProfileRequest(" Lucía ", " Lista ", " Alergia "), CancellationToken.None);

        var created = result.Result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/profiles/{profile.Id}");
        await store.Received(1).AddFamilyProfileAsync(
            user.Id,
            Arg.Is<FamilyProfile>(candidate => candidate.Name == "Lucía" && candidate.PackingNotes == "Lista" && candidate.MedicalNotes == "Alergia"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetTripProfilesAsyncDoesNotAssignProfilesToAnUnknownTrip()
    {
        var store = Substitute.For<ISmartPackingStore>();
        var user = new UserProfile(Guid.NewGuid(), "Raúl", true);
        var tripId = Guid.NewGuid();
        store.GetDefaultUserAsync(Arg.Any<CancellationToken>()).Returns(user);
        store.GetTripAsync(user.Id, tripId, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Trip?>(null));
        var controller = CreateController(store);

        var result = await controller.SetTripProfilesAsync(tripId, new SetTripProfilesRequest([Guid.NewGuid()]), CancellationToken.None);

        var problem = result.Should().BeOfType<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        await store.DidNotReceive().SetTripProfilesAsync(user.Id, tripId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    private static ProfilesController CreateController(ISmartPackingStore store) => new(store, new ProfilePackingListService(store));
}
