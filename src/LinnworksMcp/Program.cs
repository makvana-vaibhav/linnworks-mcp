using LinnworksMcp.Infrastructure;
using LinnworksMcp.Infrastructure.Auth;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Infrastructure.Observability;
using LinnworksMcp.Mcp;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModelContextProtocol.AspNetCore;

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

    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

    builder.Services.Configure<LinnworksOptions>(
        builder.Configuration.GetSection(LinnworksOptions.SectionName));
    builder.Services.Configure<McpAuthOptions>(
        builder.Configuration.GetSection(McpAuthOptions.SectionName));
    builder.Services.Configure<McpAuthOptions>(
        builder.Configuration.GetSection("Mcp"));

    builder.Services.AddLinnworks();

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
    builder.Services.Configure<McpAuthOptions>(
        builder.Configuration.GetSection("Mcp"));

    builder.Services.AddLinnworks();
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddScoped<ILinnworksCredentialProvider, HeaderLinnworksCredentialProvider>();
    builder.Services.AddScoped<IToolAuthorizer, ToolAuthorizer>();

    builder.Services.AddHealthChecks()
        .AddCheck<LinnworksReadinessCheck>(
            "linnworks", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);

    builder.Services.AddLinnworksMcpServer().WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    });

    var app = builder.Build();

    {
        var auth = app.Services
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<McpAuthOptions>>().Value;
        var startupLog = app.Services
            .GetRequiredService<ILoggerFactory>().CreateLogger("LinnworksMcp.Startup");

        if (auth.GetAllValidKeys().Count > 0)
        {
            startupLog.LogInformation(
                "MCP endpoint requires a client API key. Anonymous discovery: {Discovery}.",
                auth.AllowAnonymousDiscovery ? "allowed" : "denied");
        }
        else if (auth.RequireApiKey)
        {
            startupLog.LogError(
                "MCP endpoint is CLOSED: no client API key is configured. Set McpAuth__ApiKey.");
        }
        else
        {
            startupLog.LogWarning(
                "MCP endpoint is UNAUTHENTICATED (McpAuth__RequireApiKey=false). Anyone who can reach it can invoke tools.");
        }
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
        app.UseHsts();
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<McpAccessMiddleware>();

    app.MapHealthChecks("/health", new() { Predicate = _ => false });
    app.MapHealthChecks("/ready", new() { Predicate = check => check.Tags.Contains("ready") });
    app.MapMcp("/mcp");

    await app.RunAsync().ConfigureAwait(false);
    return 0;
}

