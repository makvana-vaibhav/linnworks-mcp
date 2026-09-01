using System.ComponentModel;
using LinnworksMcp.Application.Inventory;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Utils;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace LinnworksMcp.Mcp.Tools;

/// <summary>
/// Inventory tools. Each method validates its input, delegates to
/// <see cref="InventoryService"/>, and returns JSON.
/// </summary>
[McpServerToolType]
public sealed class InventoryTools(
    InventoryService inventoryService,
    IToolAuthorizer authorizer,
    ToolMetrics metrics,
    ILogger<InventoryTools> logger)
{
    [McpServerTool(Name = "get_inventory_items", ReadOnly = true, Idempotent = true)]
    [Description(
        "Search or browse Linnworks inventory items. Returns SKU, title, stock level, "
        + "available quantity and pricing for each item. Read-only. "
        + "Results are page-based: pass pageNumber (1-based) and pageSize (max 200).")]
    public Task<string> GetInventoryItemsAsync(
        // A nullable type alone does not make a parameter optional in the generated schema —
        // it needs an explicit default, or clients are required to supply it.
        [Description("Optional search term matched against SKU, title and barcode. Omit to browse all items.")]
        string? searchKeyword = null,
        [Description("Page number, 1-based. Defaults to 1.")]
        int pageNumber = 1,
        [Description("Items per page. Defaults to 50, maximum 200.")]
        int pageSize = ToolValidation.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_inventory_items", logger, metrics, async ct =>
        {
            var (page, size) = ToolValidation.Paging(pageNumber, pageSize);
            await authorizer.AuthorizeAsync("get_inventory_items", destructive: false, ct)
                .ConfigureAwait(false);

            return await inventoryService
                .GetInventoryItemsAsync(searchKeyword, page, size, ct)
                .ConfigureAwait(false);
        }, cancellationToken);

    [McpServerTool(Name = "get_inventory_item_by_id", ReadOnly = true, Idempotent = true)]
    [Description(
        "Get full details of one Linnworks inventory item by its StockItemId (a UUID). "
        + "Returns pricing, dimensions, tax rate, category and stock quantities. Read-only. "
        + "Use get_inventory_items first if you only know the SKU.")]
    public Task<string> GetInventoryItemByIdAsync(
        [Description("The Linnworks StockItemId (UUID) of the item to retrieve.")]
        string stockItemId,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_inventory_item_by_id", logger, metrics, async ct =>
        {
            var id = ToolValidation.RequiredGuid(nameof(stockItemId), stockItemId);
            await authorizer.AuthorizeAsync("get_inventory_item_by_id", destructive: false, ct)
                .ConfigureAwait(false);

            return await inventoryService.GetInventoryItemByIdAsync(id, ct).ConfigureAwait(false);
        }, cancellationToken);

    [McpServerTool(Name = "get_low_stock_items", ReadOnly = true, Idempotent = true)]
    [Description(
        "List inventory items at or below their configured minimum stock level, with the "
        + "shortage for each. Read-only. This Linnworks report is capped by row count and "
        + "cannot be paged — raise pageSize (max 200) rather than requesting a later page.")]
    public Task<string> GetLowStockItemsAsync(
        [Description("Optional warehouse location UUID. Omit to report across all locations combined.")]
        string? locationId = null,
        [Description("Maximum rows to return. Defaults to 50, maximum 200.")]
        int pageSize = ToolValidation.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_low_stock_items", logger, metrics, async ct =>
        {
            var (_, size) = ToolValidation.Paging(1, pageSize);
            var location = ToolValidation.OptionalGuid(nameof(locationId), locationId);
            await authorizer.AuthorizeAsync("get_low_stock_items", destructive: false, ct)
                .ConfigureAwait(false);

            return await inventoryService
                .GetLowStockItemsAsync(location, size, ct)
                .ConfigureAwait(false);
        }, cancellationToken);

    // TODO: create_inventory_item      -> POST /api/Inventory/AddInventoryItem
    // TODO: update_inventory_item      -> POST /api/Inventory/UpdateInventoryItem
    // TODO: delete_inventory_item      -> POST /api/Inventory/DeleteInventoryItems
    // TODO: get_inventory_item_prices  -> POST /api/Inventory/GetInventoryItemPrices
    // TODO: update_inventory_item_prices -> POST /api/Inventory/UpdateInventoryItemPrices
    // Verify each request/response schema at https://apidocs.linnworks.net/reference/<slug>.md
    // before implementing — namespaces and verbs vary per endpoint.
}
