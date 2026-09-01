using LinnworksMcp.Application.Listings;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Mcp.Tools;
using LinnworksMcp.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LinnworksMcp.Tests.Tools;

public class ListingToolsTests
{
    private readonly Mock<IToolAuthorizer> _authorizerMock = new();
    private readonly ToolMetrics _metrics = TestToolMetrics.Create();
    private readonly Mock<ILinnworksClient> _clientMock = new();

    public ListingToolsTests()
    {
        _authorizerMock
            .Setup(a => a.AuthorizeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task GetListingsAsync_InvokesServiceAndReturnsJson()
    {
        _clientMock
            .Setup(c => c.PostAsync<GetListingsRequest, GetListingsResponse>(
                ListingService.GetListingsPath, It.IsAny<GetListingsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetListingsResponse(
                Listings: [
                    new ListingResponse("L1", "ITEM-1", "CH-SKU-1", "EBAY", "EBAY_UK", "Sample Item", 29.99m, "SUBMITTED")
                ],
                TotalCount: 1));

        var listingService = new ListingService(_clientMock.Object);
        var tools = new ListingTools(listingService, _authorizerMock.Object, _metrics, NullLogger<ListingTools>.Instance);

        var resultJson = await tools.GetListingsAsync("CH-SKU-1", 1, 10, CancellationToken.None);

        Assert.Contains("CH-SKU-1", resultJson);
        Assert.Contains("EBAY_UK", resultJson);
    }
}
