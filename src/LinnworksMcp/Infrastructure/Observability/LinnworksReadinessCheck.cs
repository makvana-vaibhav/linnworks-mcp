using LinnworksMcp.Infrastructure.Linnworks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LinnworksMcp.Infrastructure.Observability;

/// <summary>
/// Readiness probe: is the configuration valid and is Linnworks reachable?
/// </summary>
/// <remarks>
/// Credentials arrive per request, so there are none to authenticate with here. The check
/// therefore verifies reachability of the authorization host rather than a successful login —
/// enough to catch DNS failures, egress blocks and a Linnworks outage, without needing a
/// service account.
/// </remarks>
public sealed class LinnworksReadinessCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<LinnworksOptions> options) : IHealthCheck
{
    private readonly LinnworksOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(_options.AuthUrl, UriKind.Absolute, out var authUri))
        {
            return HealthCheckResult.Unhealthy(
                $"Linnworks:AuthUrl is not a valid absolute URL ('{_options.AuthUrl}').");
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            // An unauthenticated POST is expected to be rejected. Any HTTP response at all
            // proves the endpoint is reachable, which is what readiness is asking.
            using var request = new HttpRequestMessage(HttpMethod.Head, authUri);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy(
                $"Linnworks reachable at {authUri.Host} (HTTP {(int)response.StatusCode}).");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Linnworks unreachable at {authUri.Host}.", ex);
        }
    }
}
