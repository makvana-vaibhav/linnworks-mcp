using LinnworksMcp.Application.Customers;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Mcp.Tools;
using LinnworksMcp.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LinnworksMcp.Tests.Tools;

public class CustomerToolsTests
{
    private readonly Mock<IToolAuthorizer> _authorizerMock = new();
    private readonly ToolMetrics _metrics = TestToolMetrics.Create();
    private readonly Mock<ILinnworksClient> _clientMock = new();

    public CustomerToolsTests()
    {
        _authorizerMock
            .Setup(a => a.AuthorizeAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task SearchCustomersAsync_InvokesServiceAndReturnsJson()
    {
        _clientMock
            .Setup(c => c.PostAsync<SearchCustomersRequest, SearchCustomersResponse>(
                CustomerService.SearchCustomersPath, It.IsAny<SearchCustomersRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchCustomersResponse(
                Customers: [
                    new CustomerResponse("CUST-1", "John Doe", "john@example.com", "123456789", "123 High St", "London", "SW1A 1AA", "UK")
                ],
                TotalCount: 1));

        var customerService = new CustomerService(_clientMock.Object);
        var tools = new CustomerTools(customerService, _authorizerMock.Object, _metrics, NullLogger<CustomerTools>.Instance);

        var resultJson = await tools.SearchCustomersAsync("John", 1, 10, CancellationToken.None);

        Assert.Contains("John Doe", resultJson);
        Assert.Contains("john@example.com", resultJson);
    }
}
