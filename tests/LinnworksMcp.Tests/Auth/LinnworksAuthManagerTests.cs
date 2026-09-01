using System.Net.Http;
using System.Net;
using System.Net.Http.Json;
using LinnworksMcp.Infrastructure.Linnworks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;

namespace LinnworksMcp.Tests.Auth;

public class LinnworksAuthManagerTests
{
    private readonly TestTimeProvider _timeProvider = new();
    private readonly LinnworksOptions _options = new()
    {
        AuthUrl = "https://api.linnworks.net/api/Auth/AuthorizeByApplication",
        SessionRefreshBuffer = TimeSpan.FromSeconds(60)
    };

    [Fact]
    public async Task GetSessionAsync_CachesSession_WhenTTLIsValid()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var expectedSession = CreateMockSession("token-123", ttl: 3600);

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(expectedSession)
            });

        var manager = new LinnworksAuthManager(
            StubHttpClientFactory.For(handlerMock.Object),
            Options.Create(_options),
            NullLogger<LinnworksAuthManager>.Instance,
            _timeProvider);

        var creds = new LinnworksCredentials("user-1", "app-id", "app-secret", "user-token");

        // Act - Call 1
        var session1 = await manager.GetSessionAsync(creds, CancellationToken.None);

        // Act - Call 2 (within TTL buffer)
        var session2 = await manager.GetSessionAsync(creds, CancellationToken.None);

        // Assert
        Assert.Equal("token-123", session1.Token);
        Assert.Same(session1, session2);

        // Verify HTTP Post was called only ONCE
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetSessionAsync_Reauthenticates_WhenWithinRefreshBuffer()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var initialSession = CreateMockSession("token-old", ttl: 120);
        var refreshedSession = CreateMockSession("token-new", ttl: 3600);

        handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(initialSession)
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(refreshedSession)
            });

        var manager = new LinnworksAuthManager(
            StubHttpClientFactory.For(handlerMock.Object),
            Options.Create(_options),
            NullLogger<LinnworksAuthManager>.Instance,
            _timeProvider);

        var creds = new LinnworksCredentials("user-1", "app-id", "app-secret", "user-token");

        // Call 1 at T=0
        var s1 = await manager.GetSessionAsync(creds, CancellationToken.None);
        Assert.Equal("token-old", s1.Token);

        // Advance time to T=65s (TTL 120s - 60s buffer = 60s threshold, so 65s triggers refresh)
        _timeProvider.Advance(TimeSpan.FromSeconds(65));

        // Call 2 should trigger refresh
        var s2 = await manager.GetSessionAsync(creds, CancellationToken.None);
        Assert.Equal("token-new", s2.Token);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateSession_EvictsCachedSession()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = JsonContent.Create(CreateMockSession("token-" + Guid.NewGuid(), ttl: 3600))
            });

        var manager = new LinnworksAuthManager(
            StubHttpClientFactory.For(handlerMock.Object),
            Options.Create(_options),
            NullLogger<LinnworksAuthManager>.Instance,
            _timeProvider);

        var creds = new LinnworksCredentials("user-1", "app-id", "app-secret", "user-token");

        var s1 = await manager.GetSessionAsync(creds, CancellationToken.None);
        manager.InvalidateSession("user-1");
        var s2 = await manager.GetSessionAsync(creds, CancellationToken.None);

        Assert.NotEqual(s1.Token, s2.Token);
    }

    [Fact]
    public async Task GetSessionAsync_ThrowsLinnworksApiException_OnAuthFailure()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Invalid credentials")
            });

        var manager = new LinnworksAuthManager(
            StubHttpClientFactory.For(handlerMock.Object),
            Options.Create(_options),
            NullLogger<LinnworksAuthManager>.Instance,
            _timeProvider);

        var creds = new LinnworksCredentials("user-1", "app-id", "bad-secret", "user-token");

        var ex = await Assert.ThrowsAsync<LinnworksApiException>(
            () => manager.GetSessionAsync(creds, CancellationToken.None));

        Assert.Equal(LinnworksErrorKind.Authentication, ex.Kind);
    }

    private static LinnworksSession CreateMockSession(string token, int ttl) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Token = token,
        Server = "https://eu-ext.linnworks.net",
        Ttl = ttl,
        Locality = "EU",
        UserName = "test@example.com"
    };

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    /// <summary>
    /// Hands the auth manager an <see cref="HttpClient"/> over the mocked handler, standing in
    /// for the named client it would resolve from DI.
    /// </summary>
    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public static IHttpClientFactory For(HttpMessageHandler handler) => new StubHttpClientFactory(handler);

        public HttpClient CreateClient(string name) => new(handler);
    }
}
