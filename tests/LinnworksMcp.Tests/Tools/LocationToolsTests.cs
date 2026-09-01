using LinnworksMcp.Application.Locations;
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

public class LocationToolsTests
{
    private readonly Mock<IToolAuthorizer> _authorizerMock = new();
    private readonly ToolMetrics _metrics = TestToolMetrics.Create();
    private readonly Mock<ILinnworksClient> _clientMock = new();

    public LocationToolsTests()
    {
        _authorizerMock
            .Setup(a => a.AuthorizeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task GetLocationsAsync_InvokesServiceAndReturnsJson()
    {
        _clientMock
            .Setup(c => c.GetAsync<List<StockLocationResponse>>(
                LocationService.GetStockLocationsPath, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new StockLocationResponse
                {
                    StockLocationId = Guid.NewGuid().ToString(),
                    LocationName = "Default Warehouse",
                    City = "London",
                    Country = "United Kingdom"
                }
            ]);

        var locationService = new LocationService(_clientMock.Object);
        var stockService = new StockService(_clientMock.Object, NullLogger<StockService>.Instance);

        var tools = new LocationTools(
            locationService,
            stockService,
            _authorizerMock.Object,
            _metrics,
            NullLogger<LocationTools>.Instance);

        // Act
        var resultJson = await tools.GetLocationsAsync(1, 50, CancellationToken.None);

        // Assert
        Assert.Contains("Default Warehouse", resultJson);
        Assert.Contains("London", resultJson);
    }
}
