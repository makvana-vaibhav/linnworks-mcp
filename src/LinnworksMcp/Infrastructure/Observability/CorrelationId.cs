using Microsoft.AspNetCore.Http;

namespace LinnworksMcp.Infrastructure.Observability;

/// <summary>
/// Ambient correlation id for the current logical operation, so one tool call can be traced
/// from the MCP boundary through the service layer to the Linnworks HTTP call.
/// </summary>
/// <remarks>
/// An <see cref="AsyncLocal{T}"/> rather than a scoped service because the stdio transport has
/// no request scope to hang one off — this works identically under both transports.
/// </remarks>
public static class CorrelationId
{
    private static readonly AsyncLocal<string?> Current = new();

    /// <summary>The active correlation id, or "system" outside any tracked operation.</summary>
    public static string Value => Current.Value ?? "system";

    public static void Set(string correlationId) => Current.Value = correlationId;

    /// <summary>Begins a new correlation scope, restoring the previous id on dispose.</summary>
    public static IDisposable BeginScope(string? correlationId = null)
    {
        var previous = Current.Value;
        Current.Value = correlationId ?? Guid.NewGuid().ToString();
        return new Scope(previous);
    }

    private sealed class Scope(string? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}

/// <summary>
/// Accepts a caller-supplied correlation id, or mints one, and echoes it back on the response
/// so a chatbot can tie its own logs to this server's.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers.TryGetValue(HeaderName, out var values)
            ? values.ToString()
            : null;

        var correlationId = string.IsNullOrWhiteSpace(incoming)
            ? context.TraceIdentifier
            : incoming;

        using (CorrelationId.BeginScope(correlationId))
        {
            context.Response.Headers[HeaderName] = correlationId;
            await next(context).ConfigureAwait(false);
        }
    }
}
