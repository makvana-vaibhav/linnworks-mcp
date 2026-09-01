using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace LinnworksMcp.Infrastructure.Linnworks;

/// <summary>
/// Client-side throttle that keeps us inside Linnworks' documented per-endpoint limits rather
/// than discovering them via 429s. Limits are published per endpoint at
/// https://apidocs.linnworks.net/reference/ and are almost always 150 or 250 per minute.
/// </summary>
/// <remarks>
/// Buckets are keyed by tenant *and* endpoint: Linnworks meters per account, so one busy tenant
/// must not consume another's allowance.
/// </remarks>
public sealed class EndpointRateLimiter : IAsyncDisposable
{
    /// <summary>
    /// Documented limits, in requests per minute, for the endpoints this server calls.
    /// Anything absent falls back to <see cref="LinnworksOptions.DefaultRateLimitPerMinute"/>.
    /// </summary>
    private static readonly Dictionary<string, int> DocumentedLimits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/api/Stock/GetStockItemsFull"] = 150,
        ["/api/Stock/GetStockLevel_Batch"] = 250,
        ["/api/Stock/SetStockLevel"] = 150,
        ["/api/Stock/GetItemChangesHistory"] = 250,
        ["/api/Inventory/GetInventoryItemById"] = 150,
        ["/api/Inventory/GetStockLocations"] = 150,
        ["/api/Inventory/GetInventoryItemPrices"] = 150,
        ["/api/Inventory/UpdateInventoryItem"] = 150,
        ["/api/Inventory/AddInventoryItem"] = 150,
        ["/api/Inventory/DeleteInventoryItems"] = 150,
        ["/api/Dashboards/GetLowStockLevel"] = 150,
        ["/api/OpenOrders/GetOpenOrders"] = 150,
        ["/api/Orders/GetOrdersById"] = 250,
        ["/api/Orders/GetOrderDetailsByNumOrderId"] = 250,
    };

    private readonly ConcurrentDictionary<string, RateLimiter> _limiters = new(StringComparer.Ordinal);
    private readonly LinnworksOptions _options;

    public EndpointRateLimiter(IOptions<LinnworksOptions> options) => _options = options.Value;

    /// <summary>
    /// Waits for permission to call <paramref name="path"/> on behalf of <paramref name="userId"/>.
    /// The returned lease must be disposed by the caller.
    /// </summary>
    public ValueTask<RateLimitLease> AcquireAsync(
        string userId,
        string path,
        CancellationToken cancellationToken)
    {
        var limiter = _limiters.GetOrAdd($"{userId}|{path}", _ => CreateLimiter(path));
        return limiter.AcquireAsync(permitCount: 1, cancellationToken);
    }

    private RateLimiter CreateLimiter(string path)
    {
        var permitsPerMinute = DocumentedLimits.TryGetValue(path, out var documented)
            ? documented
            : _options.DefaultRateLimitPerMinute;

        // Refill continuously over the minute rather than all at once, so a burst at the top of
        // a window cannot exhaust the whole allowance.
        return new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = permitsPerMinute,
            TokensPerPeriod = Math.Max(1, permitsPerMinute / 60),
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = permitsPerMinute,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var limiter in _limiters.Values)
        {
            await limiter.DisposeAsync().ConfigureAwait(false);
        }

        _limiters.Clear();
    }
}
