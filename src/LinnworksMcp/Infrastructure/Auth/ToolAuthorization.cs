using LinnworksMcp.Infrastructure.Linnworks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinnworksMcp.Infrastructure.Auth;

/// <summary>
/// Policy check that runs before a tool body executes.
/// </summary>
public interface IToolAuthorizer
{
    /// <exception cref="LinnworksApiException">Thrown when the caller may not invoke the tool.</exception>
    Task AuthorizeAsync(string toolName, bool destructive, CancellationToken cancellationToken);
}

public sealed class McpAuthOptions
{
    public const string SectionName = "McpAuth";

    /// <summary>Single API key helper (e.g., McpAuth__ApiKey=key).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Bearer or X-Api-Key values permitted to connect over the network transport.</summary>
    public string[] ClientApiKeys { get; set; } = [];

    /// <summary>
    /// Returns all configured valid API keys (combining ApiKey and ClientApiKeys).
    /// </summary>
    public IReadOnlyList<string> GetAllValidKeys()
    {
        var list = new List<string>(ClientApiKeys);
        if (!string.IsNullOrWhiteSpace(ApiKey) && !list.Contains(ApiKey))
        {
            list.Add(ApiKey);
        }
        return list;
    }

    /// <summary>
    /// When false, tools that create, update or delete data are refused.
    /// </summary>
    public bool AllowDestructiveTools { get; set; } = true;

    /// <summary>
    /// Allows unauthenticated callers to run discovery methods (initialize, tools/list, ping)
    /// so a client's capability probe succeeds without a key. Tool execution still requires one.
    /// Defaults to false: opening discovery leaks the tool catalogue, so it should be a
    /// deliberate choice rather than something a probe failure pressures you into.
    /// </summary>
    public bool AllowAnonymousDiscovery { get; set; }

    /// <summary>
    /// API keys allowed to call destructive tools, when destructive tools are enabled.
    /// Empty means every authenticated client may.
    /// </summary>
    public string[] DestructiveToolApiKeys { get; set; } = [];
}

public sealed class ToolAuthorizer(
    IOptions<McpAuthOptions> options,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ToolAuthorizer> logger) : IToolAuthorizer
{
    private readonly McpAuthOptions _options = options.Value;

    public Task AuthorizeAsync(string toolName, bool destructive, CancellationToken cancellationToken)
    {
        var validKeys = _options.GetAllValidKeys();
        if (validKeys.Count > 0)
        {
            var context = httpContextAccessor.HttpContext;
            var presentedKey = context != null ? ApiKeyAuthenticationHandler.ExtractApiKey(context.Request) : null;

            if (string.IsNullOrWhiteSpace(presentedKey) || !validKeys.Any(k => string.Equals(k, presentedKey, StringComparison.Ordinal)))
            {
                logger.LogWarning("Refused tool {Tool}: invalid or missing MCP client API key", toolName);
                throw new LinnworksApiException(
                    LinnworksErrorKind.Authentication,
                    "Invalid or missing MCP client API key. Pass 'X-Api-Key' or 'Authorization: Bearer <key>'.",
                    "Tool execution refused — presented API key did not match McpAuth:ApiKey.");
            }
        }

        if (!destructive)
        {
            return Task.CompletedTask;
        }

        if (!_options.AllowDestructiveTools)
        {
            logger.LogWarning("Refused destructive tool {Tool}: destructive tools are disabled", toolName);

            throw new LinnworksApiException(
                LinnworksErrorKind.Validation,
                $"The tool '{toolName}' modifies Linnworks data and is disabled on this server. "
                + "Ask an administrator to enable McpAuth:AllowDestructiveTools.",
                $"Destructive tool '{toolName}' refused — AllowDestructiveTools is false.");
        }

        if (_options.DestructiveToolApiKeys.Length > 0 && !CallerMayMutate())
        {
            logger.LogWarning("Refused destructive tool {Tool}: caller is not permitted", toolName);

            throw new LinnworksApiException(
                LinnworksErrorKind.Validation,
                $"You are not permitted to call '{toolName}', which modifies Linnworks data.",
                $"Destructive tool '{toolName}' refused — caller key not in DestructiveToolApiKeys.");
        }

        return Task.CompletedTask;
    }

    private bool CallerMayMutate()
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null)
        {
            return true;
        }

        var key = ApiKeyAuthenticationHandler.ExtractApiKey(context.Request);
        return key is not null && _options.DestructiveToolApiKeys.Contains(key, StringComparer.Ordinal);
    }
}

/// <summary>Authorizer used by the stdio transport.</summary>
public sealed class StdioToolAuthorizer(IOptions<McpAuthOptions> options, ILogger<StdioToolAuthorizer> logger)
    : IToolAuthorizer
{
    public Task AuthorizeAsync(string toolName, bool destructive, CancellationToken cancellationToken)
    {
        if (destructive && !options.Value.AllowDestructiveTools)
        {
            logger.LogWarning("Refused destructive tool {Tool}: destructive tools are disabled", toolName);

            throw new LinnworksApiException(
                LinnworksErrorKind.Validation,
                $"The tool '{toolName}' modifies Linnworks data and is disabled on this server.",
                $"Destructive tool '{toolName}' refused — AllowDestructiveTools is false.");
        }

        return Task.CompletedTask;
    }
}
