using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinnworksMcp.Infrastructure.Auth;

/// <summary>
/// Authenticates connecting MCP clients with a static API key (Bearer or X-Api-Key header).
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<McpAuthOptions> mcpOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string ApiKeyHeader = "X-Api-Key";

    private readonly McpAuthOptions _mcpOptions = mcpOptions.Value;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var validKeys = _mcpOptions.GetAllValidKeys();

        if (validKeys.Count == 0)
        {
            // Fail closed: If no key is configured, refuse all incoming connections.
            return Task.FromResult(AuthenticateResult.Fail(
                "No MCP client API keys are configured on the server; refusing all connection requests."));
        }

        var presented = ExtractApiKey(Request);
        if (string.IsNullOrWhiteSpace(presented))
        {
            // Return NoResult instead of Fail so anonymous discovery checks get 200 OK (not 401 challenge),
            // while ToolAuthorizer enforces API key check on tool execution.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!IsKnownKey(presented, validKeys))
        {
            Logger.LogWarning("Rejected MCP client connection request with an invalid API key");
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "mcp-client")], SchemeName);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    /// <summary>
    /// Extracts API Key from 'Authorization: Bearer <key>' or 'X-Api-Key: <key>' header.
    /// </summary>
    public static string? ExtractApiKey(Microsoft.AspNetCore.Http.HttpRequest request)
    {
        // 1. Check Authorization: Bearer <key>
        var authHeader = request.Headers.Authorization.ToString();
        var bearerToken = ExtractBearerToken(authHeader);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            return bearerToken;
        }

        // 2. Check X-Api-Key header
        if (request.Headers.TryGetValue(ApiKeyHeader, out var xApiKeyValues) &&
            !string.IsNullOrWhiteSpace(xApiKeyValues.ToString()))
        {
            return xApiKeyValues.ToString();
        }

        return null;
    }

    private static bool IsKnownKey(string presented, IReadOnlyList<string> validKeys)
    {
        var presentedBytes = Encoding.UTF8.GetBytes(presented);
        var matched = false;

        foreach (var candidate in validKeys)
        {
            var candidateBytes = Encoding.UTF8.GetBytes(candidate);
            matched |= presentedBytes.Length == candidateBytes.Length
                && CryptographicOperations.FixedTimeEquals(presentedBytes, candidateBytes);
        }

        return matched;
    }

    internal static string? ExtractBearerToken(string? authorizationHeader) =>
        !string.IsNullOrWhiteSpace(authorizationHeader)
        && AuthenticationHeaderValue.TryParse(authorizationHeader, out var parsed)
        && string.Equals(parsed.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(parsed.Parameter)
            ? parsed.Parameter
            : null;
}
