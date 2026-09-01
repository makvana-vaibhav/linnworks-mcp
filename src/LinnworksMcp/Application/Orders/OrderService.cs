using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Models;
using Microsoft.Extensions.Logging;

namespace LinnworksMcp.Application.Orders;

/// <summary>Order reads.</summary>
public sealed class OrderService(ILinnworksClient client, ILogger<OrderService> logger)
{
    internal const string GetOpenOrdersPath = "/api/Orders/GetOpenOrders";
    internal const string GetOrdersByIdPath = "/api/Orders/GetOrdersById";

    /// <summary>
    /// Page-based open-order listing via <c>POST /api/Orders/GetOpenOrders</c>.
    /// </summary>
    /// <param name="locationId">
    /// Fulfilment centre to filter to, or <see langword="null"/> for every location.
    /// </param>
    /// <remarks>
    /// Uses the Orders namespace rather than <c>OpenOrders/GetOpenOrders</c>, which requires a
    /// <c>ViewId</c> matching a saved view that actually exists in the account and 400s otherwise.
    /// This variant has no required fields, so it works on any account without setup.
    /// The response carries a real <c>TotalEntries</c>, so paging metadata is exact rather than
    /// inferred — which also makes "how many open orders are there?" answerable with pageSize 1.
    /// </remarks>
    public async Task<PagedResult<Order>> GetOpenOrdersAsync(
        string? locationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var request = new GetOpenOrdersRequest
        {
            EntriesPerPage = pageSize,
            PageNumber = pageNumber,
            FulfilmentCenter = locationId
        };

        var response = await client
            .PostAsync<GetOpenOrdersRequest, PostFilterPagedResponse<OrderDetailsResponse>>(
                GetOpenOrdersPath, request, cancellationToken)
            .ConfigureAwait(false);

        var orders = (response.Data ?? []).Select(Project).ToList();

        logger.LogDebug(
            "Retrieved {Count} open orders (page {PageNumber} of {TotalPages}, {TotalEntries} total)",
            orders.Count, response.PageNumber, response.TotalPages, response.TotalEntries);

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
        string? locationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var page = await GetOpenOrdersAsync(locationId, pageNumber, pageSize, cancellationToken)
            .ConfigureAwait(false);

        // The endpoint returns only open orders, so Processed is false throughout. The filter is
        // kept as a guard in case a future Linnworks change starts including processed rows.
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
        // Open orders carry the location on GeneralInfo; processed orders on the root.
        FulfilmentLocationId = o.FulfilmentLocationId ?? o.GeneralInfo?.Location,
        Source = o.GeneralInfo?.Source,
        SubSource = o.GeneralInfo?.SubSource,
        StatusCode = o.GeneralInfo?.Status,
        Status = OrderStatusNames.Resolve(o.GeneralInfo?.Status),
        NumItems = o.GeneralInfo?.NumItems ?? 0,
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
