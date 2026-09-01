namespace LinnworksMcp.Models;

/// <summary>
/// A warehouse / stock location.
/// </summary>
/// <remarks>
/// Exposing these matters more than it looks: Inventory and Stock tools take location UUIDs,
/// and without a way to list them a chatbot has no route from "the Manchester warehouse" to the
/// id those tools need.
/// </remarks>
public sealed class Location
{
    public required string StockLocationId { get; init; }

    public required string LocationName { get; init; }

    public string? City { get; init; }

    public string? Country { get; init; }

    public string? ZipCode { get; init; }

    /// <summary>Locations flagged not-trackable do not hold meaningful stock figures.</summary>
    public bool IsNotTrackable { get; init; }

    public bool IsFulfillmentCenter { get; init; }

    public bool IsWarehouseManaged { get; init; }
}

// ── Wire contracts ───────────────────────────────────────────────────────────

/// <summary>Response element of <c>GET /api/Inventory/GetStockLocations</c>.</summary>
internal sealed class StockLocationResponse
{
    public string StockLocationId { get; init; } = string.Empty;

    public string LocationName { get; init; } = string.Empty;

    public string? City { get; init; }

    public string? Country { get; init; }

    public string? ZipCode { get; init; }

    public bool IsNotTrackable { get; init; }

    public bool IsFulfillmentCenter { get; init; }

    public bool IsWarehouseManaged { get; init; }
}
