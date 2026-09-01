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

    private readonly McpAuthOptions _options = options.Value;

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

        // The 2026-07-28 revision requires clients to mirror the JSON-RPC method into this
        // header, so the request can be classified without buffering the body.
        var method = context.Request.Headers["Mcp-Method"].ToString();

        if (_options.AllowAnonymousDiscovery && DiscoveryMethods.Contains(method))
        {
            logger.LogDebug("Allowing anonymous MCP discovery call to {Method}", method);
            await next(context).ConfigureAwait(false);
            return;
        }

        logger.LogWarning(
            "Refused unauthenticated MCP request (method {Method})",
            string.IsNullOrEmpty(method) ? "<none>" : method);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer realm=\"linnworks-mcp\"";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Unauthorized. Supply a configured MCP client API key as "
                  + "'Authorization: Bearer <key>' or 'X-Api-Key: <key>'."
        }).ConfigureAwait(false);
    }
}
