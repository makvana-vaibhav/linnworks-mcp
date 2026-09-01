using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Models;
using Microsoft.Extensions.Logging;

namespace LinnworksMcp.Application.Stock;

/// <summary>Stock level reads and adjustments.</summary>
public sealed class StockService(ILinnworksClient client, ILogger<StockService> logger)
{
    internal const string GetStockLevelBatchPath = "/api/Stock/GetStockLevel_Batch";
    internal const string SetStockLevelPath = "/api/Stock/SetStockLevel";

    /// <summary>
    /// Per-location stock levels for a set of items, via
    /// <c>POST /api/Stock/GetStockLevel_Batch</c>.
    /// </summary>
    /// <remarks>
    /// The endpoint does not page — the caller bounds the result by how many item ids it passes,
    /// which the tool caps at the standard page size.
    /// </remarks>
    public async Task<IReadOnlyList<StockLevel>> GetStockLevelsAsync(
        IReadOnlyList<string> stockItemIds,
        string? locationId,
        CancellationToken cancellationToken)
    {
        var request = new GetStockLevelBatchRequest
        {
            Request = new GetStockLevelBatchRequest.StockItemIdsPayload
            {
                StockItemIds = [.. stockItemIds]
            }
        };

        var response = await client
            .PostAsync<GetStockLevelBatchRequest, List<GetStockLevelBatchResponse>>(
                GetStockLevelBatchPath, request, cancellationToken)
            .ConfigureAwait(false);

        var levels = response
            .SelectMany(item => (item.StockItemLevels ?? [])
                .Select(level => Project(item.pkStockItemId, level)))
            // Linnworks has no location filter on this endpoint, so narrow it here.
            .Where(level => locationId is null
                || string.Equals(level.LocationId, locationId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        logger.LogDebug(
            "Retrieved {Count} stock level rows for {ItemCount} items",
            levels.Count, stockItemIds.Count);

        return levels;
    }

    /// <summary>
    /// Sets absolute stock levels via <c>POST /api/Stock/SetStockLevel</c>. Mutating.
    /// </summary>
    /// <remarks>
    /// <c>Level</c> is an absolute quantity, not a delta — Linnworks has separate delta endpoints.
    /// </remarks>
    public async Task<IReadOnlyList<StockLevel>> SetStockLevelsAsync(
        IReadOnlyList<(string Sku, string LocationId, int Level)> updates,
        string changeSource,
        CancellationToken cancellationToken)
    {
        var request = new SetStockLevelRequest
        {
            StockLevels = [.. updates.Select(u => new SetStockLevelRequest.StockLevelUpdate
            {
                Sku = u.Sku,
                LocationId = u.LocationId,
                Level = u.Level
            })],
            ChangeSource = changeSource
        };

        var response = await client
            .PostAsync<SetStockLevelRequest, List<StockItemLevelResponse>>(
                SetStockLevelPath, request, cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("Set stock level for {Count} SKU/location pairs", updates.Count);

        return response.Select(level => Project(level.StockItemId ?? string.Empty, level)).ToList();
    }

    private static StockLevel Project(string stockItemId, StockItemLevelResponse level) => new()
    {
        StockItemId = string.IsNullOrEmpty(level.StockItemId) ? stockItemId : level.StockItemId,
        Sku = level.SKU ?? string.Empty,
        LocationId = level.Location?.StockLocationId,
        LocationName = level.Location?.LocationName,
        Quantity = level.StockLevel,
        Available = level.Available,
        InOrderBook = level.InOrderBook,
        MinimumLevel = level.MinimumLevel,
        Due = level.Due,
        StockValue = level.StockValue,
        LastUpdateDate = level.LastUpdateDate,
        LastUpdateOperation = level.LastUpdateOperation
    };
}
