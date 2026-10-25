using System.Globalization;

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

using Serilog;
using Serilog.Debugging;
using Serilog.Extensions.Logging;

namespace Voyager.ServiceDefaults;

/// <summary>
/// Technical defaults shared by the Voyager hosts (ADR-0011): telemetry, logs sent to Seq in
/// development, health checks, resilience of outgoing HTTP calls, service discovery, and listening
/// addresses.
/// </summary>
public static class ServiceDefaultsExtensions
{
    #region Fields

    private const string ALIVENESS_ENDPOINT_PATH = "/alive";
    private const string APPLICATION_PROPERTY = "Application";
    private const string HEALTH_ENDPOINT_PATH = "/health";
    private const string KESTREL_ENDPOINTS_SECTION = "Kestrel:Endpoints";
    private const string LIVENESS_TAG = "live";
    private const string OTLP_ENDPOINT_SETTING = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string SEQ_API_KEY_SETTING = "Seq:ApiKey";

    // Logs kept in memory while Seq does not answer; older ones are dropped beyond that.
    private const int SEQ_QUEUE_SIZE_LIMIT = 10_000;

    private const string SEQ_SERVER_URL_SETTING = "Seq:ServerUrl";

    // ASPNETCORE_URLS, ASPNETCORE_HTTP_PORTS, ASPNETCORE_HTTPS_PORTS, DOTNET_URLS and --urls land here.
    private static readonly string[] _listeningAddressSettings = ["urls", "http_ports", "https_ports"];

    // A Seq server that does not answer must not hold up a send, nor the shutdown, for long.
    private static readonly TimeSpan _seqConnectTimeout = TimeSpan.FromSeconds(5);

    #endregion Fields

    #region Private Methods

    private static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
            where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LIVENESS_TAG]);

        return builder;
    }

    /// <summary>
    /// Also writes the logs to <paramref name="seqServer"/> with Serilog (ADR-0012), with Seq:ApiKey if
    /// the server asks for one. Serilog is added next to the other log outputs (console, OpenTelemetry),
    /// which keep working, and the usual Logging:LogLevel filters apply to it too.
    /// </summary>
    private static TBuilder AddSeqLogging<TBuilder>(this TBuilder builder, Uri? seqServer)
        where TBuilder : IHostApplicationBuilder
    {
        if (seqServer is null)
        {
            return builder;
        }

        // Without this, a stopped server or a wrong API key would go unnoticed: Serilog reports its own
        // failures on the error output, which the Aspire dashboard shows.
        SelfLog.Enable(Console.Error);

        var apiKey = builder.Configuration[SEQ_API_KEY_SETTING];
        var applicationName = builder.Environment.ApplicationName;

        // Registered as a plain provider, not with AddSerilog(): that one adds a filter rule of its own
        // which lets every level through, whatever Logging:LogLevel says. Built by the container, which
        // also disposes of it, sending the last logs, when the host stops.
        builder.Services.AddSingleton<ILoggerProvider>(_ => new SerilogLoggerProvider(
            new LoggerConfiguration()
                // Every level reaches Serilog: Microsoft.Extensions.Logging has already filtered them.
                .MinimumLevel.Verbose()
                .Enrich.WithProperty(APPLICATION_PROPERTY, applicationName)
                // Invariant culture: the same message text whatever the language of the machine.
                .WriteTo.Seq(
                    seqServer.ToString(),
                    apiKey: string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
                    queueSizeLimit: SEQ_QUEUE_SIZE_LIMIT,
                    messageHandler: new SocketsHttpHandler { ConnectTimeout = _seqConnectTimeout },
                    formatProvider: CultureInfo.InvariantCulture)
                .CreateLogger(),
            dispose: true));

        return builder;
    }

    private static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder, Uri? seqServer)
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
                .AddHttpClientInstrumentation(options =>
                    // Every batch of logs sent to Seq would otherwise show up as a trace of its own.
                    options.FilterHttpRequestMessage = request => !IsSentTo(request, seqServer)));

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

    /// <summary>Whether an outgoing HTTP request goes to <paramref name="server"/>.</summary>
    private static bool IsSentTo(HttpRequestMessage request, Uri? server) =>
        server is not null
        && request.RequestUri is { } target
        && Uri.Compare(target, server, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;

    /// <summary>
    /// The Seq server named by Seq:ServerUrl, in development only; null when there is none.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Seq:ServerUrl is not an http or https address, or is a plain http address of another machine.
    /// </exception>
    private static Uri? SeqServerInDevelopment<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // Like the OTLP export: sending logs off the process outside development is a decision of its own
        // (where they go, how long they are kept), not taken yet (ADR-0011, ADR-0012).
        var serverUrl = builder.Configuration[SEQ_SERVER_URL_SETTING];
        if (!builder.Environment.IsDevelopment() || string.IsNullOrWhiteSpace(serverUrl))
        {
            return null;
        }

        // A mistyped address ("localhost:5341") would otherwise fail on every send, without a word.
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var server)
            || (server.Scheme != Uri.UriSchemeHttp && server.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"{SEQ_SERVER_URL_SETTING} must be an http or https address, such as http://localhost:5341.");
        }

        // Logs and API key never travel unencrypted over the network (ADR-0012).
        if (server.Scheme == Uri.UriSchemeHttp && !server.IsLoopback)
        {
            throw new InvalidOperationException(
                $"{SEQ_SERVER_URL_SETTING} must use https unless Seq runs on this machine.");
        }

        return server;
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Adds dependency injection validation, telemetry, Seq logs, health checks, service discovery
    /// and resilient HTTP clients to a host.
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

        var seqServer = builder.SeqServerInDevelopment();
        builder.ConfigureOpenTelemetry(seqServer);
        builder.AddSeqLogging(seqServer);
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

    #endregion Public Methods
}
