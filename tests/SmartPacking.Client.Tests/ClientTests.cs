using System.Net;
using System.Text;
using FluentAssertions;
using Xunit;

namespace SmartPacking.Client.Tests;

public sealed class BearerTokenHandlerTests
{
    [Fact]
    public async Task SendAsyncWithAccessTokenAddsBearerHeader()
    {
        var tokenProvider = new StubTokenProvider("access-1", null);
        using var terminal = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var bearer = new BearerTokenHandler(tokenProvider) { InnerHandler = terminal };
        using var client = new HttpClient(bearer);

        using var response = await client.GetAsync("https://smartpacking.test/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        terminal.AuthorizationValues.Should().Equal("Bearer access-1");
        tokenProvider.RefreshCalls.Should().Be(0);
    }

    [Fact]
    public async Task SendAsyncWhenUnauthorizedRefreshesAndRetriesOnce()
    {
        var tokenProvider = new StubTokenProvider("access-1", "access-2");
        using var terminal = new SequenceHandler(call => new HttpResponseMessage(
            call == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK));
        using var bearer = new BearerTokenHandler(tokenProvider) { InnerHandler = terminal };
        using var client = new HttpClient(bearer);

        using var response = await client.GetAsync("https://smartpacking.test/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        terminal.AuthorizationValues.Should().Equal("Bearer access-1", "Bearer access-2");
        tokenProvider.RefreshCalls.Should().Be(1);
        tokenProvider.RejectedAccessToken.Should().Be("access-1");
    }

    [Fact]
    public async Task SendAsyncWhenRefreshUnavailableNotifiesAuthenticationRequiredWithoutRetry()
    {
        var tokenProvider = new StubTokenProvider("access-1", null);
        var authenticationRequired = new RecordingAuthenticationRequiredHandler();
        using var terminal = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var bearer = new BearerTokenHandler(tokenProvider, authenticationRequired) { InnerHandler = terminal };
        using var client = new HttpClient(bearer);

        using var response = await client.GetAsync("https://smartpacking.test/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        terminal.AuthorizationValues.Should().Equal("Bearer access-1");
        tokenProvider.RefreshCalls.Should().Be(1);
        authenticationRequired.Calls.Should().Be(1);
    }

    [Fact]
    public async Task SendAsyncWhenNoStoredTokenGetsUnauthorizedNotifiesAuthenticationRequired()
    {
        var tokenProvider = new StubTokenProvider(null, null);
        var authenticationRequired = new RecordingAuthenticationRequiredHandler();
        using var terminal = new SequenceHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        using var bearer = new BearerTokenHandler(tokenProvider, authenticationRequired) { InnerHandler = terminal };
        using var client = new HttpClient(bearer);

        using var response = await client.GetAsync("https://smartpacking.test/api/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        terminal.AuthorizationValues.Should().ContainSingle().Which.Should().BeNull();
        tokenProvider.RefreshCalls.Should().Be(0);
        authenticationRequired.Calls.Should().Be(1);
    }

    private sealed class StubTokenProvider(string? accessToken, string? refreshedAccessToken) : IAccessTokenProvider
    {
        public int RefreshCalls { get; private set; }
        public string? RejectedAccessToken { get; private set; }

        public ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(accessToken);
        }

        public ValueTask<string?> RefreshAccessTokenAsync(string? rejectedAccessToken, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RefreshCalls++;
            RejectedAccessToken = rejectedAccessToken;
            return ValueTask.FromResult(refreshedAccessToken);
        }
    }

    private sealed class RecordingAuthenticationRequiredHandler : IAuthenticationRequiredHandler
    {
        public int Calls { get; private set; }

        public Task HandleAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class SequenceHandler(Func<int, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        private int calls;

        public List<string?> AuthorizationValues { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            calls++;
            AuthorizationValues.Add(request.Headers.Authorization?.ToString());
            return Task.FromResult(responseFactory(calls));
        }
    }
}

public sealed class SmartPackingClientTests
{
    [Fact]
    public async Task GetWardrobePageAsyncUsesRequestedPagination()
    {
        string? requestedPath = null;
        using var handler = new CallbackHandler((request, _) =>
        {
            requestedPath = request.RequestUri?.PathAndQuery;
            return Task.FromResult(JsonResponse("{\"data\":[]}"));
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://smartpacking.test/") };
        var client = new SmartPackingClient(httpClient);

        var result = await client.GetWardrobePageAsync(2, 20, CancellationToken.None);

        requestedPath.Should().Be("/api/wardrobe?page=2&pageSize=20");
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(20);
        result.Items.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetClothingThumbnailAsyncWhenMissingReturnsNull()
    {
        using var handler = new CallbackHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://smartpacking.test/") };
        var client = new SmartPackingClient(httpClient);

        var result = await client.GetClothingThumbnailAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UploadClothingPhotoAsyncWithThumbnailSendsBothParts()
    {
        string? multipartBody = null;
        using var handler = new CallbackHandler(async (request, cancellationToken) =>
        {
            var content = request.Content ?? throw new InvalidOperationException("Expected multipart request content.");
            var bytes = await content.ReadAsByteArrayAsync(cancellationToken);
            multipartBody = Encoding.Latin1.GetString(bytes);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://smartpacking.test/") };
        var client = new SmartPackingClient(httpClient);

        await client.UploadClothingPhotoAsync(
            Guid.NewGuid(),
            [1, 2, 3],
            "garment.jpg",
            CancellationToken.None,
            [4, 5, 6]);

        multipartBody.Should().NotBeNull();
        multipartBody.Should().Contain("name=photo");
        multipartBody.Should().Contain("name=thumbnail");
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class CallbackHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            callback(request, cancellationToken);
    }
}
