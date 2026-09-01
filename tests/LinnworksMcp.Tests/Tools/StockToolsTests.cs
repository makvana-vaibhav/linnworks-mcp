using LinnworksMcp.Application.Stock;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Mcp.Tools;
using LinnworksMcp.Models;
using LinnworksMcp.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LinnworksMcp.Tests.Tools;

public class StockToolsTests
{
    private readonly Mock<IToolAuthorizer> _authorizerMock = new();
    private readonly ToolMetrics _metrics = TestToolMetrics.Create();
    private readonly Mock<ILinnworksClient> _clientMock = new();

    public StockToolsTests()
    {
        _authorizerMock
            .Setup(a => a.AuthorizeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task UpdateStockLevelsAsync_InvokesAuthorizerWithDestructiveTrue()
    {
        _clientMock
            .Setup(c => c.PostAsync<SetStockLevelRequest, List<StockItemLevelResponse>>(
                StockService.SetStockLevelPath, It.IsAny<SetStockLevelRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new StockItemLevelResponse
                {
                    StockItemId = Guid.NewGuid().ToString(),
                    SKU = "TEST-SKU",
                    StockLevel = 50
                }
            ]);

        var stockService = new StockService(_clientMock.Object, NullLogger<StockService>.Instance);
        var tools = new StockTools(
            stockService,
            _authorizerMock.Object,
            _metrics,
            NullLogger<StockTools>.Instance);

        var validLocationId = Guid.NewGuid().ToString();

        // Act
        var response = await tools.UpdateStockLevelsAsync("TEST-SKU", validLocationId, 50, "AuditNote", CancellationToken.None);

        // Assert
        Assert.Contains("TEST-SKU", response);
        Assert.Contains("50", response);
        _authorizerMock.Verify(a => a.AuthorizeAsync("update_stock_levels", true, It.IsAny<CancellationToken>()), Times.Once);
    }
}
