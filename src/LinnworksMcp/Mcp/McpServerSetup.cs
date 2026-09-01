using System.Reflection;
using LinnworksMcp.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace LinnworksMcp.Mcp;

public static class McpServerSetup
{
    /// <summary>
    /// Registers the MCP server and every tool type.
    /// </summary>
    public static IMcpServerBuilder AddLinnworksMcpServer(this IServiceCollection services)
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "1.0.0";

        return services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "linnworks-mcp",
                    Title = "Linnworks MCP Server",
                    Version = version
                };

                options.ServerInstructions =
                    "Tools for querying and managing a Linnworks account: inventory, stock "
                    + "levels, orders, warehouse locations, listings, customers, shipping, purchase orders and returns.\n\n"
                    + "Most tools take a warehouse location UUID — call get_locations first to "
                    + "translate a location name into its id. Likewise, call get_inventory_items "
                    + "to resolve a SKU into a StockItemId before using item-level tools.\n\n"
                    + "List tools are paginated with pageNumber (1-based) and pageSize "
                    + "(maximum 200) and return an envelope with hasMore. Request a further page "
                    + "rather than a very large one.\n\n"
                    + "Tools whose description begins with MUTATES DATA change the live "
                    + "Linnworks account. Confirm the details with the user before calling one.";
            })
            .WithTools<InventoryTools>()
            .WithTools<StockTools>()
            .WithTools<OrderTools>()
            .WithTools<LocationTools>()
            .WithTools<ListingTools>()
            .WithTools<CustomerTools>()
            .WithTools<ShippingTools>()
            .WithTools<PurchaseOrderTools>()
            .WithTools<ReturnTools>();
    }
}
