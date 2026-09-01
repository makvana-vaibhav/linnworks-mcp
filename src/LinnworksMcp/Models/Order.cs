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

    /// <summary>Raw Linnworks order status code.</summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Human-readable form of <see cref="StatusCode"/>, or null when Linnworks returns a code
    /// this server does not recognise.
    /// </summary>
    public string? Status { get; init; }

    public int NumItems { get; init; }

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

/// <summary>
/// Request for <c>POST /api/Orders/GetOpenOrders</c>.
/// </summary>
/// <remarks>
/// This is the Orders-namespace variant, not <c>OpenOrders/GetOpenOrders</c>. The latter
/// requires a <c>ViewId</c> naming a real saved view in the account and rejects the request with
/// 400 when given one that does not exist — there is no "0 means default" fallback. This variant
/// declares no required fields at all, so it works without any account-specific setup, and it
/// carries a higher rate limit (250/min vs 150/min). Field names here are camelCase; the
/// OpenOrders variant used PascalCase.
/// </remarks>
internal sealed class GetOpenOrdersRequest
{
    [JsonPropertyName("entriesPerPage")]
    public required int EntriesPerPage { get; init; }

    [JsonPropertyName("pageNumber")]
    public required int PageNumber { get; init; }

    /// <summary>
    /// Location to get orders for. Null is sent as an omitted field and means every location —
    /// passing an all-zero UUID instead is rejected as an unknown location.
    /// </summary>
    [JsonPropertyName("fulfilmentCenter")]
    public string? FulfilmentCenter { get; init; }

    /// <summary>Optional extra filter expression, passed through untouched when supplied.</summary>
    [JsonPropertyName("additionalFilter")]
    public string? AdditionalFilter { get; init; }
}

/// <summary>
/// Linnworks' own paged envelope, normalised into <see cref="PagedResult{T}"/>. Both
/// <c>GenericPagedResult_OpenOrder</c> and <c>PostFilterPagedResponse_OpenOrder</c> use
/// these field names, so one type covers both.
/// </summary>
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

    /// <summary>
    /// Order status as an integer, per the documented enum
    /// (0 = UNPAID, 1 = PAID, 2 = RETURN, 3 = PENDING, 4 = RESEND).
    /// Typing this as a string makes every real response fail to deserialize.
    /// </summary>
    public int? Status { get; init; }

    /// <summary>Fulfilment location. On open orders this is the only place the location appears.</summary>
    public string? Location { get; init; }

    public int NumItems { get; init; }

    public DateTimeOffset? ReceivedDate { get; init; }

    public DateTimeOffset? DespatchByDate { get; init; }
}

/// <summary>Documented Linnworks order-status codes.</summary>
internal static class OrderStatusNames
{
    public static string? Resolve(int? status) => status switch
    {
        0 => "UNPAID",
        1 => "PAID",
        2 => "RETURN",
        3 => "PENDING",
        4 => "RESEND",
        // An unrecognised code is left unnamed rather than guessed at; StatusCode still carries it.
        _ => null
    };
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
