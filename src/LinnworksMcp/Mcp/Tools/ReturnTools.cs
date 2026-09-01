using System.ComponentModel;
using LinnworksMcp.Application.Returns;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Utils;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace LinnworksMcp.Mcp.Tools;

/// <summary>
/// Returns and RMAs tools.
/// </summary>
[McpServerToolType]
public sealed class ReturnTools(
    ReturnService returnService,
    IToolAuthorizer authorizer,
    ToolMetrics metrics,
    ILogger<ReturnTools> logger)
{
    [McpServerTool(Name = "get_returns", ReadOnly = true, Idempotent = true)]
    [Description(
        "Search customer returns / RMA requests. Read-only. "
        + "Results are page-based: pass pageNumber (1-based) and pageSize (max 200).")]
    public Task<string> GetReturnsAsync(
        [Description("Optional status filter (e.g., 'BOOKED', 'APPROVED', 'COMPLETED').")]
        string? status = null,
        [Description("Page number, 1-based. Defaults to 1.")]
        int pageNumber = 1,
        [Description("Returns per page. Defaults to 50, maximum 200.")]
        int pageSize = ToolValidation.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_returns", logger, metrics, async ct =>
        {
            var (page, size) = ToolValidation.Paging(pageNumber, pageSize);
            await authorizer.AuthorizeAsync("get_returns", destructive: false, ct).ConfigureAwait(false);
            return await returnService.GetReturnsAsync(status, page, size, ct).ConfigureAwait(false);
        }, cancellationToken);

    [McpServerTool(Name = "create_return", Destructive = true)]
    [Description(
        "MUTATES DATA. Creates a return / RMA request for an order. "
        + "Confirm order ID and return reason with the user before calling.")]
    public Task<string> CreateReturnAsync(
        [Description("The Order UUID (pkOrderId).")]
        string orderId,
        [Description("Reason description for the return.")]
        string reason,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("create_return", logger, metrics, async ct =>
        {
            var validOrderId = ToolValidation.RequiredGuid(nameof(orderId), orderId);
            var validReason = ToolValidation.RequiredText(nameof(reason), reason);

            await authorizer.AuthorizeAsync("create_return", destructive: true, ct).ConfigureAwait(false);
            return await returnService.CreateReturnAsync(validOrderId, validReason, ct).ConfigureAwait(false);
        }, cancellationToken);
}
