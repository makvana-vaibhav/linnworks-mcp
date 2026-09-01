using System.Net;

namespace LinnworksMcp.Infrastructure.Linnworks;

/// <summary>
/// Every failure originating from <see cref="LinnworksClient"/> or
/// <see cref="LinnworksAuthManager"/> — HTTP error, timeout, deserialization failure,
/// auth failure — surfaces as this type.
/// </summary>
/// <remarks>
/// <see cref="SafeMessage"/> is what may cross the MCP boundary to a client.
/// <see cref="Exception.Message"/> carries the full upstream detail and is for logs only.
/// </remarks>
public sealed class LinnworksApiException : Exception
{
    public LinnworksApiException(
        LinnworksErrorKind kind,
        string safeMessage,
        string internalMessage,
        HttpStatusCode? statusCode = null,
        Exception? innerException = null)
        : base(internalMessage, innerException)
    {
        Kind = kind;
        SafeMessage = safeMessage;
        StatusCode = statusCode;
    }

    public LinnworksErrorKind Kind { get; }

    /// <summary>Sanitized message safe to return to an MCP client. Never contains secrets.</summary>
    public string SafeMessage { get; }

    public HttpStatusCode? StatusCode { get; }

    /// <summary>Maps an upstream status code onto the error contract.</summary>
    public static LinnworksApiException FromStatusCode(
        HttpStatusCode statusCode,
        string path,
        string responseBody)
    {
        var (kind, safeMessage) = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => (
                LinnworksErrorKind.Authentication,
                "Authentication failed — session may have expired or credentials are invalid."),

            HttpStatusCode.NotFound => (
                LinnworksErrorKind.NotFound,
                "The requested resource was not found."),

            HttpStatusCode.TooManyRequests => (
                LinnworksErrorKind.RateLimited,
                "Linnworks API is rate-limiting requests — please retry shortly."),

            >= HttpStatusCode.InternalServerError => (
                LinnworksErrorKind.Unavailable,
                "Linnworks API is currently unavailable."),

            _ => (
                LinnworksErrorKind.UpstreamError,
                $"Linnworks rejected the request ({(int)statusCode}).")
        };

        // Response body goes to the internal message only — it can echo request content.
        return new LinnworksApiException(
            kind,
            safeMessage,
            $"Linnworks API error [{(int)statusCode}] {path}: {responseBody}",
            statusCode);
    }

    public static LinnworksApiException Timeout(string path, Exception? inner = null) =>
        new(LinnworksErrorKind.Unavailable,
            "Linnworks API is currently unavailable.",
            $"Linnworks API call to {path} timed out.",
            innerException: inner);

    public static LinnworksApiException Deserialization(string path, Exception inner) =>
        new(LinnworksErrorKind.UpstreamError,
            "Linnworks returned a response that could not be understood.",
            $"Failed to deserialize the Linnworks response from {path}.",
            innerException: inner);
}

public enum LinnworksErrorKind
{
    Authentication,
    NotFound,
    RateLimited,
    Unavailable,
    UpstreamError,
    Validation
}
