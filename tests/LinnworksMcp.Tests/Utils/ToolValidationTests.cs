using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Utils;

namespace LinnworksMcp.Tests.Utils;

public class ToolValidationTests
{
    [Fact]
    public void Paging_ReturnsValidParameters_WhenWithinBounds()
    {
        var (page, size) = ToolValidation.Paging(1, 50);

        Assert.Equal(1, page);
        Assert.Equal(50, size);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    public void Paging_ThrowsLinnworksApiException_WhenPageNumberIsInvalid(int invalidPage, int size)
    {
        var ex = Assert.Throws<LinnworksApiException>(
            () => ToolValidation.Paging(invalidPage, size));

        Assert.Equal(LinnworksErrorKind.Validation, ex.Kind);
        Assert.Contains("pageNumber", ex.SafeMessage);
    }

    [Theory]
    [InlineData(1, 201)]
    [InlineData(1, 500)]
    public void Paging_ThrowsLinnworksApiException_WhenPageSizeExceedsMaximum(int page, int invalidSize)
    {
        var ex = Assert.Throws<LinnworksApiException>(
            () => ToolValidation.Paging(page, invalidSize));

        Assert.Equal(LinnworksErrorKind.Validation, ex.Kind);
        Assert.Contains("pageSize", ex.SafeMessage);
    }

    [Fact]
    public void RequiredGuid_ReturnsValidGuid_WhenStringIsValid()
    {
        var validGuid = Guid.NewGuid().ToString();
        var result = ToolValidation.RequiredGuid("testParam", validGuid);

        Assert.Equal(validGuid, result);
    }

    [Fact]
    public void RequiredGuid_ThrowsLinnworksApiException_WhenStringIsInvalid()
    {
        var ex = Assert.Throws<LinnworksApiException>(
            () => ToolValidation.RequiredGuid("testParam", "not-a-guid"));

        Assert.Equal(LinnworksErrorKind.Validation, ex.Kind);
        Assert.Contains("testParam", ex.SafeMessage);
    }

    [Fact]
    public void RequiredText_ThrowsLinnworksApiException_WhenNullOrWhitespace()
    {
        var ex = Assert.Throws<LinnworksApiException>(
            () => ToolValidation.RequiredText("name", "   "));

        Assert.Equal(LinnworksErrorKind.Validation, ex.Kind);
    }
}
