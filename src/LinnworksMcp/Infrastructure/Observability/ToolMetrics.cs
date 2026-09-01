using System.Diagnostics.Metrics;

namespace LinnworksMcp.Infrastructure.Observability;

/// <summary>
/// Instruments tool invocations and upstream Linnworks calls. Registered as a singleton.
/// </summary>
public sealed class ToolMetrics : IDisposable
{
    public const string MeterName = "LinnworksMcp";

    private readonly Meter _meter;
    private readonly Counter<long> _toolCalls;
    private readonly Counter<long> _toolErrors;
    private readonly Histogram<double> _toolDuration;
    private readonly Histogram<double> _upstreamDuration;
    private readonly Counter<long> _upstreamThrottled;
    private readonly Counter<long> _upstreamRetries;

    public ToolMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _toolCalls = _meter.CreateCounter<long>(
            "linnworks_mcp.tool.calls", "{call}", "Number of MCP tool invocations.");

        _toolErrors = _meter.CreateCounter<long>(
            "linnworks_mcp.tool.errors", "{error}", "Number of MCP tool invocations that failed.");

        _toolDuration = _meter.CreateHistogram<double>(
            "linnworks_mcp.tool.duration", "ms", "Duration of MCP tool invocations.");

        _upstreamDuration = _meter.CreateHistogram<double>(
            "linnworks_mcp.upstream.duration", "ms", "Latency of calls to the Linnworks API.");

        _upstreamThrottled = _meter.CreateCounter<long>(
            "linnworks_mcp.upstream.throttled", "{response}", "Number of 429 responses from Linnworks.");

        _upstreamRetries = _meter.CreateCounter<long>(
            "linnworks_mcp.upstream.retries", "{retry}", "Number of retried Linnworks calls.");
    }

    public void RecordToolCall(string toolName, TimeSpan duration, bool success)
    {
        var tag = new KeyValuePair<string, object?>("tool", toolName);

        _toolCalls.Add(1, tag);
        _toolDuration.Record(duration.TotalMilliseconds, tag);

        if (!success)
        {
            _toolErrors.Add(1, tag);
        }
    }

    public void RecordUpstreamCall(string path, TimeSpan duration, bool success) =>
        _upstreamDuration.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("endpoint", path),
            new KeyValuePair<string, object?>("success", success));

    public void RecordThrottled(string path) =>
        _upstreamThrottled.Add(1, new KeyValuePair<string, object?>("endpoint", path));

    public void RecordRetry(string path) =>
        _upstreamRetries.Add(1, new KeyValuePair<string, object?>("endpoint", path));

    public void Dispose() => _meter.Dispose();
}
