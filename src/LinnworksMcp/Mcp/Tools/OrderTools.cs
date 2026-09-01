using System.ComponentModel;
using LinnworksMcp.Application.Orders;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Utils;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace LinnworksMcp.Mcp.Tools;

/// <summary>Order tools.</summary>
[McpServerToolType]
public sealed class OrderTools(
    OrderService orderService,
    IToolAuthorizer authorizer,
    ToolMetrics metrics,
    ILogger<OrderTools> logger)
{
    [McpServerTool(Name = "get_open_orders", ReadOnly = true, Idempotent = true)]
    [Description(
        "List open (not yet processed) Linnworks orders for a warehouse location. Returns order "
        + "number, channel, status, customer, total and shipping service. Read-only. "
        + "Results are page-based: pass pageNumber (1-based) and pageSize (max 200). "
        + "Use get_locations to find the location UUID.")]
    public Task<string> GetOpenOrdersAsync(
        [Description("Warehouse location UUID to list orders for. Use get_locations to look this up.")]
        string locationId,
        [Description("Page number, 1-based. Defaults to 1.")]
        int pageNumber = 1,
        [Description("Orders per page. Defaults to 50, maximum 200.")]
        int pageSize = ToolValidation.DefaultPageSize,
        [Description("Optional Linnworks saved-view id. Defaults to 0, the account's default view.")]
        int viewId = 0,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_open_orders", logger, metrics, async ct =>
        {
            var (page, size) = ToolValidation.Paging(pageNumber, pageSize);
            var location = ToolValidation.RequiredGuid(nameof(locationId), locationId);

            await authorizer.AuthorizeAsync("get_open_orders", destructive: false, ct)
                .ConfigureAwait(false);

            return await orderService
                .GetOpenOrdersAsync(location, page, size, viewId, ct)
                .ConfigureAwait(false);
        }, cancellationToken);

    [McpServerTool(Name = "get_order_by_id", ReadOnly = true, Idempotent = true)]
    [Description(
        "Get full details of one or more Linnworks orders by their order UUIDs, including line "
        + "items, customer, totals and shipping. Read-only. Accepts up to 200 ids per call.")]
    public Task<string> GetOrderByIdAsync(
        [Description("Order UUIDs (pkOrderId), comma-separated. Maximum 200.")]
        string orderIds,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_order_by_id", logger, metrics, async ct =>
        {
            var ids = (orderIds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (ids.Count == 0)
            {
                throw ToolValidation.Invalid(nameof(orderIds), "must contain at least one UUID.");
            }

            if (ids.Count > ToolValidation.MaxPageSize)
            {
                throw ToolValidation.Invalid(
                    nameof(orderIds),
                    $"must not contain more than {ToolValidation.MaxPageSize} ids (received {ids.Count}).");
            }

            var validated = ids.Select(id => ToolValidation.RequiredGuid(nameof(orderIds), id)).ToList();

            await authorizer.AuthorizeAsync("get_order_by_id", destructive: false, ct)
                .ConfigureAwait(false);

            return await orderService.GetOrdersByIdAsync(validated, ct).ConfigureAwait(false);
        }, cancellationToken);

    [McpServerTool(Name = "get_unfulfilled_orders", ReadOnly = true, Idempotent = true)]
    [Description(
        "List open orders that still need to be shipped, enriched with their line items and "
        + "shipping details. Read-only. Answers 'what still needs fulfilling' in one call by "
        + "combining the open-order list with full order detail. Page-based: pass pageNumber "
        + "(1-based) and pageSize (max 200).")]
    public Task<string> GetUnfulfilledOrdersAsync(
        [Description("Warehouse location UUID. Use get_locations to look this up.")]
        string locationId,
        [Description("Page number, 1-based. Defaults to 1.")]
        int pageNumber = 1,
        [Description("Orders per page. Defaults to 50, maximum 200.")]
        int pageSize = ToolValidation.DefaultPageSize,
        [Description("Optional Linnworks saved-view id. Defaults to 0, the account's default view.")]
        int viewId = 0,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_unfulfilled_orders", logger, metrics, async ct =>
        {
            var (page, size) = ToolValidation.Paging(pageNumber, pageSize);
            var location = ToolValidation.RequiredGuid(nameof(locationId), locationId);

            await authorizer.AuthorizeAsync("get_unfulfilled_orders", destructive: false, ct)
                .ConfigureAwait(false);

            return await orderService
                .GetUnfulfilledOrdersAsync(location, page, size, viewId, ct)
                .ConfigureAwait(false);
        }, cancellationToken);

    // TODO: get_processed_orders     -> POST /api/ProcessedOrders/SearchProcessedOrders
    // TODO: search_orders            -> POST /api/ProcessedOrders/SearchProcessedOrdersPaged
    // TODO: add_order_note           -> POST /api/Orders/AddOrderNote  (destructive)
    // TODO: get_order_shipping_info  -> POST /api/Orders/GetOrderShippingInfo
    // Verify each schema at https://apidocs.linnworks.net/reference/<slug>.md before implementing.
}
