using System.Text.Json.Serialization;

namespace LinnworksMcp.Models;

/// <summary>
/// Compact projection of a Linnworks stock item, as returned by list tools.
/// </summary>
/// <remarks>
/// Linnworks' raw item payloads are large and mostly irrelevant to a chatbot. Projecting keeps
/// tool responses small enough to be usable as model context — rishvi-agent returned raw
/// payloads and had to cap page sizes at 10 to stop responses blowing up.
/// </remarks>
public sealed class InventoryItem
{
    public required string StockItemId { get; init; }

    /// <summary>SKU. Linnworks calls this ItemNumber on detail responses.</summary>
    public required string Sku { get; init; }

    public required string Title { get; init; }

    public string? Barcode { get; init; }

    public string? CategoryName { get; init; }

    /// <summary>Total stock across all locations.</summary>
    public int StockLevel { get; init; }

    /// <summary>Stock available to sell — total minus what is committed to open orders.</summary>
    public int Available { get; init; }

    public int InOrderBook { get; init; }

    public int MinimumLevel { get; init; }

    public int Due { get; init; }

    public decimal RetailPrice { get; init; }

    public decimal PurchasePrice { get; init; }

    public double Weight { get; init; }
}

/// <summary>Full detail projection, from <c>GET /api/Inventory/GetInventoryItemById</c>.</summary>
public sealed class InventoryItemDetail
{
    public required string StockItemId { get; init; }

    public required string Sku { get; init; }

    public required string Title { get; init; }

    public string? Barcode { get; init; }

    public string? CategoryName { get; init; }

    public string? PackageGroupName { get; init; }

    public string? PostalServiceName { get; init; }

    public int Quantity { get; init; }

    public int Available { get; init; }

    public int InOrder { get; init; }

    public int Due { get; init; }

    public int MinimumLevel { get; init; }

    public decimal RetailPrice { get; init; }

    public decimal PurchasePrice { get; init; }

    public double TaxRate { get; init; }

    public double Weight { get; init; }

    public double Height { get; init; }

    public double Width { get; init; }

    public double Depth { get; init; }

    public DateTimeOffset? CreationDate { get; init; }
}

// ── Wire contracts ───────────────────────────────────────────────────────────
// Request bodies are camelCase, responses PascalCase. Both are annotated explicitly rather
// than relying on a global naming policy, because a single policy cannot satisfy both.

/// <summary>Request for <c>POST /api/Stock/GetStockItemsFull</c>.</summary>
internal sealed class GetStockItemsFullRequest
{
    [JsonPropertyName("keyword")]
    public string? Keyword { get; init; }

    /// <summary>Fields the keyword is matched against: SKU, Title, Barcode.</summary>
    [JsonPropertyName("searchTypes")]
    public required string[] SearchTypes { get; init; }

    [JsonPropertyName("pageNumber")]
    public required int PageNumber { get; init; }

    /// <summary>Linnworks caps this at 200.</summary>
    [JsonPropertyName("entriesPerPage")]
    public required int EntriesPerPage { get; init; }

    /// <summary>
    /// Extra data to load: StockLevels, Pricing, Supplier, ShippingInformation, ChannelTitle,
    /// ChannelDescription, ChannelPrice, ExtendedProperties, Images. Each one costs response
    /// size, so request only StockLevels and Pricing.
    /// </summary>
    [JsonPropertyName("dataRequirements")]
    public required string[] DataRequirements { get; init; }

    [JsonPropertyName("loadCompositeParents")]
    public bool LoadCompositeParents { get; init; }

    [JsonPropertyName("loadVariationParents")]
    public bool LoadVariationParents { get; init; }
}

/// <summary>Response element of <c>GetStockItemsFull</c>.</summary>
internal sealed class StockItemFullResponse
{
    public string StockItemId { get; init; } = string.Empty;

    public string ItemNumber { get; init; } = string.Empty;

    public string ItemTitle { get; init; } = string.Empty;

    public string? BarcodeNumber { get; init; }

    public string? CategoryName { get; init; }

    public decimal RetailPrice { get; init; }

    public decimal PurchasePrice { get; init; }

    public double Weight { get; init; }

    // StockItemFull carries no MinimumLevel or IsArchived of its own — MinimumLevel is per
    // location on StockLevels, and archived state is not returned by this endpoint at all.
    public List<StockItemLevelResponse>? StockLevels { get; init; }
}

/// <summary>Response element of <c>GetInventoryItemById</c> (a <c>StockItemInv</c>).</summary>
internal sealed class StockItemInvResponse
{
    public string StockItemId { get; init; } = string.Empty;

    public string ItemNumber { get; init; } = string.Empty;

    public string ItemTitle { get; init; } = string.Empty;

    public string? BarcodeNumber { get; init; }

    public string? CategoryName { get; init; }

    public string? PackageGroupName { get; init; }

    public string? PostalServiceName { get; init; }

    public int Quantity { get; init; }

    public int Available { get; init; }

    public int InOrder { get; init; }

    public int Due { get; init; }

    public int MinimumLevel { get; init; }

    public decimal RetailPrice { get; init; }

    public decimal PurchasePrice { get; init; }

    public double TaxRate { get; init; }

    public double Weight { get; init; }

    public double Height { get; init; }

    public double Width { get; init; }

    public double Depth { get; init; }

    public DateTimeOffset? CreationDate { get; init; }
}
