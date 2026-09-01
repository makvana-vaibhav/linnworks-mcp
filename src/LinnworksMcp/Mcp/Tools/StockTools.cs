using System.ComponentModel;
using LinnworksMcp.Application.Stock;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Utils;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace LinnworksMcp.Mcp.Tools;

/// <summary>Stock level tools.</summary>
[McpServerToolType]
public sealed class StockTools(
    StockService stockService,
    IToolAuthorizer authorizer,
    ToolMetrics metrics,
    ILogger<StockTools> logger)
{
    [McpServerTool(Name = "get_stock_levels", ReadOnly = true, Idempotent = true)]
    [Description(
        "Get per-location stock levels for specific inventory items. Returns quantity, "
        + "available quantity, quantity in open orders and minimum level at each location. "
        + "Read-only. This Linnworks endpoint does not page — bound the result by passing "
        + "fewer item ids (maximum 200 per call).")]
    public Task<string> GetStockLevelsAsync(
        [Description("StockItemId UUIDs to look up, comma-separated. Maximum 200.")]
        string stockItemIds,
        [Description("Optional warehouse location UUID to filter to a single location.")]
        string? locationId = null,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_stock_levels", logger, metrics, async ct =>
        {
            var ids = ParseIds(stockItemIds, nameof(stockItemIds));
            var location = ToolValidation.OptionalGuid(nameof(locationId), locationId);

            await authorizer.AuthorizeAsync("get_stock_levels", destructive: false, ct)
                .ConfigureAwait(false);

            return await stockService.GetStockLevelsAsync(ids, location, ct).ConfigureAwait(false);
        }, cancellationToken);

    [McpServerTool(Name = "update_stock_levels", Destructive = true, Idempotent = true)]
    [Description(
        "MUTATES DATA. Sets the stock level of a SKU at a warehouse location in Linnworks to an "
        + "absolute quantity — this overwrites the current level rather than adjusting it. "
        + "Confirm the SKU, location and quantity with the user before calling. "
        + "Use get_locations to find the location UUID.")]
    public Task<string> UpdateStockLevelsAsync(
        [Description("The SKU (item number) whose stock level should be set.")]
        string sku,
        [Description("Warehouse location UUID. Use get_locations to look this up.")]
        string locationId,
        [Description("The absolute quantity to set. This replaces the existing level; it is not a delta.")]
        int quantity,
        [Description("Optional audit label recorded against the change in Linnworks.")]
        string? changeSource = null,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("update_stock_levels", logger, metrics, async ct =>
        {
            var validSku = ToolValidation.RequiredText(nameof(sku), sku);
            var location = ToolValidation.RequiredGuid(nameof(locationId), locationId);

            if (quantity < 0)
            {
                throw ToolValidation.Invalid(nameof(quantity), $"must be zero or greater (received {quantity}).");
            }

            await authorizer.AuthorizeAsync("update_stock_levels", destructive: true, ct)
                .ConfigureAwait(false);

            return await stockService
                .SetStockLevelsAsync(
                    [(validSku, location, quantity)],
                    string.IsNullOrWhiteSpace(changeSource) ? "LinnworksMcp" : changeSource,
                    ct)
                .ConfigureAwait(false);
        }, cancellationToken);

    /// <summary>
    /// Parses and validates a comma-separated UUID list, enforcing the same ceiling that bounds
    /// every other list-shaped response.
    /// </summary>
    private static List<string> ParseIds(string raw, string parameterName)
    {
        var ids = (raw ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (ids.Count == 0)
        {
            throw ToolValidation.Invalid(parameterName, "must contain at least one UUID.");
        }

        if (ids.Count > ToolValidation.MaxPageSize)
        {
            throw ToolValidation.Invalid(
                parameterName,
                $"must not contain more than {ToolValidation.MaxPageSize} ids (received {ids.Count}).");
        }

        return [.. ids.Select(id => ToolValidation.RequiredGuid(parameterName, id))];
    }

    // TODO: get_stock_level_history -> POST /api/Stock/GetItemChangesHistory
    // TODO: set_stock_item_batch    -> POST /api/Stock/BatchStockLevelDelta (relative adjustments)
    // Verify both schemas at https://apidocs.linnworks.net/reference/<slug>.md first.
}
