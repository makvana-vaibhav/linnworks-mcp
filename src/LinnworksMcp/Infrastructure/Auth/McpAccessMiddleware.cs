using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinnworksMcp.Infrastructure.Auth;

/// <summary>
/// Gate on the MCP endpoint. Authenticated callers pass; everyone else is refused with a 401,
/// except that discovery may optionally be opened up without opening up execution.
/// </summary>
/// <remarks>
/// This exists because relying on <c>RequireAuthorization</c> alone proved fragile: an
/// authentication handler that suppresses its challenge turns the policy into a no-op and the
/// endpoint answers 200 to anonymous callers. That matters here more than on a typical API —
/// when server-side Linnworks credentials are configured as a fallback, an unauthenticated
/// caller would be operating a real Linnworks account, mutating tools included.
/// </remarks>
public sealed class McpAccessMiddleware(
    RequestDelegate next,
    IOptions<McpAuthOptions> options,
    ILogger<McpAccessMiddleware> logger)
{
    /// <summary>
    /// Methods that only describe the server. They expose tool names and schemas but cannot
    /// touch Linnworks, so they are safe to open up when a client's probe needs it.
    /// </summary>
    private static readonly HashSet<string> DiscoveryMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "initialize", "ping", "tools/list", "prompts/list", "resources/list",
        "resources/templates/list", "notifications/initialized"
    };

    /// <summary>Cap on how much request body will be buffered to identify the method.</summary>
    private const int MaxPeekBytes = 64 * 1024;

    private readonly McpAuthOptions _options = options.Value;

    /// <summary>
    /// Identifies the JSON-RPC method being invoked.
    /// </summary>
    /// <remarks>
    /// The <c>Mcp-Method</c> header only exists from the 2026-07-28 revision onwards. Claude's
    /// <c>initialize</c> handshake predates it and sends the method in the body alone, so
    /// header-only classification treats every connection attempt as non-discovery and answers
    /// 401 — which the client reports as an authentication failure. Falling back to the body
    /// keeps older clients working. The body is buffered and rewound so the MCP handler can
    /// still read it.
    /// </remarks>
    private static async Task<string> ResolveMethodAsync(HttpContext context)
    {
        var header = context.Request.Headers["Mcp-Method"].ToString();
        if (!string.IsNullOrEmpty(header))
        {
            return header;
        }

        // Anything oversized is not a discovery call; leave it to the authenticated path.
        if (context.Request.ContentLength is null or 0 or > MaxPeekBytes)
        {
            return string.Empty;
        }

        try
        {
            context.Request.EnableBuffering();
            using var document = await JsonDocument
                .ParseAsync(context.Request.Body).ConfigureAwait(false);

            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("method", out var m)
                && m.ValueKind == JsonValueKind.String
                    ? m.GetString() ?? string.Empty
                    : string.Empty;
        }
        catch (JsonException)
        {
            // Not JSON we understand; the MCP handler will reject it on its own terms.
            return string.Empty;
        }
        finally
        {
            // Rewind unconditionally so the handler downstream sees the full body.
            context.Request.Body.Position = 0;
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // The key is validated here rather than deferred to an authentication scheme, so this
        // gate holds even when no scheme is registered.
        var validKeys = _options.GetAllValidKeys();
        var presented = ApiKeyAuthenticationHandler.ExtractApiKey(context.Request);

        if (validKeys.Count > 0
            && !string.IsNullOrWhiteSpace(presented)
            && validKeys.Any(k => string.Equals(k, presented, StringComparison.Ordinal)))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (validKeys.Count == 0)
        {
            if (!_options.RequireApiKey)
            {
                // Explicitly opted out of authentication; the startup warning covers the risk.
                await next(context).ConfigureAwait(false);
                return;
            }

            // No keys configured and none waived. Refuse rather than run open — with
            // server-side Linnworks credentials in play, an open endpoint operates a real
            // account.
            logger.LogError(
                "Refusing MCP request: no client API keys are configured. Set McpAuth__ApiKey "
                + "(or set McpAuth__RequireApiKey=false to intentionally run without auth).");

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "This server has no MCP client API key configured. Set McpAuth__ApiKey "
                      + "on the server, then supply it as 'X-Api-Key' or "
                      + "'Authorization: Bearer <key>'."
            }).ConfigureAwait(false);
            return;
        }

        var method = await ResolveMethodAsync(context).ConfigureAwait(false);

        if (_options.AllowAnonymousDiscovery && DiscoveryMethods.Contains(method))
        {
            logger.LogDebug("Allowing anonymous MCP discovery call to {Method}", method);
            await next(context).ConfigureAwait(false);
            return;
        }

        logger.LogWarning(
            "Refused unauthenticated MCP request (method {Method})",
            string.IsNullOrEmpty(method) ? "<none>" : method);

        // Deliberately no WWW-Authenticate header. This server authenticates with a static
        // API key, not OAuth, and Claude treats a 401 carrying WWW-Authenticate as the start of
        // an OAuth handshake: it probes /.well-known/oauth-protected-resource, finds nothing,
        // and reports "Authentication failed" instead of surfacing the real problem.
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Unauthorized. Supply the configured MCP client API key as "
                  + "'X-Api-Key: <key>' or 'Authorization: Bearer <key>'."
        }).ConfigureAwait(false);
    }
}
