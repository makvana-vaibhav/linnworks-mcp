using LinnworksMcp.Application.Locations;
using LinnworksMcp.Application.Orders;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Mcp.Tools;
using LinnworksMcp.Models;
using LinnworksMcp.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LinnworksMcp.Tests.Tools;

public class OrderToolsTests
{
    private readonly Mock<IToolAuthorizer> _authorizerMock = new();
    private readonly ToolMetrics _metrics = TestToolMetrics.Create();
    private readonly Mock<ILinnworksClient> _clientMock = new();

    public OrderToolsTests()
    {
        _authorizerMock
            .Setup(a => a.AuthorizeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task GetOpenOrdersAsync_ValidatesLocationId_AndReturnsJson()
    {
        _clientMock
            .Setup(c => c.PostAsync<GetOpenOrdersRequest, PostFilterPagedResponse<OrderDetailsResponse>>(
                OrderService.GetOpenOrdersPath, It.IsAny<GetOpenOrdersRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PostFilterPagedResponse<OrderDetailsResponse>
            {
                PageNumber = 1,
                EntriesPerPage = 10,
                TotalEntries = 1,
                TotalPages = 1,
                Data = [
                    new OrderDetailsResponse
                    {
                        OrderId = Guid.NewGuid().ToString(),
                        NumOrderId = 1001,
                        Processed = false,
                        GeneralInfo = new OrderGeneralInfoResponse
                        {
                            Status = 0,   // 0 = UNPAID, per the documented Linnworks enum
                            SubSource = "EBAY",
                            ReceivedDate = DateTimeOffset.UtcNow
                        },
                        TotalsInfo = new OrderTotalsInfoResponse
                        {
                            TotalCharge = 49.99m,
                            Currency = "GBP"
                        }
                    }
                ]
            });

        var orderService = new OrderService(_clientMock.Object, NullLogger<OrderService>.Instance);
        var locationService = new LocationService(_clientMock.Object);

        var tools = new OrderTools(
            orderService,
            locationService,
            _authorizerMock.Object,
            _metrics,
            NullLogger<OrderTools>.Instance);

        var locationId = Guid.NewGuid().ToString();

        // Act
        var resultJson = await tools.GetOpenOrdersAsync(locationId, 1, 10, CancellationToken.None);

        // Assert
        Assert.Contains("1001", resultJson);
        Assert.Contains("EBAY", resultJson);
        Assert.Contains("UNPAID", resultJson);   // status code resolved to its name
        _authorizerMock.Verify(a => a.AuthorizeAsync("get_open_orders", false, It.IsAny<CancellationToken>()), Times.Once);
    }
}
