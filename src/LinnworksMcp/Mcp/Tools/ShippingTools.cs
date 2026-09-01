using System.ComponentModel;
using LinnworksMcp.Application.Shipping;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Utils;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace LinnworksMcp.Mcp.Tools;

/// <summary>
/// Shipping services tools.
/// </summary>
[McpServerToolType]
public sealed class ShippingTools(
    ShippingService shippingService,
    IToolAuthorizer authorizer,
    ToolMetrics metrics,
    ILogger<ShippingTools> logger)
{
    [McpServerTool(Name = "get_shipping_services", ReadOnly = true, Idempotent = true)]
    [Description(
        "List configured postal services and carriers (vendors) in Linnworks. Read-only.")]
    public Task<string> GetShippingServicesAsync(CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_shipping_services", logger, metrics, async ct =>
        {
            await authorizer.AuthorizeAsync("get_shipping_services", destructive: false, ct).ConfigureAwait(false);
            return await shippingService.GetShippingServicesAsync(ct).ConfigureAwait(false);
        }, cancellationToken);

    [McpServerTool(Name = "get_order_shipping_info", ReadOnly = true, Idempotent = true)]
    [Description(
        "Retrieve tracking and courier shipping information for a specific order UUID. Read-only.")]
    public Task<string> GetOrderShippingInfoAsync(
        [Description("The Order UUID (pkOrderId).")]
        string orderId,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_order_shipping_info", logger, metrics, async ct =>
        {
            var id = ToolValidation.RequiredGuid(nameof(orderId), orderId);
            await authorizer.AuthorizeAsync("get_order_shipping_info", destructive: false, ct).ConfigureAwait(false);
            var result = await shippingService.GetTrackingInfoAsync(id, ct).ConfigureAwait(false);
            return result is not null
                ? System.Text.Json.JsonSerializer.Serialize(result)
                : throw new Infrastructure.Linnworks.LinnworksApiException(
                    Infrastructure.Linnworks.LinnworksErrorKind.NotFound,
                    "Shipping information not found for the requested order.",
                    $"No tracking info for order {id}.");
        }, cancellationToken);
}
