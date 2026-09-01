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

    public static IServiceCollection AddLinnworks(this IServiceCollection services)
    {
        services.AddSingleton<ToolMetrics>();
        services.AddSingleton<EndpointRateLimiter>();
        services.AddSingleton(TimeProvider.System);

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

        client.Timeout = options.HttpTimeout;
        client.DefaultRequestHeaders.Accept.Add(new("application/json"));
    }

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
            MaxRetryAttempts = Math.Max(0, options.MaxRetryAttempts - 1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromMilliseconds(500),
            ShouldHandle = static args => ValueTask.FromResult(
                args.Outcome.Exception is HttpRequestException or TimeoutException
                || args.Outcome.Result is { } response
                   && (response.StatusCode == HttpStatusCode.TooManyRequests
                       || (int)response.StatusCode >= 500)),

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
                    "Retrying Linnworks {Path} (attempt {Attempt}) after {Status}; waiting {DelayMs}ms [correlation id: {CorrelationId}]",
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

