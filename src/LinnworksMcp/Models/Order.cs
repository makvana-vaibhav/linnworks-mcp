using System.Text.Json.Serialization;

namespace LinnworksMcp.Models;

/// <summary>Compact projection of an order, used by both open and detail order tools.</summary>
public sealed class Order
{
    public required string OrderId { get; init; }

    /// <summary>Human-facing order number shown in the Linnworks UI.</summary>
    public int NumOrderId { get; init; }

    public bool Processed { get; init; }

    public string? Source { get; init; }

    public string? SubSource { get; init; }

    public string? Status { get; init; }

    public DateTimeOffset? ReceivedDate { get; init; }

    public DateTimeOffset? ProcessedDateTime { get; init; }

    public DateTimeOffset? DespatchByDate { get; init; }

    public decimal TotalCharge { get; init; }

    public string? Currency { get; init; }

    public string? CustomerName { get; init; }

    public string? PostalServiceName { get; init; }

    public string? TrackingNumber { get; init; }

    public string? FulfilmentLocationId { get; init; }

    public IReadOnlyList<OrderItem>? Items { get; init; }
}

public sealed class OrderItem
{
    public required string Sku { get; init; }

    public string? Title { get; init; }

    public int Quantity { get; init; }

    public decimal PricePerUnit { get; init; }

    public decimal Cost { get; init; }
}

// ── Wire contracts ───────────────────────────────────────────────────────────

/// <summary>Request for <c>POST /api/OpenOrders/GetOpenOrders</c>.</summary>
internal sealed class GetOpenOrdersRequest
{
    /// <summary>Linnworks saved-view id. 0 selects the account's default view.</summary>
    [JsonPropertyName("ViewId")]
    public required int ViewId { get; init; }

    [JsonPropertyName("LocationId")]
    public required string LocationId { get; init; }

    [JsonPropertyName("EntriesPerPage")]
    public required int EntriesPerPage { get; init; }

    [JsonPropertyName("PageNumber")]
    public required int PageNumber { get; init; }

    [JsonPropertyName("OrderIds")]
    public string[]? OrderIds { get; init; }
}

/// <summary>Linnworks' own paged envelope, normalised into <see cref="PagedResult{T}"/>.</summary>
internal sealed class PostFilterPagedResponse<T>
{
    public int PageNumber { get; init; }

    public int EntriesPerPage { get; init; }

    public int TotalEntries { get; init; }

    public int TotalPages { get; init; }

    public List<T>? Data { get; init; }
}

/// <summary>Request for <c>POST /api/Orders/GetOrdersById</c>.</summary>
internal sealed class GetOrdersByIdRequest
{
    [JsonPropertyName("pkOrderIds")]
    public required string[] PkOrderIds { get; init; }
}

/// <summary>Shared order shape returned by the OpenOrders and Orders endpoints.</summary>
internal sealed class OrderDetailsResponse
{
    public string OrderId { get; init; } = string.Empty;

    public int NumOrderId { get; init; }

    public bool Processed { get; init; }

    public DateTimeOffset? ProcessedDateTime { get; init; }

    public string? FulfilmentLocationId { get; init; }

    public OrderGeneralInfoResponse? GeneralInfo { get; init; }

    public OrderShippingInfoResponse? ShippingInfo { get; init; }

    public OrderCustomerInfoResponse? CustomerInfo { get; init; }

    public OrderTotalsInfoResponse? TotalsInfo { get; init; }

    public List<OrderItemResponse>? Items { get; init; }
}

internal sealed class OrderGeneralInfoResponse
{
    public string? Source { get; init; }

    public string? SubSource { get; init; }

    public string? Status { get; init; }

    public DateTimeOffset? ReceivedDate { get; init; }

    public DateTimeOffset? DespatchByDate { get; init; }
}

internal sealed class OrderShippingInfoResponse
{
    public string? PostalServiceName { get; init; }

    public string? TrackingNumber { get; init; }
}

internal sealed class OrderCustomerInfoResponse
{
    public OrderAddressResponse? Address { get; init; }
}

internal sealed class OrderAddressResponse
{
    public string? FullName { get; init; }

    public string? Town { get; init; }

    public string? Country { get; init; }
}

internal sealed class OrderTotalsInfoResponse
{
    public decimal TotalCharge { get; init; }

    public string? Currency { get; init; }
}

internal sealed class OrderItemResponse
{
    public string? SKU { get; init; }

    public string? Title { get; init; }

    public int Quantity { get; init; }

    public decimal PricePerUnit { get; init; }

    public decimal Cost { get; init; }
}
