using System.Security.Cryptography;
using System.Text;

namespace LinnworksMcp.Infrastructure.Linnworks;

/// <summary>
/// One tenant's Linnworks credentials. Held in memory only, never persisted, never logged.
/// </summary>
public sealed record LinnworksCredentials(
    string UserId,
    string ApplicationId,
    string ApplicationSecret,
    string Token)
{
    /// <summary>
    /// Stable fingerprint of the secret material, used to detect that a user id has been
    /// re-presented with different credentials so the cached session can be evicted.
    /// Hashed so the cache never holds the raw secret alongside the session.
    /// </summary>
    public string Fingerprint => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes($"{ApplicationId}{ApplicationSecret}{Token}")));

    /// <summary>Redacted form for logging. Never returns secret material.</summary>
    public override string ToString() => $"LinnworksCredentials {{ UserId = {UserId} }}";
}
