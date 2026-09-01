using System.Text.Json.Serialization;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Models;

namespace LinnworksMcp.Application.PurchaseOrders;

/// <summary>
/// Purchase order operations.
/// </summary>
public sealed class PurchaseOrderService(ILinnworksClient client)
{
    internal const string GetPurchaseOrdersPath = "/api/PurchaseOrder/GetPurchaseOrders";
    internal const string CreatePurchaseOrderPath = "/api/PurchaseOrder/CreatePurchaseOrder";

    public async Task<PagedResult<PurchaseOrderDto>> GetPurchaseOrdersAsync(
        string? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var request = new GetPurchaseOrdersRequest(
            Status: status,
            PageNumber: pageNumber,
            EntriesPerPage: pageSize);

        var response = await client
            .PostAsync<GetPurchaseOrdersRequest, GetPurchaseOrdersResponse>(GetPurchaseOrdersPath, request, cancellationToken)
            .ConfigureAwait(false);

        var items = response.PurchaseOrders?.Select(Project).ToList() ?? [];
        return PagedResult<PurchaseOrderDto>.Create(items, pageNumber, pageSize, totalCount: response.TotalCount);
    }

    public async Task<PurchaseOrderDto> CreatePurchaseOrderAsync(
        string supplierId,
        string locationId,
        string externalRef,
        CancellationToken cancellationToken)
    {
        var request = new CreatePurchaseOrderRequest(
            SupplierId: supplierId,
            LocationId: locationId,
            ExternalRef: externalRef);

        var response = await client
            .PostAsync<CreatePurchaseOrderRequest, PurchaseOrderResponse>(CreatePurchaseOrderPath, request, cancellationToken)
            .ConfigureAwait(false);

        return Project(response);
    }

    private static PurchaseOrderDto Project(PurchaseOrderResponse p) => new(
        PurchaseOrderId: p.PurchaseOrderId ?? string.Empty,
        SupplierId: p.SupplierId ?? string.Empty,
        LocationId: p.LocationId ?? string.Empty,
        ExternalRef: p.ExternalRef ?? string.Empty,
        Status: p.Status ?? "OPEN",
        DateCreated: p.DateCreated);
}

public sealed record GetPurchaseOrdersRequest(
    [property: JsonPropertyName("Status")] string? Status,
    [property: JsonPropertyName("PageNumber")] int PageNumber,
    [property: JsonPropertyName("EntriesPerPage")] int EntriesPerPage);

public sealed record GetPurchaseOrdersResponse(
    [property: JsonPropertyName("PurchaseOrders")] List<PurchaseOrderResponse>? PurchaseOrders,
    [property: JsonPropertyName("TotalCount")] int TotalCount);

public sealed record CreatePurchaseOrderRequest(
    [property: JsonPropertyName("SupplierId")] string SupplierId,
    [property: JsonPropertyName("LocationId")] string LocationId,
    [property: JsonPropertyName("ExternalRef")] string ExternalRef);

public sealed record PurchaseOrderResponse(
    [property: JsonPropertyName("PurchaseOrderId")] string? PurchaseOrderId,
    [property: JsonPropertyName("SupplierId")] string? SupplierId,
    [property: JsonPropertyName("LocationId")] string? LocationId,
    [property: JsonPropertyName("ExternalRef")] string? ExternalRef,
    [property: JsonPropertyName("Status")] string? Status,
    [property: JsonPropertyName("DateCreated")] DateTimeOffset DateCreated);

public sealed record PurchaseOrderDto(
    string PurchaseOrderId,
    string SupplierId,
    string LocationId,
    string ExternalRef,
    string Status,
    DateTimeOffset DateCreated);
