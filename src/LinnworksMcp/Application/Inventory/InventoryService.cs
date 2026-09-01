using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Models;
using Microsoft.Extensions.Logging;

namespace LinnworksMcp.Application.Inventory;

/// <summary>
/// Inventory operations. All Linnworks HTTP traffic goes through <see cref="ILinnworksClient"/>;
/// tools never call the API directly.
/// </summary>
public sealed class InventoryService(ILinnworksClient client, ILogger<InventoryService> logger)
{
    internal const string GetStockItemsFullPath = "/api/Stock/GetStockItemsFull";
    internal const string GetInventoryItemByIdPath = "/api/Inventory/GetInventoryItemById";
    internal const string GetLowStockLevelPath = "/api/Dashboards/GetLowStockLevel";

    /// <summary>
    /// Page-based listing via <c>POST /api/Stock/GetStockItemsFull</c>.
    /// </summary>
    /// <remarks>
    /// This endpoint returns a bare array with no total count, so <see cref="PagedResult{T}.HasMore"/>
    /// is inferred from whether the page came back full.
    /// </remarks>
    public async Task<PagedResult<InventoryItem>> GetInventoryItemsAsync(
        string? searchKeyword,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var request = new GetStockItemsFullRequest
        {
            Keyword = searchKeyword,
            SearchTypes = ["SKU", "Title", "Barcode"],
            PageNumber = pageNumber,
            EntriesPerPage = pageSize,
            // Only what the projection needs — every extra requirement inflates the response.
            DataRequirements = ["StockLevels"],
            LoadCompositeParents = false,
            LoadVariationParents = false
        };

        var response = await client
            .PostAsync<GetStockItemsFullRequest, List<StockItemFullResponse>>(
                GetStockItemsFullPath, request, cancellationToken)
            .ConfigureAwait(false);

        logger.LogDebug(
            "Retrieved {Count} inventory items for page {PageNumber}", response.Count, pageNumber);

        var items = response.Select(Project).ToList();

        return PagedResult<InventoryItem>.Create(items, pageNumber, pageSize);
    }

    /// <summary>Single item detail via <c>GET /api/Inventory/GetInventoryItemById</c>.</summary>
    public async Task<InventoryItemDetail> GetInventoryItemByIdAsync(
        string stockItemId,
        CancellationToken cancellationToken)
    {
        // Documented as a GET with a query parameter, despite most Linnworks calls being POSTs.
        var response = await client
            .GetAsync<StockItemInvResponse>(
                GetInventoryItemByIdPath,
                new Dictionary<string, string?> { ["id"] = stockItemId },
                cancellationToken)
            .ConfigureAwait(false);

        return new InventoryItemDetail
        {
            StockItemId = response.StockItemId,
            Sku = response.ItemNumber,
            Title = response.ItemTitle,
            Barcode = response.BarcodeNumber,
            CategoryName = response.CategoryName,
            PackageGroupName = response.PackageGroupName,
            PostalServiceName = response.PostalServiceName,
            Quantity = response.Quantity,
            Available = response.Available,
            InOrder = response.InOrder,
            Due = response.Due,
            MinimumLevel = response.MinimumLevel,
            RetailPrice = response.RetailPrice,
            PurchasePrice = response.PurchasePrice,
            TaxRate = response.TaxRate,
            Weight = response.Weight,
            Height = response.Height,
            Width = response.Width,
            Depth = response.Depth,
            CreationDate = response.CreationDate
        };
    }

    /// <summary>
    /// Low-stock report via <c>GET /api/Dashboards/GetLowStockLevel</c>.
    /// </summary>
    /// <remarks>
    /// This endpoint takes a row cap (<c>numRows</c>) and cannot page, so the result is a single
    /// capped page. The returned envelope says so via <see cref="PagedResult{T}.PagingNote"/>.
    /// </remarks>
    public async Task<PagedResult<LowStockItem>> GetLowStockItemsAsync(
        string? locationId,
        int maxRows,
        CancellationToken cancellationToken)
    {
        var response = await client
            .GetAsync<List<LowStockLevelResponse>>(
                GetLowStockLevelPath,
                new Dictionary<string, string?>
                {
                    // Null means "combined across all locations".
                    ["locationId"] = locationId,
                    ["numRows"] = maxRows.ToString()
                },
                cancellationToken)
            .ConfigureAwait(false);

        var items = response
            .Select(r => new LowStockItem
            {
                Sku = r.ItemNumber,
                Title = r.ItemTitle,
                Quantity = r.Quantity,
                MinimumLevel = r.MinimumLevel,
                InOrderBook = r.InBooks,
                LocationName = r.Location
            })
            .ToList();

        return PagedResult<LowStockItem>.Create(
            items,
            pageNumber: 1,
            pageSize: maxRows,
            totalCount: items.Count,
            pagingNote: "This Linnworks report is capped by row count and cannot be paged. "
                      + "Increase pageSize (up to 200) to see more.");
    }

    private static InventoryItem Project(StockItemFullResponse item)
    {
        // GetStockItemsFull reports levels per location; roll them up for the list view.
        var levels = item.StockLevels;

        return new InventoryItem
        {
            StockItemId = item.StockItemId,
            Sku = item.ItemNumber,
            Title = item.ItemTitle,
            Barcode = item.BarcodeNumber,
            CategoryName = item.CategoryName,
            StockLevel = levels?.Sum(l => l.StockLevel) ?? 0,
            Available = levels?.Sum(l => l.Available) ?? 0,
            InOrderBook = levels?.Sum(l => l.InOrderBook) ?? 0,
            Due = levels?.Sum(l => l.Due) ?? 0,
            // Minimum level is defined per location; surface the highest so a rollup never
            // looks safe when one location is under its own threshold.
            MinimumLevel = levels is { Count: > 0 } ? levels.Max(l => l.MinimumLevel) : 0,
            RetailPrice = item.RetailPrice,
            PurchasePrice = item.PurchasePrice,
            Weight = item.Weight
        };
    }
}
