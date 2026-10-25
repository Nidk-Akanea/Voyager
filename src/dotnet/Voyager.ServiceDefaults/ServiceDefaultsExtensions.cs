using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Voyager.ServiceDefaults;

/// <summary>
/// Technical defaults shared by the Voyager hosts (ADR-0011): telemetry, health checks, resilience of
/// outgoing HTTP calls, service discovery, and listening addresses.
/// </summary>
public static class ServiceDefaultsExtensions
{
    private const string HEALTH_ENDPOINT_PATH = "/health";
    private const string ALIVENESS_ENDPOINT_PATH = "/alive";
    private const string LIVENESS_TAG = "live";
    private const string OTLP_ENDPOINT_SETTING = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string KESTREL_ENDPOINTS_SECTION = "Kestrel:Endpoints";

    // ASPNETCORE_URLS, ASPNETCORE_HTTP_PORTS, ASPNETCORE_HTTPS_PORTS, DOTNET_URLS and --urls land here.
    private static readonly string[] _listeningAddressSettings = ["urls", "http_ports", "https_ports"];

    /// <summary>
    /// Adds dependency injection validation, telemetry, health checks, service discovery and
    /// resilient HTTP clients to a host.
    /// </summary>
    /// <param name="builder">The host builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // A misconfigured container must fail at startup, not at the first request (ADR-0006).
        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        }));

        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Retrying a POST could book or bill something twice (paid APIs, synchronisation):
            // only safe methods are retried. Long-lived gRPC streams will need their own pipeline.
            http.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Exposes the health endpoints, in development only: in production they would tell anyone about
    /// the state of the service.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same application, for chaining.</returns>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HEALTH_ENDPOINT_PATH);
            app.MapHealthChecks(ALIVENESS_ENDPOINT_PATH, new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains(LIVENESS_TAG),
            });
        }

        return app;
    }

    /// <summary>
    /// Listens on <paramref name="defaultUrls"/> unless the environment already decided where to
    /// listen (Aspire, Docker, Electron, ASPNETCORE_URLS or ASPNETCORE_HTTP_PORTS).
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="defaultUrls">The addresses to use when nothing else is configured.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static WebApplicationBuilder UseDefaultUrlsWhenNotConfigured(this WebApplicationBuilder builder, string defaultUrls)
    {
        // Each setting is checked on its own: an empty value (an unset variable in a compose file) must
        // not hide another one that is set.
        var alreadyConfigured = _listeningAddressSettings
                .Any(setting => !string.IsNullOrWhiteSpace(builder.Configuration[setting]))
            || builder.Configuration.GetSection(KESTREL_ENDPOINTS_SECTION).Exists();

        if (!alreadyConfigured)
        {
            builder.WebHost.UseUrls(defaultUrls);
        }

        return builder;
    }

    private static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddAspNetCoreInstrumentation(options =>
                    // Health probes run every few seconds: tracing them would drown the useful traces.
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments(HEALTH_ENDPOINT_PATH)
                        && !context.Request.Path.StartsWithSegments(ALIVENESS_ENDPOINT_PATH))
                .AddHttpClientInstrumentation());

        // Aspire provides the OTLP endpoint in development. Exporting telemetry anywhere else is a
        // decision of its own (where it goes, how long it is kept): until it is taken, nothing leaves
        // the machine outside development (ADR-0011).
        if (builder.Environment.IsDevelopment()
            && !string.IsNullOrWhiteSpace(builder.Configuration[OTLP_ENDPOINT_SETTING]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    private static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LIVENESS_TAG]);

        return builder;
    }
}
