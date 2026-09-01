using LinnworksMcp.Application.Inventory;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Mcp.Tools;
using LinnworksMcp.Models;
using LinnworksMcp.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LinnworksMcp.Tests.Tools;

public class InventoryToolsTests
{
    private readonly Mock<IToolAuthorizer> _authorizerMock = new();
    private readonly ToolMetrics _metrics = TestToolMetrics.Create();
    private readonly Mock<ILinnworksClient> _clientMock = new();

    public InventoryToolsTests()
    {
        _authorizerMock
            .Setup(a => a.AuthorizeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task GetInventoryItemsAsync_InvokesServiceAndReturnsJson()
    {
        _clientMock
            .Setup(c => c.PostAsync<GetStockItemsFullRequest, List<StockItemFullResponse>>(
                InventoryService.GetStockItemsFullPath, It.IsAny<GetStockItemsFullRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new StockItemFullResponse
                {
                    StockItemId = Guid.NewGuid().ToString(),
                    ItemNumber = "WIDGET-01",
                    ItemTitle = "Widget 01",
                    BarcodeNumber = "123456",
                    CategoryName = "General",
                    RetailPrice = 19.99m,
                    PurchasePrice = 8.50m
                }
            ]);

        var inventoryService = new InventoryService(_clientMock.Object, NullLogger<InventoryService>.Instance);
        var tools = new InventoryTools(
            inventoryService,
            _authorizerMock.Object,
            _metrics,
            NullLogger<InventoryTools>.Instance);

        // Act
        var resultJson = await tools.GetInventoryItemsAsync("WIDGET", 1, 10, CancellationToken.None);

        // Assert
        Assert.Contains("WIDGET-01", resultJson);
        Assert.Contains("19.99", resultJson);
        _authorizerMock.Verify(a => a.AuthorizeAsync("get_inventory_items", false, It.IsAny<CancellationToken>()), Times.Once);
    }
}
