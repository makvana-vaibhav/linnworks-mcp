using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinnworksMcp.Infrastructure.Observability;
using Microsoft.Extensions.Logging;

namespace LinnworksMcp.Infrastructure.Linnworks;

public interface ILinnworksClient
{
    Task<TResponse> PostAsync<TRequest, TResponse>(
        string path, TRequest body, CancellationToken cancellationToken);

    Task<TResponse> GetAsync<TResponse>(
        string path, IReadOnlyDictionary<string, string?>? query, CancellationToken cancellationToken);
}

/// <summary>
/// Authorized HTTP client wrapper over Linnworks REST API endpoints with regional routing and error handling.
/// </summary>
public sealed class LinnworksClient(
    HttpClient httpClient,
    ILinnworksAuthManager authManager,
    ILinnworksCredentialProvider credentialProvider,
    EndpointRateLimiter rateLimiter,
    ToolMetrics metrics,
    ILogger<LinnworksClient> logger) : ILinnworksClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public Task<TResponse> PostAsync<TRequest, TResponse>(
        string path, TRequest body, CancellationToken cancellationToken) =>
        SendAsync<TResponse>(
            HttpMethod.Post,
            path,
            query: null,
            createContent: () => JsonContent.Create(body, options: SerializerOptions),
            cancellationToken);

    public Task<TResponse> GetAsync<TResponse>(
        string path, IReadOnlyDictionary<string, string?>? query, CancellationToken cancellationToken) =>
        SendAsync<TResponse>(HttpMethod.Get, path, query, createContent: null, cancellationToken);

    private async Task<TResponse> SendAsync<TResponse>(
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string?>? query,
        Func<HttpContent>? createContent,
        CancellationToken cancellationToken)
    {
        var credentials = credentialProvider.GetCredentials();

        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var session = await authManager
                .GetSessionAsync(credentials, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                return await ExecuteAsync<TResponse>(
                    method, path, query, createContent, session, credentials, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (LinnworksApiException ex)
                when (ex.Kind == LinnworksErrorKind.Authentication && attempt == 1)
            {
                logger.LogWarning(
                    "Linnworks rejected session for user {UserId} calling {Path} — re-authorizing",
                    credentials.UserId,
                    path);

                authManager.InvalidateSession(credentials.UserId);
            }
        }
    }

    private async Task<TResponse> ExecuteAsync<TResponse>(
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string?>? query,
        Func<HttpContent>? createContent,
        LinnworksSession session,
        LinnworksCredentials credentials,
        CancellationToken cancellationToken)
    {
        var uri = BuildUri(session.Server, path, query);

        using var lease = await rateLimiter
            .AcquireAsync(credentials.UserId, path, cancellationToken)
            .ConfigureAwait(false);

        if (!lease.IsAcquired)
        {
            throw new LinnworksApiException(
                LinnworksErrorKind.RateLimited,
                "Linnworks API is rate-limiting requests — please retry shortly.",
                $"Client-side rate limit rejected the call to {path}.");
        }

        using var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("Authorization", session.Token);

        if (createContent is not null)
        {
            request.Content = createContent();
        }

        logger.LogInformation("Linnworks {Method} {Path}", method.Method, path);

        var stopwatch = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            metrics.RecordUpstreamCall(path, stopwatch.Elapsed, success: false);
            throw LinnworksApiException.Timeout(path);
        }
        catch (HttpRequestException ex)
        {
            metrics.RecordUpstreamCall(path, stopwatch.Elapsed, success: false);
            throw new LinnworksApiException(
                LinnworksErrorKind.Unavailable,
                "Linnworks API is currently unavailable.",
                $"Transport failure calling {path}.",
                innerException: ex);
        }

        using (response)
        {
            stopwatch.Stop();
            metrics.RecordUpstreamCall(path, stopwatch.Elapsed, response.IsSuccessStatusCode);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content
                    .ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    metrics.RecordThrottled(path);
                }

                logger.LogError(
                    "Linnworks {Method} {Path} failed with {StatusCode} after {DurationMs}ms",
                    method.Method,
                    path,
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds);

                logger.LogDebug(
                    "Reproduce with: {CurlCommand}",
                    BuildCurlCommand(method, uri, createContent is not null));

                throw LinnworksApiException.FromStatusCode(response.StatusCode, path, errorBody);
            }

            logger.LogInformation(
                "Linnworks {Method} {Path} succeeded in {DurationMs}ms",
                method.Method,
                path,
                stopwatch.ElapsedMilliseconds);

            try
            {
                var result = await response.Content
                    .ReadFromJsonAsync<TResponse>(cancellationToken)
                    .ConfigureAwait(false);

                if (result is null)
                {
                    throw new LinnworksApiException(
                        LinnworksErrorKind.UpstreamError,
                        "Linnworks returned an empty response.",
                        $"Linnworks returned null for {path}.");
                }

                return result;
            }
            catch (JsonException ex)
            {
                throw LinnworksApiException.Deserialization(path, ex);
            }
        }
    }

    internal static Uri BuildUri(string server, string path, IReadOnlyDictionary<string, string?>? query)
    {
        var baseUrl = server.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? server.TrimEnd('/')
            : $"https://{server.TrimEnd('/')}";

        var builder = new UriBuilder($"{baseUrl}{path}");

        if (query is { Count: > 0 })
        {
            var pairs = query
                .Where(kvp => kvp.Value is not null)
                .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}");

            builder.Query = string.Join('&', pairs);
        }

        return builder.Uri;
    }

    private static string BuildCurlCommand(HttpMethod method, Uri uri, bool hasBody)
    {
        var body = hasBody ? " -d '<request body omitted>'" : string.Empty;
        return $"curl -X {method.Method} \"{uri}\" -H \"Content-Type: application/json\" "
             + $"-H \"Authorization: <REDACTED>\"{body}";
    }
}

