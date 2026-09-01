using System.ComponentModel;
using LinnworksMcp.Application.PurchaseOrders;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Utils;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace LinnworksMcp.Mcp.Tools;

/// <summary>
/// Purchase order tools.
/// </summary>
[McpServerToolType]
public sealed class PurchaseOrderTools(
    PurchaseOrderService purchaseOrderService,
    IToolAuthorizer authorizer,
    ToolMetrics metrics,
    ILogger<PurchaseOrderTools> logger)
{
    [McpServerTool(Name = "get_purchase_orders", ReadOnly = true, Idempotent = true)]
    [Description(
        "List purchase orders from suppliers. Read-only. "
        + "Results are page-based: pass pageNumber (1-based) and pageSize (max 200).")]
    public Task<string> GetPurchaseOrdersAsync(
        [Description("Optional status filter (e.g., 'OPEN', 'DELIVERED', 'PENDING').")]
        string? status = null,
        [Description("Page number, 1-based. Defaults to 1.")]
        int pageNumber = 1,
        [Description("Purchase orders per page. Defaults to 50, maximum 200.")]
        int pageSize = ToolValidation.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_purchase_orders", logger, metrics, async ct =>
        {
            var (page, size) = ToolValidation.Paging(pageNumber, pageSize);
            await authorizer.AuthorizeAsync("get_purchase_orders", destructive: false, ct).ConfigureAwait(false);
            return await purchaseOrderService.GetPurchaseOrdersAsync(status, page, size, ct).ConfigureAwait(false);
        }, cancellationToken);

    [McpServerTool(Name = "create_purchase_order", Destructive = true)]
    [Description(
        "MUTATES DATA. Creates a new purchase order draft with a supplier for a warehouse location. "
        + "Confirm supplier ID, location UUID and reference with user before calling.")]
    public Task<string> CreatePurchaseOrderAsync(
        [Description("Supplier UUID.")]
        string supplierId,
        [Description("Warehouse location UUID. Use get_locations to look this up.")]
        string locationId,
        [Description("External reference code or PO number.")]
        string externalRef,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("create_purchase_order", logger, metrics, async ct =>
        {
            var validSupplier = ToolValidation.RequiredGuid(nameof(supplierId), supplierId);
            var validLocation = ToolValidation.RequiredGuid(nameof(locationId), locationId);
            var validRef = ToolValidation.RequiredText(nameof(externalRef), externalRef);

            await authorizer.AuthorizeAsync("create_purchase_order", destructive: true, ct).ConfigureAwait(false);
            return await purchaseOrderService.CreatePurchaseOrderAsync(validSupplier, validLocation, validRef, ct).ConfigureAwait(false);
        }, cancellationToken);
}
