using System.ComponentModel;
using LinnworksMcp.Application.Customers;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Utils;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace LinnworksMcp.Mcp.Tools;

/// <summary>
/// Customer tools.
/// </summary>
[McpServerToolType]
public sealed class CustomerTools(
    CustomerService customerService,
    IToolAuthorizer authorizer,
    ToolMetrics metrics,
    ILogger<CustomerTools> logger)
{
    [McpServerTool(Name = "search_customers", ReadOnly = true, Idempotent = true)]
    [Description(
        "Search customer records by name, email or address. Read-only. "
        + "Results are page-based: pass pageNumber (1-based) and pageSize (max 200).")]
    public Task<string> SearchCustomersAsync(
        [Description("Search term (customer name, email address or location).")]
        string searchKeyword,
        [Description("Page number, 1-based. Defaults to 1.")]
        int pageNumber = 1,
        [Description("Customers per page. Defaults to 50, maximum 200.")]
        int pageSize = ToolValidation.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("search_customers", logger, metrics, async ct =>
        {
            var validKeyword = ToolValidation.RequiredText(nameof(searchKeyword), searchKeyword);
            var (page, size) = ToolValidation.Paging(pageNumber, pageSize);
            await authorizer.AuthorizeAsync("search_customers", destructive: false, ct).ConfigureAwait(false);
            return await customerService.SearchCustomersAsync(validKeyword, page, size, ct).ConfigureAwait(false);
        }, cancellationToken);

    [McpServerTool(Name = "get_customer_by_id", ReadOnly = true, Idempotent = true)]
    [Description(
        "Retrieve detailed record of a single customer by CustomerId. Read-only.")]
    public Task<string> GetCustomerByIdAsync(
        [Description("The Linnworks CustomerId (UUID).")]
        string customerId,
        CancellationToken cancellationToken = default) =>
        ToolExecution.RunAsync("get_customer_by_id", logger, metrics, async ct =>
        {
            var id = ToolValidation.RequiredGuid(nameof(customerId), customerId);
            await authorizer.AuthorizeAsync("get_customer_by_id", destructive: false, ct).ConfigureAwait(false);
            var result = await customerService.GetCustomerByIdAsync(id, ct).ConfigureAwait(false);
            return result is not null
                ? System.Text.Json.JsonSerializer.Serialize(result)
                : throw new Infrastructure.Linnworks.LinnworksApiException(
                    Infrastructure.Linnworks.LinnworksErrorKind.NotFound,
                    "The requested customer was not found.",
                    $"No customer matched id {id}.");
        }, cancellationToken);
}
