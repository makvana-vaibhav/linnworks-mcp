using System.Text.Json.Serialization;
using LinnworksMcp.Infrastructure.Linnworks;

namespace LinnworksMcp.Application.Shipping;

/// <summary>
/// Shipping services and postal service operations.
/// </summary>
public sealed class ShippingService(ILinnworksClient client)
{
    internal const string GetPostalServicesPath = "/api/PostalServices/GetPostalServices";
    internal const string GetOrderShippingInfoPath = "/api/Orders/GetOrderShippingInfo";

    public async Task<List<ShippingServiceDto>> GetShippingServicesAsync(CancellationToken cancellationToken)
    {
        var response = await client
            .GetAsync<List<PostalServiceResponse>>(GetPostalServicesPath, query: null, cancellationToken)
            .ConfigureAwait(false);

        return response?.Select(p => new ShippingServiceDto(
            PostalServiceId: p.PostalServiceId ?? string.Empty,
            PostalServiceName: p.PostalServiceName ?? string.Empty,
            Vendor: p.Vendor ?? string.Empty,
            ServiceCode: p.ServiceCode ?? string.Empty,
            HasTracking: p.HasTracking)).ToList() ?? [];
    }

    public async Task<TrackingInfoDto?> GetTrackingInfoAsync(string orderId, CancellationToken cancellationToken)
    {
        var request = new GetOrderShippingInfoRequest(OrderId: orderId);
        var response = await client
            .PostAsync<GetOrderShippingInfoRequest, OrderShippingInfoResponse>(GetOrderShippingInfoPath, request, cancellationToken)
            .ConfigureAwait(false);

        return response is not null ? new TrackingInfoDto(
            OrderId: orderId,
            TrackingNumber: response.TrackingNumber ?? string.Empty,
            Vendor: response.Vendor ?? string.Empty,
            PostalServiceName: response.PostalServiceName ?? string.Empty,
            ShippedDate: response.ShippedDate) : null;
    }
}

public sealed record GetOrderShippingInfoRequest(
    [property: JsonPropertyName("OrderId")] string OrderId);

public sealed record PostalServiceResponse(
    [property: JsonPropertyName("PostalServiceId")] string? PostalServiceId,
    [property: JsonPropertyName("PostalServiceName")] string? PostalServiceName,
    [property: JsonPropertyName("Vendor")] string? Vendor,
    [property: JsonPropertyName("ServiceCode")] string? ServiceCode,
    [property: JsonPropertyName("HasTracking")] bool HasTracking);

public sealed record OrderShippingInfoResponse(
    [property: JsonPropertyName("TrackingNumber")] string? TrackingNumber,
    [property: JsonPropertyName("Vendor")] string? Vendor,
    [property: JsonPropertyName("PostalServiceName")] string? PostalServiceName,
    [property: JsonPropertyName("ShippedDate")] DateTimeOffset? ShippedDate);

public sealed record ShippingServiceDto(
    string PostalServiceId,
    string PostalServiceName,
    string Vendor,
    string ServiceCode,
    bool HasTracking);

public sealed record TrackingInfoDto(
    string OrderId,
    string TrackingNumber,
    string Vendor,
    string PostalServiceName,
    DateTimeOffset? ShippedDate);
