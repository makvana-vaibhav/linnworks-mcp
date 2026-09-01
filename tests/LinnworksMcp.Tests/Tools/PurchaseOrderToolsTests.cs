using LinnworksMcp.Application.PurchaseOrders;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Mcp.Tools;
using LinnworksMcp.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LinnworksMcp.Tests.Tools;

public class PurchaseOrderToolsTests
{
    private readonly Mock<IToolAuthorizer> _authorizerMock = new();
    private readonly ToolMetrics _metrics = TestToolMetrics.Create();
    private readonly Mock<ILinnworksClient> _clientMock = new();

    public PurchaseOrderToolsTests()
    {
        _authorizerMock
            .Setup(a => a.AuthorizeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task GetPurchaseOrdersAsync_InvokesServiceAndReturnsJson()
    {
        _clientMock
            .Setup(c => c.PostAsync<GetPurchaseOrdersRequest, GetPurchaseOrdersResponse>(
                PurchaseOrderService.GetPurchaseOrdersPath, It.IsAny<GetPurchaseOrdersRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetPurchaseOrdersResponse(
                PurchaseOrders: [
                    new PurchaseOrderResponse("PO-100", "SUP-1", "LOC-1", "REF-999", "OPEN", DateTimeOffset.UtcNow)
                ],
                TotalCount: 1));

        var poService = new PurchaseOrderService(_clientMock.Object);
        var tools = new PurchaseOrderTools(poService, _authorizerMock.Object, _metrics, NullLogger<PurchaseOrderTools>.Instance);

        var resultJson = await tools.GetPurchaseOrdersAsync("OPEN", 1, 10, CancellationToken.None);

        Assert.Contains("PO-100", resultJson);
        Assert.Contains("REF-999", resultJson);
    }
}
