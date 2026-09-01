using System.Net;
using LinnworksMcp.Application.Customers;
using LinnworksMcp.Application.Inventory;
using LinnworksMcp.Application.Listings;
using LinnworksMcp.Application.Locations;
using LinnworksMcp.Application.Orders;
using LinnworksMcp.Application.PurchaseOrders;
using LinnworksMcp.Application.Returns;
using LinnworksMcp.Application.Shipping;
using LinnworksMcp.Application.Stock;
using LinnworksMcp.Infrastructure.Linnworks;
using LinnworksMcp.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace LinnworksMcp.Infrastructure;

public static class ServiceCollectionExtensions
{
    public const string LinnworksHttpClient = "linnworks";
    public const string LinnworksAuthHttpClient = "linnworks-auth";

    /// <summary>
    /// Registers the Linnworks infrastructure, application services and observability.
    /// Credential resolution differs per transport and is registered separately by the caller.
    /// </summary>
    public static IServiceCollection AddLinnworks(this IServiceCollection services)
    {
        services.AddSingleton<ToolMetrics>();
        services.AddSingleton<EndpointRateLimiter>();
        services.AddSingleton(TimeProvider.System);

        // The auth manager MUST be a singleton — its session cache is the whole point, and a
        // per-instance cache would re-authenticate on every tool call. That rules out
        // AddHttpClient<TInterface,TImpl>, whose typed-client registration is transient, so the
        // HttpClient is registered by name and resolved through IHttpClientFactory instead.
        services.AddHttpClient(LinnworksAuthHttpClient, ConfigureTimeout)
            .AddResilienceHandler("linnworks-auth", BuildTransientPipeline);

        services.AddSingleton<ILinnworksAuthManager, LinnworksAuthManager>();

        services.AddHttpClient<ILinnworksClient, LinnworksClient>(LinnworksHttpClient, ConfigureTimeout)
            .AddResilienceHandler("linnworks-api", BuildTransientPipeline);

        services.AddScoped<InventoryService>();
        services.AddScoped<StockService>();
        services.AddScoped<OrderService>();
        services.AddScoped<LocationService>();
        services.AddScoped<ListingService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<ShippingService>();
        services.AddScoped<PurchaseOrderService>();
        services.AddScoped<ReturnService>();

        return services;
    }

    private static void ConfigureTimeout(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<LinnworksOptions>>().Value;

        // Per-call cancellation is separate and always wins if it fires first.
        client.Timeout = options.HttpTimeout;
        client.DefaultRequestHeaders.Accept.Add(new("application/json"));
    }

    /// <summary>
    /// Retries 429 and 5xx with exponential backoff plus jitter, honouring Retry-After when
    /// Linnworks supplies one. Capped so a sustained outage surfaces as an error rather than
    /// retrying forever.
    /// </summary>
    private static void BuildTransientPipeline(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        ResilienceHandlerContext context)
    {
        var options = context.ServiceProvider.GetRequiredService<
            Microsoft.Extensions.Options.IOptions<LinnworksOptions>>().Value;

        var metrics = context.ServiceProvider.GetRequiredService<ToolMetrics>();
        var logger = context.ServiceProvider.GetRequiredService<
            Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("LinnworksMcp.Resilience");

        builder.AddRetry(new HttpRetryStrategyOptions
        {
            // MaxRetryAttempts excludes the initial attempt.
            MaxRetryAttempts = Math.Max(0, options.MaxRetryAttempts - 1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(500),
            ShouldHandle = static args => ValueTask.FromResult(
                args.Outcome.Exception is HttpRequestException or TimeoutException
                || args.Outcome.Result is { } response
                   && (response.StatusCode == HttpStatusCode.TooManyRequests
                       || (int)response.StatusCode >= 500)),

            // Respect an explicit Retry-After over our own backoff curve.
            DelayGenerator = static args =>
            {
                var retryAfter = args.Outcome.Result?.Headers.RetryAfter;
                if (retryAfter?.Delta is { } delta)
                {
                    return ValueTask.FromResult<TimeSpan?>(delta);
                }

                if (retryAfter?.Date is { } date)
                {
                    var wait = date - DateTimeOffset.UtcNow;
                    if (wait > TimeSpan.Zero)
                    {
                        return ValueTask.FromResult<TimeSpan?>(wait);
                    }
                }

                // null lets the configured exponential-with-jitter delay apply.
                return ValueTask.FromResult<TimeSpan?>(null);
            },

            OnRetry = args =>
            {
                var path = args.Outcome.Result?.RequestMessage?.RequestUri?.AbsolutePath ?? "unknown";
                var status = args.Outcome.Result?.StatusCode;

                metrics.RecordRetry(path);
                if (status == HttpStatusCode.TooManyRequests)
                {
                    metrics.RecordThrottled(path);
                }

                logger.LogWarning(
                    "Retrying Linnworks {Path} (attempt {Attempt}) after {Status}; waiting {DelayMs}ms "
                    + "[correlation id: {CorrelationId}]",
                    path,
                    args.AttemptNumber + 1,
                    status is null ? "transport failure" : ((int)status).ToString(),
                    args.RetryDelay.TotalMilliseconds,
                    CorrelationId.Value);

                return ValueTask.CompletedTask;
            }
        });
    }
}
