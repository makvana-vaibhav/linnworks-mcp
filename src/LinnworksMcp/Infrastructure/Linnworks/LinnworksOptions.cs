namespace LinnworksMcp.Infrastructure.Linnworks;

/// <summary>Non-secret defaults live in appsettings.json; secrets come from env/user-secrets.</summary>
public sealed class LinnworksOptions
{
    public const string SectionName = "Linnworks";

    /// <summary>
    /// Authorization endpoint. Region routing is handled by the <c>Server</c> field of the
    /// auth response, so this is the only fixed Linnworks host.
    /// </summary>
    public string AuthUrl { get; set; } = "https://api.linnworks.net/api/Auth/AuthorizeByApplication";

    /// <summary>Refresh a cached session this long before its TTL expires.</summary>
    public TimeSpan SessionRefreshBuffer { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Default per-request HTTP timeout, independent of caller cancellation.</summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Total attempts (1 initial + retries) for a throttled or failing call.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Fallback requests/minute for endpoints with no entry in the documented-limit table.
    /// Linnworks documents per-endpoint limits of 150 or 250 per minute.
    /// </summary>
    public int DefaultRateLimitPerMinute { get; set; } = 150;

    /// <summary>
    /// Credentials used in stdio mode, where there are no HTTP headers to carry them.
    /// Leave empty for the HTTP transport, which takes credentials per request.
    /// </summary>
    public StdioCredentialOptions Stdio { get; set; } = new();

    public sealed class StdioCredentialOptions
    {
        public string? UserId { get; set; }

        public string? ApplicationId { get; set; }

        public string? ApplicationSecret { get; set; }

        public string? Token { get; set; }

        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(ApplicationId)
            && !string.IsNullOrWhiteSpace(ApplicationSecret)
            && !string.IsNullOrWhiteSpace(Token);
    }
}
