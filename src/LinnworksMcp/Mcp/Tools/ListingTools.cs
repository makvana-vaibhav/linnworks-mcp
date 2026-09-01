using System.ComponentModel;
using LinnworksMcp.Application.Listings;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Utils;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace LinnworksMcp.Mcp.Tools;

/// <summary>
/// Channel listings tools.
/// </summary>
[McpServerToolType]
public sealed class ListingTools(
    ListingService listingService,
    IToolAuthorizer authorizer,
    ToolMetrics metrics,
    ILogger<ListingTools> logger)
{
    [McpServerTool(Name = "get_listings", ReadOnly = true, Idempotent = true)]
    [Description(
        "Retrieve sales channel listings by SKU keyword. Read-only. "
        + "Results are page-based: pass pageNumber (1-based) and pageSize (max 200).")]
    public Task<string> GetListingsAsync(
        [Description("Optional SKU keyword to search for listings.")]
        string? skuKeyword = null,
        [Description("Page number, 1-based. Defaults to 1.")]
        int pageNumber = 1,
        [Description("Listings per page. Defaults to 50, maximum 200.")]
        int pageSize = ToolValidation.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_listings", logger, metrics, async ct =>
        {
            var (page, size) = ToolValidation.Paging(pageNumber, pageSize);
            await authorizer.AuthorizeAsync("get_listings", destructive: false, ct).ConfigureAwait(false);
            return await listingService.GetListingsAsync(skuKeyword, page, size, ct).ConfigureAwait(false);
        }, cancellationToken);

    [McpServerTool(Name = "get_listing_by_id", ReadOnly = true, Idempotent = true)]
    [Description(
        "Get all channel listings associated with a specific StockItemId (UUID). Read-only.")]
    public Task<string> GetListingByIdAsync(
        [Description("The StockItemId (UUID) of the inventory item.")]
        string stockItemId,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_listing_by_id", logger, metrics, async ct =>
        {
            var id = ToolValidation.RequiredGuid(nameof(stockItemId), stockItemId);
            await authorizer.AuthorizeAsync("get_listing_by_id", destructive: false, ct).ConfigureAwait(false);
            return await listingService.GetListingByIdAsync(id, ct).ConfigureAwait(false);
        }, cancellationToken);

    [McpServerTool(Name = "get_channel_listing_errors", ReadOnly = true, Idempotent = true)]
    [Description(
        "List error logs for channel listing synchronization. Read-only. "
        + "Results are page-based: pass pageNumber (1-based) and pageSize (max 200).")]
    public Task<string> GetChannelListingErrorsAsync(
        [Description("Optional channel sub-source name (e.g. 'EBAY_US', 'AMAZON_UK').")]
        string? subSource = null,
        [Description("Page number, 1-based. Defaults to 1.")]
        int pageNumber = 1,
        [Description("Errors per page. Defaults to 50, maximum 200.")]
        int pageSize = ToolValidation.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_channel_listing_errors", logger, metrics, async ct =>
        {
            var (page, size) = ToolValidation.Paging(pageNumber, pageSize);
            await authorizer.AuthorizeAsync("get_channel_listing_errors", destructive: false, ct).ConfigureAwait(false);
            return await listingService.GetChannelListingErrorsAsync(subSource, page, size, ct).ConfigureAwait(false);
        }, cancellationToken);
}
