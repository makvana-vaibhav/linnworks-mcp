using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LinnworksMcp.Infrastructure.Linnworks;

public interface ILinnworksCredentialProvider
{
    /// <summary>
    /// Resolves the calling tenant's credentials.
    /// </summary>
    LinnworksCredentials GetCredentials();
}

/// <summary>
/// Hybrid credential provider:
/// 1. If HTTP headers (X-Linnworks-*) are present in the request, uses per-request user credentials (SaaS chatbot mode).
/// 2. If headers are absent, falls back to server-configured credentials (Claude Web Connector mode).
/// </summary>
public sealed class HeaderLinnworksCredentialProvider(
    IHttpContextAccessor httpContextAccessor,
    IOptions<LinnworksOptions> options)
    : ILinnworksCredentialProvider
{
    public const string UserIdHeader = "X-Linnworks-User-Id";
    public const string ApplicationIdHeader = "X-Linnworks-Application-Id";
    public const string ApplicationSecretHeader = "X-Linnworks-Application-Secret";
    public const string TokenHeader = "X-Linnworks-Token";

    public LinnworksCredentials GetCredentials()
    {
        var context = httpContextAccessor.HttpContext;

        if (context is not null)
        {
            var userId = Read(context, UserIdHeader);
            var applicationId = Read(context, ApplicationIdHeader);
            var applicationSecret = Read(context, ApplicationSecretHeader);
            var token = Read(context, TokenHeader);

            // If per-request header credentials are fully provided, use them (SaaS multi-tenant chatbot mode)
            if (!string.IsNullOrWhiteSpace(applicationId) &&
                !string.IsNullOrWhiteSpace(applicationSecret) &&
                !string.IsNullOrWhiteSpace(token))
            {
                return new LinnworksCredentials(
                    string.IsNullOrWhiteSpace(userId) ? "header-user" : userId,
                    applicationId,
                    applicationSecret,
                    token);
            }
        }

        // Fallback: If headers are missing (e.g. Claude Web connector), use server-configured default credentials
        var fallback = options.Value.Stdio;
        if (fallback.IsComplete)
        {
            return new LinnworksCredentials(
                string.IsNullOrWhiteSpace(fallback.UserId) ? "server-default-user" : fallback.UserId,
                fallback.ApplicationId!,
                fallback.ApplicationSecret!,
                fallback.Token!);
        }

        throw new LinnworksApiException(
            LinnworksErrorKind.Validation,
            "Linnworks credentials were not supplied. Pass X-Linnworks-* HTTP headers or configure server defaults (Linnworks__Stdio__ApplicationId, etc.).",
            "Neither X-Linnworks-* headers nor server fallback environment variables were found.");
    }

    private static string? Read(HttpContext context, string header) =>
        context.Request.Headers.TryGetValue(header, out var values) ? values.ToString() : null;
}

/// <summary>
/// Supplies credentials from configuration. Used by the stdio transport.
/// </summary>
public sealed class ConfiguredLinnworksCredentialProvider(IOptions<LinnworksOptions> options)
    : ILinnworksCredentialProvider
{
    private readonly LinnworksOptions.StdioCredentialOptions _stdio = options.Value.Stdio;

    public LinnworksCredentials GetCredentials()
    {
        if (!_stdio.IsComplete)
        {
            throw new LinnworksApiException(
                LinnworksErrorKind.Validation,
                "The server has no Linnworks credentials configured. Set Linnworks__Stdio__ApplicationId, "
                + "Linnworks__Stdio__ApplicationSecret and Linnworks__Stdio__Token.",
                "stdio credentials are missing or incomplete in configuration.");
        }

        return new LinnworksCredentials(
            string.IsNullOrWhiteSpace(_stdio.UserId) ? "stdio" : _stdio.UserId,
            _stdio.ApplicationId!,
            _stdio.ApplicationSecret!,
            _stdio.Token!);
    }
}
