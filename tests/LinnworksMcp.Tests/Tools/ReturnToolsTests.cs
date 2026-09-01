using LinnworksMcp.Application.Returns;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Mcp.Tools;
using LinnworksMcp.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LinnworksMcp.Tests.Tools;

public class ReturnToolsTests
{
    private readonly Mock<IToolAuthorizer> _authorizerMock = new();
    private readonly ToolMetrics _metrics = TestToolMetrics.Create();
    private readonly Mock<ILinnworksClient> _clientMock = new();

    public ReturnToolsTests()
    {
        _authorizerMock
            .Setup(a => a.AuthorizeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task GetReturnsAsync_InvokesServiceAndReturnsJson()
    {
        _clientMock
            .Setup(c => c.PostAsync<SearchReturnsRequest, SearchReturnsResponse>(
                ReturnService.SearchReturnsPath, It.IsAny<SearchReturnsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchReturnsResponse(
                Returns: [
                    new ReturnResponse("RMA-500", "ORD-123", "Defective Item", "BOOKED", DateTimeOffset.UtcNow)
                ],
                TotalCount: 1));

        var returnService = new ReturnService(_clientMock.Object);
        var tools = new ReturnTools(returnService, _authorizerMock.Object, _metrics, NullLogger<ReturnTools>.Instance);

        var resultJson = await tools.GetReturnsAsync("BOOKED", 1, 10, CancellationToken.None);

        Assert.Contains("RMA-500", resultJson);
        Assert.Contains("Defective Item", resultJson);
    }
}
