using LinnworksMcp.Application.Shipping;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Mcp.Tools;
using LinnworksMcp.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LinnworksMcp.Tests.Tools;

public class ShippingToolsTests
{
    private readonly Mock<IToolAuthorizer> _authorizerMock = new();
    private readonly ToolMetrics _metrics = TestToolMetrics.Create();
    private readonly Mock<ILinnworksClient> _clientMock = new();

    public ShippingToolsTests()
    {
        _authorizerMock
            .Setup(a => a.AuthorizeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task GetShippingServicesAsync_InvokesServiceAndReturnsJson()
    {
        _clientMock
            .Setup(c => c.GetAsync<List<PostalServiceResponse>>(
                ShippingService.GetPostalServicesPath, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PostalServiceResponse("P1", "Royal Mail Tracked 24", "Royal Mail", "RM24", true)
            ]);

        var shippingService = new ShippingService(_clientMock.Object);
        var tools = new ShippingTools(shippingService, _authorizerMock.Object, _metrics, NullLogger<ShippingTools>.Instance);

        var resultJson = await tools.GetShippingServicesAsync(CancellationToken.None);

        Assert.Contains("Royal Mail Tracked 24", resultJson);
        Assert.Contains("RM24", resultJson);
    }
}
