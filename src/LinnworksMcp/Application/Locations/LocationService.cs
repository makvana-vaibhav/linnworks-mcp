using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Models;

namespace LinnworksMcp.Application.Locations;

/// <summary>Warehouse / stock location lookups.</summary>
public sealed class LocationService(ILinnworksClient client)
{
    internal const string GetStockLocationsPath = "/api/Inventory/GetStockLocations";

    /// <summary>
    /// All stock locations via <c>GET /api/Inventory/GetStockLocations</c>.
    /// </summary>
    /// <remarks>
    /// The endpoint takes no parameters and returns every location in one response. Accounts
    /// have few enough locations for that to be safe, so paging is applied client-side purely
    /// to keep the response envelope consistent with every other list tool.
    /// </remarks>
    public async Task<PagedResult<Location>> GetLocationsAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var response = await client
            .GetAsync<List<StockLocationResponse>>(
                GetStockLocationsPath, query: null, cancellationToken)
            .ConfigureAwait(false);

        var all = response.Select(Project).ToList();

        var page = all
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return PagedResult<Location>.Create(page, pageNumber, pageSize, totalCount: all.Count);
    }

    /// <summary>
    /// A single location by id. Linnworks has no by-id endpoint for locations, so this filters
    /// the full list rather than adding a second round trip elsewhere.
    /// </summary>
    public async Task<Location?> GetLocationByIdAsync(
        string stockLocationId,
        CancellationToken cancellationToken)
    {
        var response = await client
            .GetAsync<List<StockLocationResponse>>(
                GetStockLocationsPath, query: null, cancellationToken)
            .ConfigureAwait(false);

        return response
            .Where(l => string.Equals(l.StockLocationId, stockLocationId, StringComparison.OrdinalIgnoreCase))
            .Select(Project)
            .FirstOrDefault();
    }

    private static Location Project(StockLocationResponse l) => new()
    {
        StockLocationId = l.StockLocationId,
        LocationName = l.LocationName,
        City = l.City,
        Country = l.Country,
        ZipCode = l.ZipCode,
        IsNotTrackable = l.IsNotTrackable,
        IsFulfillmentCenter = l.IsFulfillmentCenter,
        IsWarehouseManaged = l.IsWarehouseManaged
    };
}
