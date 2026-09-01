using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinnworksMcp.Application.Locations;
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
            stopwatch.Stop();
            metrics.RecordToolCall(toolName, stopwatch.Elapsed, success: false);
            logger.LogInformation("Tool {Tool} was cancelled by the caller", toolName);
            throw;
        }
        catch (LinnworksApiException ex)
        {
            stopwatch.Stop();
            metrics.RecordToolCall(toolName, stopwatch.Elapsed, success: false);

            logger.LogError(
                ex,
                "Tool {Tool} failed after {DurationMs}ms with {Kind}",
                toolName,
                stopwatch.ElapsedMilliseconds,
                ex.Kind);

            throw new McpException($"{ex.SafeMessage} (correlation id: {CorrelationId.Value})");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            metrics.RecordToolCall(toolName, stopwatch.Elapsed, success: false);

            logger.LogError(
                ex, "Tool {Tool} failed unexpectedly after {DurationMs}ms",
                toolName, stopwatch.ElapsedMilliseconds);

            throw new McpException(
                $"The tool failed unexpectedly. (correlation id: {CorrelationId.Value})");
        }
    }
}

/// <summary>Parameter validation and smart resolution that runs before any Linnworks call is made.</summary>
public static class ToolValidation
{
    public const int MaxPageSize = 200;
    public const int DefaultPageSize = 50;
    public const string DefaultAllLocationsGuid = "00000000-0000-0000-0000-000000000000";

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

    /// <summary>
    /// Smart location resolver: Accepts GUIDs, 'all', 'default', location names, or null/empty.
    /// Maps to exact UUID expected by Linnworks endpoints.
    /// </summary>
    public static async Task<string> ResolveLocationGuidAsync(
        string? locationIdOrName,
        LocationService locationService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(locationIdOrName) ||
            string.Equals(locationIdOrName, "all", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(locationIdOrName, "all locations", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(locationIdOrName, "default", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(locationIdOrName, DefaultAllLocationsGuid, StringComparison.OrdinalIgnoreCase))
        {
            return DefaultAllLocationsGuid;
        }

        if (Guid.TryParse(locationIdOrName, out var parsedGuid))
        {
            return parsedGuid.ToString();
        }

        // Try matching by location name
        try
        {
            var locationsPage = await locationService.GetLocationsAsync(1, 200, ct).ConfigureAwait(false);
            var matched = locationsPage.Items.FirstOrDefault(l =>
                string.Equals(l.LocationName, locationIdOrName, StringComparison.OrdinalIgnoreCase) ||
                (l.LocationName?.Contains(locationIdOrName, StringComparison.OrdinalIgnoreCase) ?? false));

            if (matched != null)
            {
                return matched.StockLocationId;
            }
        }
        catch
        {
            // Ignore lookup failure and fall back to default
        }

        return DefaultAllLocationsGuid;
    }

    public static string RequiredText(string parameterName, string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw Invalid(parameterName, "is required.")
            : value;

    public static LinnworksApiException Invalid(string parameterName, string problem) =>
        new(LinnworksErrorKind.Validation,
            $"Invalid value for '{parameterName}' — it {problem}",
            $"Validation failed for parameter '{parameterName}': {problem}");
}
