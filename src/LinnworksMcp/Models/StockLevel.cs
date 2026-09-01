using System.Text.Json.Serialization;

namespace LinnworksMcp.Models;

/// <summary>Stock held for one item at one location.</summary>
public sealed class StockLevel
{
    public required string StockItemId { get; init; }

    public required string Sku { get; init; }

    public string? LocationId { get; init; }

    public string? LocationName { get; init; }

    public int Quantity { get; init; }

    /// <summary>Quantity available to sell, after open-order commitments.</summary>
    public int Available { get; init; }

    public int InOrderBook { get; init; }

    public int MinimumLevel { get; init; }

    public int Due { get; init; }

    public decimal StockValue { get; init; }

    public DateTimeOffset? LastUpdateDate { get; init; }

    public string? LastUpdateOperation { get; init; }
}

/// <summary>An item at or below its configured minimum level.</summary>
public sealed class LowStockItem
{
    public required string Sku { get; init; }

    public required string Title { get; init; }

    public int Quantity { get; init; }

    public int MinimumLevel { get; init; }

    /// <summary>Quantity committed to open orders.</summary>
    public int InOrderBook { get; init; }

    public string? LocationName { get; init; }

    /// <summary>How far below the minimum this item sits. Zero when exactly at the minimum.</summary>
    public int Shortage => Math.Max(0, MinimumLevel - Quantity);
}

// ── Wire contracts ───────────────────────────────────────────────────────────

/// <summary>
/// Request for <c>POST /api/Stock/GetStockLevel_Batch</c>. Note the nested <c>request</c>
/// wrapper — the payload is not flat.
/// </summary>
internal sealed class GetStockLevelBatchRequest
{
    [JsonPropertyName("request")]
    public required StockItemIdsPayload Request { get; init; }

    internal sealed class StockItemIdsPayload
    {
        [JsonPropertyName("StockItemIds")]
        public required string[] StockItemIds { get; init; }
    }
}

internal sealed class GetStockLevelBatchResponse
{
    public string pkStockItemId { get; init; } = string.Empty;

    public List<StockItemLevelResponse>? StockItemLevels { get; init; }
}

/// <summary>Request for <c>POST /api/Stock/SetStockLevel</c>.</summary>
internal sealed class SetStockLevelRequest
{
    [JsonPropertyName("stockLevels")]
    public required StockLevelUpdate[] StockLevels { get; init; }

    /// <summary>Free-text audit label recorded against the change in Linnworks.</summary>
    [JsonPropertyName("changeSource")]
    public string ChangeSource { get; init; } = "LinnworksMcp";

    internal sealed class StockLevelUpdate
    {
        [JsonPropertyName("SKU")]
        public required string Sku { get; init; }

        [JsonPropertyName("LocationId")]
        public required string LocationId { get; init; }

        /// <summary>The absolute quantity to set — this is not a delta.</summary>
        [JsonPropertyName("Level")]
        public required int Level { get; init; }
    }
}

/// <summary>Shared <c>StockItemLevel</c> element used by several Stock endpoints.</summary>
internal sealed class StockItemLevelResponse
{
    public InventoryStockLocationResponse? Location { get; init; }

    public string? SKU { get; init; }

    public string? StockItemId { get; init; }

    public int StockLevel { get; init; }

    public decimal StockValue { get; init; }

    public int MinimumLevel { get; init; }

    public int InOrderBook { get; init; }

    public int Due { get; init; }

    public int Available { get; init; }

    public DateTimeOffset? LastUpdateDate { get; init; }

    public string? LastUpdateOperation { get; init; }
}

internal sealed class InventoryStockLocationResponse
{
    public string StockLocationId { get; init; } = string.Empty;

    public string LocationName { get; init; } = string.Empty;
}

/// <summary>Response element of <c>GET /api/Dashboards/GetLowStockLevel</c>.</summary>
internal sealed class LowStockLevelResponse
{
    public string ItemTitle { get; init; } = string.Empty;

    public string ItemNumber { get; init; } = string.Empty;

    public int Quantity { get; init; }

    public int MinimumLevel { get; init; }

    public int InBooks { get; init; }

    public string? Location { get; init; }
}
