using System.ComponentModel;
using LinnworksMcp.Application.Locations;
using LinnworksMcp.Application.Stock;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Utils;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace LinnworksMcp.Mcp.Tools;

/// <summary>
/// Warehouse / location tools.
/// </summary>
/// <remarks>
/// These are the entry point for most other tools: inventory, stock and order tools all take a
/// location UUID, and this is the only way to discover one from a location's name.
/// </remarks>
[McpServerToolType]
public sealed class LocationTools(
    LocationService locationService,
    StockService stockService,
    IToolAuthorizer authorizer,
    ToolMetrics metrics,
    ILogger<LocationTools> logger)
{
    [McpServerTool(Name = "get_locations", ReadOnly = true, Idempotent = true)]
    [Description(
        "List all Linnworks warehouse / stock locations with their UUIDs and names. Read-only. "
        + "Call this first to translate a location name into the UUID that inventory, stock and "
        + "order tools require. Page-based, though most accounts have few enough locations to "
        + "fit in one page.")]
    public Task<string> GetLocationsAsync(
        [Description("Page number, 1-based. Defaults to 1.")]
        int pageNumber = 1,
        [Description("Locations per page. Defaults to 50, maximum 200.")]
        int pageSize = ToolValidation.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_locations", logger, metrics, async ct =>
        {
            var (page, size) = ToolValidation.Paging(pageNumber, pageSize);

            await authorizer.AuthorizeAsync("get_locations", destructive: false, ct)
                .ConfigureAwait(false);

            return await locationService.GetLocationsAsync(page, size, ct).ConfigureAwait(false);
        }, cancellationToken);

    [McpServerTool(Name = "get_location_by_id", ReadOnly = true, Idempotent = true)]
    [Description(
        "Get one Linnworks warehouse location by its UUID, including address and whether it is "
        + "a fulfilment centre. Read-only.")]
    public Task<string> GetLocationByIdAsync(
        [Description("The StockLocationId (UUID) of the location.")]
        string stockLocationId,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_location_by_id", logger, metrics, async ct =>
        {
            var id = ToolValidation.RequiredGuid(nameof(stockLocationId), stockLocationId);

            await authorizer.AuthorizeAsync("get_location_by_id", destructive: false, ct)
                .ConfigureAwait(false);

            var location = await locationService.GetLocationByIdAsync(id, ct).ConfigureAwait(false);

            return location ?? throw new Infrastructure.Linnworks.LinnworksApiException(
                Infrastructure.Linnworks.LinnworksErrorKind.NotFound,
                "The requested resource was not found.",
                $"No stock location matched id {id}.");
        }, cancellationToken);

    [McpServerTool(Name = "get_stock_by_location", ReadOnly = true, Idempotent = true)]
    [Description(
        "Get stock levels for specific inventory items at one warehouse location. Read-only. "
        + "Use this to answer 'how much of X is in warehouse Y'. Accepts up to 200 item ids.")]
    public Task<string> GetStockByLocationAsync(
        [Description("Warehouse location UUID. Use get_locations to look this up.")]
        string locationId,
        [Description("StockItemId UUIDs to look up, comma-separated. Maximum 200.")]
        string stockItemIds,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_stock_by_location", logger, metrics, async ct =>
        {
            var location = ToolValidation.RequiredGuid(nameof(locationId), locationId);

            var ids = (stockItemIds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (ids.Count == 0)
            {
                throw ToolValidation.Invalid(nameof(stockItemIds), "must contain at least one UUID.");
            }

            if (ids.Count > ToolValidation.MaxPageSize)
            {
                throw ToolValidation.Invalid(
                    nameof(stockItemIds),
                    $"must not contain more than {ToolValidation.MaxPageSize} ids (received {ids.Count}).");
            }

            var validated = ids
                .Select(id => ToolValidation.RequiredGuid(nameof(stockItemIds), id))
                .ToList();

            await authorizer.AuthorizeAsync("get_stock_by_location", destructive: false, ct)
                .ConfigureAwait(false);

            return await stockService
                .GetStockLevelsAsync(validated, location, ct)
                .ConfigureAwait(false);
        }, cancellationToken);
}
