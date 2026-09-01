using System.Diagnostics.Metrics;
using LinnworksMcp.Infrastructure.Observability;
using Moq;

namespace LinnworksMcp.Tests.TestHelpers;

public static class TestToolMetrics
{
    public static ToolMetrics Create()
    {
        var factoryMock = new Mock<IMeterFactory>();
        factoryMock
            .Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opt) => new Meter(opt));

        return new ToolMetrics(factoryMock.Object);
    }
}
