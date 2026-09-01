using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Models;
using Microsoft.Extensions.Logging;

namespace LinnworksMcp.Application.Orders;

/// <summary>Order reads.</summary>
public sealed class OrderService(ILinnworksClient client, ILogger<OrderService> logger)
{
    internal const string GetOpenOrdersPath = "/api/OpenOrders/GetOpenOrders";
    internal const string GetOrdersByIdPath = "/api/Orders/GetOrdersById";

    /// <summary>
    /// Page-based open-order listing via <c>POST /api/OpenOrders/GetOpenOrders</c>.
    /// </summary>
    /// <remarks>
    /// Note the namespace: this is <c>OpenOrders</c>, not <c>Orders</c>. Linnworks splits open
    /// and processed orders across different namespaces. Unlike the inventory endpoints, this
    /// one does return a real total, so paging metadata is exact rather than inferred.
    /// </remarks>
    public async Task<PagedResult<Order>> GetOpenOrdersAsync(
        string locationId,
        int pageNumber,
        int pageSize,
        int viewId,
        CancellationToken cancellationToken)
    {
        var request = new GetOpenOrdersRequest
        {
            ViewId = viewId,
            LocationId = locationId,
            EntriesPerPage = pageSize,
            PageNumber = pageNumber
        };

        var response = await client
            .PostAsync<GetOpenOrdersRequest, PostFilterPagedResponse<OrderDetailsResponse>>(
                GetOpenOrdersPath, request, cancellationToken)
            .ConfigureAwait(false);

        var orders = (response.Data ?? []).Select(Project).ToList();

        logger.LogDebug(
            "Retrieved {Count} open orders (page {PageNumber} of {TotalPages})",
            orders.Count, response.PageNumber, response.TotalPages);

        return PagedResult<Order>.Create(
            orders, pageNumber, pageSize, totalCount: response.TotalEntries);
    }

    /// <summary>
    /// Full detail for specific orders via <c>POST /api/Orders/GetOrdersById</c>.
    /// </summary>
    public async Task<IReadOnlyList<Order>> GetOrdersByIdAsync(
        IReadOnlyList<string> orderIds,
        CancellationToken cancellationToken)
    {
        var request = new GetOrdersByIdRequest { PkOrderIds = [.. orderIds] };

        var response = await client
            .PostAsync<GetOrdersByIdRequest, List<OrderDetailsResponse>>(
                GetOrdersByIdPath, request, cancellationToken)
            .ConfigureAwait(false);

        return response.Select(Project).ToList();
    }

    /// <summary>
    /// Open orders that are not yet dispatched, enriched with full item and shipping detail.
    /// </summary>
    /// <remarks>
    /// Combines <c>GetOpenOrders</c> with <c>GetOrdersById</c> because "what still needs
    /// shipping, and what is in each one" is a single question a user actually asks — the list
    /// call alone often omits item-level detail. Cancellation is checked between the two calls
    /// so an aborted request does not pay for the second one.
    /// </remarks>
    public async Task<PagedResult<Order>> GetUnfulfilledOrdersAsync(
        string locationId,
        int pageNumber,
        int pageSize,
        int viewId,
        CancellationToken cancellationToken)
    {
        var page = await GetOpenOrdersAsync(locationId, pageNumber, pageSize, viewId, cancellationToken)
            .ConfigureAwait(false);

        var unprocessed = page.Items.Where(o => !o.Processed).ToList();
        if (unprocessed.Count == 0)
        {
            return PagedResult<Order>.Create(unprocessed, pageNumber, pageSize, page.TotalCount);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var detailed = await GetOrdersByIdAsync(
            [.. unprocessed.Select(o => o.OrderId)], cancellationToken).ConfigureAwait(false);

        // Fall back to the list projection for anything the detail call did not return.
        var byId = detailed.ToDictionary(o => o.OrderId, StringComparer.OrdinalIgnoreCase);
        var merged = unprocessed
            .Select(o => byId.TryGetValue(o.OrderId, out var full) ? full : o)
            .ToList();

        return PagedResult<Order>.Create(merged, pageNumber, pageSize, page.TotalCount);
    }

    private static Order Project(OrderDetailsResponse o) => new()
    {
        OrderId = o.OrderId,
        NumOrderId = o.NumOrderId,
        Processed = o.Processed,
        ProcessedDateTime = o.ProcessedDateTime,
        FulfilmentLocationId = o.FulfilmentLocationId,
        Source = o.GeneralInfo?.Source,
        SubSource = o.GeneralInfo?.SubSource,
        Status = o.GeneralInfo?.Status,
        ReceivedDate = o.GeneralInfo?.ReceivedDate,
        DespatchByDate = o.GeneralInfo?.DespatchByDate,
        PostalServiceName = o.ShippingInfo?.PostalServiceName,
        TrackingNumber = o.ShippingInfo?.TrackingNumber,
        CustomerName = o.CustomerInfo?.Address?.FullName,
        TotalCharge = o.TotalsInfo?.TotalCharge ?? 0m,
        Currency = o.TotalsInfo?.Currency,
        Items = o.Items?.Select(i => new OrderItem
        {
            Sku = i.SKU ?? string.Empty,
            Title = i.Title,
            Quantity = i.Quantity,
            PricePerUnit = i.PricePerUnit,
            Cost = i.Cost
        }).ToList()
    };
}
