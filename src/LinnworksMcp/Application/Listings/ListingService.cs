using System.Text.Json.Serialization;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Models;

namespace LinnworksMcp.Application.Listings;

/// <summary>
/// Channel listings operations.
/// </summary>
public sealed class ListingService(ILinnworksClient client)
{
    internal const string GetListingsPath = "/api/Listings/GetListingsBySKU";
    internal const string GetItemListingsPath = "/api/Listings/GetInventoryItemListings";
    internal const string GetListingErrorsPath = "/api/Listings/GetListingErrors";

    public async Task<PagedResult<ListingItemDto>> GetListingsAsync(
        string? skuKeyword,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var request = new GetListingsRequest(
            SKU: skuKeyword ?? string.Empty,
            PageNumber: pageNumber,
            EntriesPerPage: pageSize);

        var response = await client
            .PostAsync<GetListingsRequest, GetListingsResponse>(GetListingsPath, request, cancellationToken)
            .ConfigureAwait(false);

        var items = response.Listings?.Select(Project).ToList() ?? [];
        return PagedResult<ListingItemDto>.Create(items, pageNumber, pageSize, totalCount: response.TotalCount);
    }

    public async Task<List<ListingItemDto>> GetListingByIdAsync(
        string stockItemId,
        CancellationToken cancellationToken)
    {
        var request = new GetItemListingsRequest(StockItemId: stockItemId);
        var response = await client
            .PostAsync<GetItemListingsRequest, List<ListingResponse>>(GetItemListingsPath, request, cancellationToken)
            .ConfigureAwait(false);

        return response?.Select(Project).ToList() ?? [];
    }

    public async Task<PagedResult<ListingErrorDto>> GetChannelListingErrorsAsync(
        string? subSource,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var request = new GetListingErrorsRequest(
            SubSource: subSource,
            PageNumber: pageNumber,
            EntriesPerPage: pageSize);

        var response = await client
            .PostAsync<GetListingErrorsRequest, GetListingErrorsResponse>(GetListingErrorsPath, request, cancellationToken)
            .ConfigureAwait(false);

        var items = response.Errors?.Select(e => new ListingErrorDto(
            e.ListingId ?? string.Empty,
            e.ChannelSKU ?? string.Empty,
            e.ErrorMessage ?? string.Empty,
            e.ErrorDate)).ToList() ?? [];

        return PagedResult<ListingErrorDto>.Create(items, pageNumber, pageSize, totalCount: response.TotalCount);
    }

    private static ListingItemDto Project(ListingResponse r) => new(
        ListingId: r.Id ?? string.Empty,
        StockItemId: r.StockItemId ?? string.Empty,
        ChannelSKU: r.ChannelSKU ?? string.Empty,
        Source: r.Source ?? string.Empty,
        SubSource: r.SubSource ?? string.Empty,
        Title: r.Title ?? string.Empty,
        Price: r.Price,
        Status: r.Status ?? "Unknown");
}

public sealed record GetListingsRequest(
    [property: JsonPropertyName("SKU")] string SKU,
    [property: JsonPropertyName("PageNumber")] int PageNumber,
    [property: JsonPropertyName("EntriesPerPage")] int EntriesPerPage);

public sealed record GetListingsResponse(
    [property: JsonPropertyName("Listings")] List<ListingResponse>? Listings,
    [property: JsonPropertyName("TotalCount")] int TotalCount);

public sealed record GetItemListingsRequest(
    [property: JsonPropertyName("StockItemId")] string StockItemId);

public sealed record GetListingErrorsRequest(
    [property: JsonPropertyName("SubSource")] string? SubSource,
    [property: JsonPropertyName("PageNumber")] int PageNumber,
    [property: JsonPropertyName("EntriesPerPage")] int EntriesPerPage);

public sealed record GetListingErrorsResponse(
    [property: JsonPropertyName("Errors")] List<ListingErrorResponse>? Errors,
    [property: JsonPropertyName("TotalCount")] int TotalCount);

public sealed record ListingResponse(
    [property: JsonPropertyName("Id")] string? Id,
    [property: JsonPropertyName("StockItemId")] string? StockItemId,
    [property: JsonPropertyName("ChannelSKU")] string? ChannelSKU,
    [property: JsonPropertyName("Source")] string? Source,
    [property: JsonPropertyName("SubSource")] string? SubSource,
    [property: JsonPropertyName("Title")] string? Title,
    [property: JsonPropertyName("Price")] decimal Price,
    [property: JsonPropertyName("Status")] string? Status);

public sealed record ListingErrorResponse(
    [property: JsonPropertyName("ListingId")] string? ListingId,
    [property: JsonPropertyName("ChannelSKU")] string? ChannelSKU,
    [property: JsonPropertyName("ErrorMessage")] string? ErrorMessage,
    [property: JsonPropertyName("ErrorDate")] DateTimeOffset ErrorDate);

public sealed record ListingItemDto(
    string ListingId,
    string StockItemId,
    string ChannelSKU,
    string Source,
    string SubSource,
    string Title,
    decimal Price,
    string Status);

public sealed record ListingErrorDto(
    string ListingId,
    string ChannelSKU,
    string ErrorMessage,
    DateTimeOffset ErrorDate);
