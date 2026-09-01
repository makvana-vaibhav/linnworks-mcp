using System.Text.Json.Serialization;

namespace LinnworksMcp.Infrastructure.Linnworks;

/// <summary>
/// Response body of <c>POST /api/Auth/AuthorizeByApplication</c>.
/// Linnworks returns PascalCase, which matches these property names directly.
/// </summary>
public sealed class LinnworksSession
{
    public string Id { get; init; } = string.Empty;

    public string EntityId { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    /// <summary>Region the account is homed in — EU, US or AS.</summary>
    public string Locality { get; init; } = string.Empty;

    /// <summary>Seconds until this session expires.</summary>
    [JsonPropertyName("TTL")]
    public int Ttl { get; init; }

    /// <summary>
    /// The session token. This is what goes in the <c>Authorization</c> header of every
    /// subsequent call — raw, with no <c>Bearer</c> prefix. Not <see cref="AccessToken"/>,
    /// which is empty in practice.
    /// </summary>
    public string Token { get; init; } = string.Empty;

    public string AccessToken { get; init; } = string.Empty;

    /// <summary>
    /// Region-specific base URL for every subsequent API call (e.g. https://eu-ext.linnworks.net).
    /// Never hardcode a Linnworks host — always route through this.
    /// </summary>
    public string Server { get; init; } = string.Empty;

    public string PushServer { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string UserId { get; init; } = string.Empty;

    public string GroupName { get; init; } = string.Empty;

    public string UserType { get; init; } = string.Empty;

    public bool SuperAdmin { get; init; }

    public LinnworksSessionStatus? Status { get; init; }

    public Dictionary<string, string>? Properties { get; init; }
}

public sealed class LinnworksSessionStatus
{
    public string State { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public Dictionary<string, string>? Parameters { get; init; }
}
