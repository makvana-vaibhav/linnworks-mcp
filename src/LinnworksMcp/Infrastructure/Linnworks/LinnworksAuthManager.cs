using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinnworksMcp.Infrastructure.Linnworks;

public interface ILinnworksAuthManager
{
    Task<LinnworksSession> GetSessionAsync(LinnworksCredentials credentials, CancellationToken cancellationToken);

    void InvalidateSession(string userId);
}

/// <summary>
/// Caches one authorized Linnworks session per tenant, refreshing it shortly before it expires.
/// Registered as a singleton so the cache is process-wide.
/// </summary>
/// <remarks>
/// Mirrors rishvi-agent's <c>LinnworksAuthManager</c>: cache keyed by caller-supplied user id,
/// expiry taken from the response TTL, refresh 60s early, region routing via the response
/// <c>Server</c> field. It additionally fixes two defects in that implementation — concurrent
/// callers for one tenant no longer each fire their own authorize request, and re-presenting a
/// user id with changed credentials evicts the stale session instead of continuing to serve it.
/// </remarks>
public sealed class LinnworksAuthManager : ILinnworksAuthManager
{
    private readonly HttpClient _httpClient;
    private readonly LinnworksOptions _options;
    private readonly ILogger<LinnworksAuthManager> _logger;
    private readonly TimeProvider _timeProvider;

    private readonly ConcurrentDictionary<string, CachedSession> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public LinnworksAuthManager(
        HttpClient httpClient,
        IOptions<LinnworksOptions> options,
        ILogger<LinnworksAuthManager> logger,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<LinnworksSession> GetSessionAsync(
        LinnworksCredentials credentials,
        CancellationToken cancellationToken)
    {
        if (TryGetValidSession(credentials, out var cached))
        {
            return cached;
        }

        // Single-flight: only one authorize request per tenant is in flight at a time.
        var gate = _locks.GetOrAdd(credentials.UserId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller may have populated the cache while we waited.
            if (TryGetValidSession(credentials, out cached))
            {
                return cached;
            }

            return await AuthenticateAsync(credentials, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public void InvalidateSession(string userId)
    {
        if (_sessions.TryRemove(userId, out _))
        {
            _logger.LogInformation("Session invalidated for user {UserId}", userId);
        }
    }

    private bool TryGetValidSession(LinnworksCredentials credentials, out LinnworksSession session)
    {
        session = null!;

        if (!_sessions.TryGetValue(credentials.UserId, out var cached))
        {
            return false;
        }

        // Same user id presented with different credentials — the cached session belongs to
        // whoever authenticated first and must not be handed to a different caller.
        if (!string.Equals(cached.Fingerprint, credentials.Fingerprint, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Credentials changed for user {UserId} — evicting cached session", credentials.UserId);
            _sessions.TryRemove(credentials.UserId, out _);
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        if (now >= cached.ExpiresAt - _options.SessionRefreshBuffer)
        {
            return false;
        }

        _logger.LogDebug(
            "Using cached session for user {UserId}, expires in {ExpiresInSeconds}s",
            credentials.UserId,
            (int)(cached.ExpiresAt - now).TotalSeconds);

        session = cached.Session;
        return true;
    }

    private async Task<LinnworksSession> AuthenticateAsync(
        LinnworksCredentials credentials,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Authorizing user {UserId} against Linnworks", credentials.UserId);

        // Body is PascalCase. No Authorization header on this call.
        var body = new AuthorizeRequest(
            credentials.ApplicationId,
            credentials.ApplicationSecret,
            credentials.Token);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient
                .PostAsJsonAsync(_options.AuthUrl, body, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw LinnworksApiException.Timeout(_options.AuthUrl);
        }
        catch (HttpRequestException ex)
        {
            throw new LinnworksApiException(
                LinnworksErrorKind.Unavailable,
                "Linnworks API is currently unavailable.",
                $"Transport failure calling {_options.AuthUrl}.",
                innerException: ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogError(
                    "AuthorizeByApplication failed for user {UserId} with status {StatusCode}",
                    credentials.UserId,
                    (int)response.StatusCode);

                // On this endpoint any 4xx means the supplied credentials were not accepted —
                // Linnworks answers bad application credentials with 400, not 401 — so report it
                // as an authentication failure rather than a generic upstream error.
                if ((int)response.StatusCode is >= 400 and < 500)
                {
                    throw new LinnworksApiException(
                        LinnworksErrorKind.Authentication,
                        "Authentication failed — session may have expired or credentials are invalid.",
                        $"AuthorizeByApplication rejected the credentials "
                        + $"[{(int)response.StatusCode}]: {errorBody}",
                        response.StatusCode);
                }

                throw LinnworksApiException.FromStatusCode(
                    response.StatusCode, "/api/Auth/AuthorizeByApplication", errorBody);
            }

            LinnworksSession? session;
            try
            {
                session = await response.Content
                    .ReadFromJsonAsync<LinnworksSession>(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException ex)
            {
                throw LinnworksApiException.Deserialization("/api/Auth/AuthorizeByApplication", ex);
            }

            if (session is null || string.IsNullOrWhiteSpace(session.Token))
            {
                throw new LinnworksApiException(
                    LinnworksErrorKind.Authentication,
                    "Authentication failed — session may have expired or credentials are invalid.",
                    "AuthorizeByApplication returned no usable session token.");
            }

            // TTL is in seconds, and expiry runs from when the response was parsed.
            var expiresAt = _timeProvider.GetUtcNow().AddSeconds(session.Ttl);

            _sessions[credentials.UserId] = new CachedSession(session, expiresAt, credentials.Fingerprint);

            _logger.LogInformation(
                "Session obtained for user {UserId} on {Server} (locality {Locality}), TTL {Ttl}s",
                credentials.UserId,
                session.Server,
                session.Locality,
                session.Ttl);

            return session;
        }
    }

    private sealed record CachedSession(LinnworksSession Session, DateTimeOffset ExpiresAt, string Fingerprint);

    private sealed record AuthorizeRequest(
        string ApplicationId,
        string ApplicationSecret,
        string Token);
}
