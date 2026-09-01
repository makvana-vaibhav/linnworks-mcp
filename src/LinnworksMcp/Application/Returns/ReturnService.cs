using System.Text.Json.Serialization;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Models;

namespace LinnworksMcp.Application.Returns;

/// <summary>
/// Returns and RMAs operations.
/// </summary>
public sealed class ReturnService(ILinnworksClient client)
{
    // VERIFIED endpoint paths (the ones previously coded here do not exist and return 404).
    // The namespace is ReturnsRefunds, not Returns:
    //   actionable list  -> POST /api/ReturnsRefunds/GetActionableRefundHeaders
    //   create refund    -> POST /api/ReturnsRefunds/CreateRefund     (destructive)
    //   action a refund  -> POST /api/ReturnsRefunds/ActionRefund     (destructive)
    // Verify each request/response schema before re-registering ReturnTools in McpServerSetup.

    internal const string SearchReturnsPath = "/api/Returns/SearchReturns";
    internal const string CreateReturnPath = "/api/Returns/CreateReturn";

    public async Task<PagedResult<ReturnDto>> GetReturnsAsync(
        string? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var request = new SearchReturnsRequest(
            Status: status,
            PageNumber: pageNumber,
            EntriesPerPage: pageSize);

        var response = await client
            .PostAsync<SearchReturnsRequest, SearchReturnsResponse>(SearchReturnsPath, request, cancellationToken)
            .ConfigureAwait(false);

        var items = response.Returns?.Select(Project).ToList() ?? [];
        return PagedResult<ReturnDto>.Create(items, pageNumber, pageSize, totalCount: response.TotalCount);
    }

    public async Task<ReturnDto> CreateReturnAsync(
        string orderId,
        string reason,
        CancellationToken cancellationToken)
    {
        var request = new CreateReturnRequest(
            OrderId: orderId,
            Reason: reason);

        var response = await client
            .PostAsync<CreateReturnRequest, ReturnResponse>(CreateReturnPath, request, cancellationToken)
            .ConfigureAwait(false);

        return Project(response);
    }

    private static ReturnDto Project(ReturnResponse r) => new(
        ReturnId: r.ReturnId ?? string.Empty,
        OrderId: r.OrderId ?? string.Empty,
        Reason: r.Reason ?? string.Empty,
        Status: r.Status ?? "BOOKED",
        DateCreated: r.DateCreated);
}

public sealed record SearchReturnsRequest(
    [property: JsonPropertyName("Status")] string? Status,
    [property: JsonPropertyName("PageNumber")] int PageNumber,
    [property: JsonPropertyName("EntriesPerPage")] int EntriesPerPage);

public sealed record SearchReturnsResponse(
    [property: JsonPropertyName("Returns")] List<ReturnResponse>? Returns,
    [property: JsonPropertyName("TotalCount")] int TotalCount);

public sealed record CreateReturnRequest(
    [property: JsonPropertyName("OrderId")] string OrderId,
    [property: JsonPropertyName("Reason")] string Reason);

public sealed record ReturnResponse(
    [property: JsonPropertyName("ReturnId")] string? ReturnId,
    [property: JsonPropertyName("OrderId")] string? OrderId,
    [property: JsonPropertyName("Reason")] string? Reason,
    [property: JsonPropertyName("Status")] string? Status,
    [property: JsonPropertyName("DateCreated")] DateTimeOffset DateCreated);

public sealed record ReturnDto(
    string ReturnId,
    string OrderId,
    string Reason,
    string Status,
    DateTimeOffset DateCreated);
