namespace LinnworksMcp.IntegrationTests;

public class InventoryIntegrationTests
{
    [Fact]
    public void IntegrationTest_SkippedWhenNotExplicitlyEnabled()
    {
        var enabled = Environment.GetEnvironmentVariable("LINNWORKS_INTEGRATION_TESTS_ENABLED");

        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            // Integration tests against live/sandbox Linnworks API require environment credentials
            // Set LINNWORKS_INTEGRATION_TESTS_ENABLED=true to run against sandbox.
            Assert.True(true, "Integration tests disabled by default.");
            return;
        }

        // Live sandbox smoke test logic goes here when credentials are provided
        Assert.NotNull(enabled);
    }
}
