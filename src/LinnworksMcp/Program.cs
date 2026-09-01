using LinnworksMcp.Infrastructure;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Mcp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModelContextProtocol.AspNetCore;

// Transport is chosen at startup: stdio for local tooling (MCP Inspector, a desktop client
// launching this as a subprocess), Streamable HTTP for remote chatbots. The two cannot share a
// process — stdio owns stdout as its JSON-RPC channel.
var useStdio = args.Contains("--stdio", StringComparer.OrdinalIgnoreCase)
    || string.Equals(
        Environment.GetEnvironmentVariable("LINNWORKS_MCP_TRANSPORT"),
        "stdio",
        StringComparison.OrdinalIgnoreCase);

return useStdio
    ? await RunStdioAsync(args).ConfigureAwait(false)
    : await RunHttpAsync(args).ConfigureAwait(false);

static async Task<int> RunStdioAsync(string[] args)
{
    var builder = Host.CreateApplicationBuilder(args);

    // Anything written to stdout corrupts the JSON-RPC stream, so logs go to stderr.
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

    builder.Services.Configure<LinnworksOptions>(
        builder.Configuration.GetSection(LinnworksOptions.SectionName));
    builder.Services.Configure<McpAuthOptions>(
        builder.Configuration.GetSection(McpAuthOptions.SectionName));

    builder.Services.AddLinnworks();

    // No HTTP request means no credential headers: stdio is single-tenant, from configuration.
    builder.Services.AddSingleton<ILinnworksCredentialProvider, ConfiguredLinnworksCredentialProvider>();
    builder.Services.AddSingleton<IToolAuthorizer, StdioToolAuthorizer>();

    builder.Services.AddLinnworksMcpServer().WithStdioServerTransport();

    await builder.Build().RunAsync().ConfigureAwait(false);
    return 0;
}

static async Task<int> RunHttpAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.Configure<LinnworksOptions>(
        builder.Configuration.GetSection(LinnworksOptions.SectionName));
    builder.Services.Configure<McpAuthOptions>(
        builder.Configuration.GetSection(McpAuthOptions.SectionName));

    builder.Services.AddLinnworks();
    builder.Services.AddHttpContextAccessor();

    // Credentials arrive per request. See HeaderLinnworksCredentialProvider for why this is
    // headers rather than MCP session state.
    builder.Services.AddScoped<ILinnworksCredentialProvider, HeaderLinnworksCredentialProvider>();
    builder.Services.AddScoped<IToolAuthorizer, ToolAuthorizer>();

    builder.Services
        .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationHandler.SchemeName, _ => { });

    builder.Services
        .AddAuthorizationBuilder()
        .AddPolicy("McpClient", policy => policy
            .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
            .RequireAuthenticatedUser());

    builder.Services.AddHealthChecks()
        .AddCheck<LinnworksReadinessCheck>(
            "linnworks", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);

    builder.Services.AddLinnworksMcpServer().WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    });

    var app = builder.Build();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
        app.UseHsts();
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();

    // Liveness check
    app.MapHealthChecks("/health", new() { Predicate = _ => false }).AllowAnonymous();

    // Readiness check
    app.MapHealthChecks("/ready", new() { Predicate = check => check.Tags.Contains("ready") })
        .AllowAnonymous();

    // Map MCP endpoint for Streamable HTTP discovery
    app.MapMcp("/mcp");

    await app.RunAsync().ConfigureAwait(false);
    return 0;
}
