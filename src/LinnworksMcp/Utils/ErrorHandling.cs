using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace LinnworksMcp.Utils;

/// <summary>
/// The boundary every MCP tool body runs inside. Guarantees three things: a raw .NET exception
/// can never reach a client, every invocation is logged and measured, and every response is a
/// JSON string with a predictable shape.
/// </summary>
public static class ToolExecution
{
    internal static readonly JsonSerializerOptions ResponseOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static async Task<string> RunAsync<T>(
        string toolName,
        ILogger logger,
        ToolMetrics metrics,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        using var scope = CorrelationId.BeginScope();
        var stopwatch = Stopwatch.StartNew();

        using var loggerScope = logger.BeginScope(new Dictionary<string, object>
        {
            ["tool"] = toolName,
            ["correlation_id"] = CorrelationId.Value
        });

        logger.LogInformation("Tool {Tool} invoked", toolName);

        try
        {
            var result = await operation(cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            metrics.RecordToolCall(toolName, stopwatch.Elapsed, success: true);
            logger.LogInformation(
                "Tool {Tool} succeeded in {DurationMs}ms", toolName, stopwatch.ElapsedMilliseconds);

            return JsonSerializer.Serialize(result, ResponseOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The client went away or aborted the call; nothing to report back to it.
            stopwatch.Stop();
            metrics.RecordToolCall(toolName, stopwatch.Elapsed, success: false);
            logger.LogInformation("Tool {Tool} was cancelled by the caller", toolName);
            throw;
        }
        catch (LinnworksApiException ex)
        {
            stopwatch.Stop();
            metrics.RecordToolCall(toolName, stopwatch.Elapsed, success: false);

            // Full detail — including the upstream body — stays in the log.
            logger.LogError(
                ex,
                "Tool {Tool} failed after {DurationMs}ms with {Kind}",
                toolName,
                stopwatch.ElapsedMilliseconds,
                ex.Kind);

            // Only the sanitized message crosses the boundary.
            throw new McpException($"{ex.SafeMessage} (correlation id: {CorrelationId.Value})");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            metrics.RecordToolCall(toolName, stopwatch.Elapsed, success: false);

            logger.LogError(
                ex, "Tool {Tool} failed unexpectedly after {DurationMs}ms",
                toolName, stopwatch.ElapsedMilliseconds);

            // Never surface the exception text — it can carry paths, config or payload fragments.
            throw new McpException(
                $"The tool failed unexpectedly. (correlation id: {CorrelationId.Value})");
        }
    }
}

/// <summary>Parameter validation that runs before any Linnworks call is made.</summary>
public static class ToolValidation
{
    public const int MaxPageSize = 200;
    public const int DefaultPageSize = 50;

    /// <summary>
    /// Validates paging inputs. Rejects rather than silently clamps, so a caller asking for 500
    /// records learns the cap exists instead of quietly receiving 200.
    /// </summary>
    public static (int PageNumber, int PageSize) Paging(int pageNumber, int pageSize)
    {
        if (pageNumber <= 0)
        {
            throw Invalid(nameof(pageNumber), $"must be 1 or greater (received {pageNumber}).");
        }

        if (pageSize <= 0)
        {
            throw Invalid(nameof(pageSize), $"must be 1 or greater (received {pageSize}).");
        }

        if (pageSize > MaxPageSize)
        {
            throw Invalid(
                nameof(pageSize),
                $"must not exceed {MaxPageSize} (received {pageSize}). "
                + "Request a smaller page and page through the results.");
        }

        return (pageNumber, pageSize);
    }

    public static string RequiredGuid(string parameterName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(parameterName, "is required.");
        }

        if (!Guid.TryParse(value, out var parsed))
        {
            throw Invalid(parameterName, "must be a valid UUID.");
        }

        return parsed.ToString();
    }

    public static string? OptionalGuid(string parameterName, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : RequiredGuid(parameterName, value);

    public static string RequiredText(string parameterName, string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw Invalid(parameterName, "is required.")
            : value;

    public static LinnworksApiException Invalid(string parameterName, string problem) =>
        new(LinnworksErrorKind.Validation,
            $"Invalid value for '{parameterName}' — it {problem}",
            $"Validation failed for parameter '{parameterName}': {problem}");
}
