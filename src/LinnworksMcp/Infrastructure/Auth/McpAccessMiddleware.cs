using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinnworksMcp.Infrastructure.Auth;

/// <summary>
/// Gate middleware on the MCP endpoint verifying client API key access and discovery calls.
/// </summary>
public sealed class McpAccessMiddleware(
    RequestDelegate next,
    IOptions<McpAuthOptions> options,
    ILogger<McpAccessMiddleware> logger)
{
    private static readonly HashSet<string> DiscoveryMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "initialize", "ping", "tools/list", "prompts/list", "resources/list",
        "resources/templates/list", "notifications/initialized"
    };

    private const int MaxPeekBytes = 64 * 1024;
    private readonly McpAuthOptions _options = options.Value;

    private static async Task<string> ResolveMethodAsync(HttpContext context)
    {
        var header = context.Request.Headers["Mcp-Method"].ToString();
        if (!string.IsNullOrEmpty(header))
        {
            return header;
        }

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
            return string.Empty;
        }
        finally
        {
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
                await next(context).ConfigureAwait(false);
                return;
            }

            logger.LogError(
                "Refusing MCP request: no client API keys are configured. Set McpAuth__ApiKey.");

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "This server has no MCP client API key configured. Set McpAuth__ApiKey on the server."
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

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Unauthorized. Supply the configured MCP client API key as 'X-Api-Key: <key>' or 'Authorization: Bearer <key>'."
        }).ConfigureAwait(false);
    }
}

