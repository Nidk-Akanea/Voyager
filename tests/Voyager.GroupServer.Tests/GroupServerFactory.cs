using System.Net;

using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NodaTime;

using Voyager.Contracts.Diagnostics.V1;
using Voyager.Contracts.PluginHost.V1;
using Voyager.GroupServer.Api;

namespace Voyager.GroupServer.Tests;

/// <summary>
/// Starts the group server in memory, without a database unless a test gives one.
/// </summary>
/// <param name="environment">The hosting environment, or null for Development.</param>
/// <param name="developmentIdentity">Whether the server accepts calls on behalf of the development identity.</param>
/// <param name="shippedPluginsDirectory">The folder that holds the shipped plugins, or null for none.</param>
/// <param name="minimumAppVersion">The oldest version of the desktop app the server serves, or null for any.</param>
/// <param name="clock">The clock of the server, or null for the system clock.</param>
/// <param name="connectionString">The connection string of the database, or null for none.</param>
/// <param name="remoteAddress">The address every call seems to come from, or null for a call from inside the process.</param>
/// <param name="seqServerUrl">The Seq server to write logs to, or null for none.</param>
/// <param name="logDirectory">The folder for log files, or null for none.</param>
internal sealed class GroupServerFactory(
    string? environment = null,
    bool developmentIdentity = true,
    string? shippedPluginsDirectory = null,
    string? minimumAppVersion = null,
    IClock? clock = null,
    string? connectionString = null,
    IPAddress? remoteAddress = null,
    string? seqServerUrl = null,
    string? logDirectory = null)
    : WebApplicationFactory<Program>
{
    #region Fields

    private readonly StartupFailureLog _failureLog = new();

    #endregion Fields

    #region Public Methods

    /// <summary>
    /// Starts the server, which must refuse to start, and returns why: the exception the server logged before stopping, or
    /// else the one the host threw (see <see cref="StartupFailureLog"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">The server started.</exception>
    public Exception StartupFailure()
    {
        try
        {
            _ = Server;
        }
        catch (Exception thrown)
        {
            return _failureLog.Failure ?? thrown;
        }

        throw new InvalidOperationException("The server started, though it was expected to refuse.");
    }

    /// <summary>The headers in which the desktop app names itself and gives its version on a gRPC call.</summary>
    public static Metadata AppVersionHeader(string version) => new()
    {
        { AppVersionInterceptor.APP_HEADER, "desktop" },
        { AppVersionInterceptor.VERSION_HEADER, version },
    };

    /// <summary>Creates a gRPC channel that talks to the in-memory server.</summary>
    public GrpcChannel CreateChannel() =>
        GrpcChannel.ForAddress(Server.BaseAddress, new GrpcChannelOptions { HttpHandler = Server.CreateHandler() });

    /// <summary>Creates a client of the plugin host service of the in-memory server.</summary>
    public PluginHostService.PluginHostServiceClient CreatePluginHostClient() => new(CreateChannel());

    /// <summary>Asks the in-memory server for its "About" information, the call most tests make.</summary>
    /// <param name="headers">The headers of the call, such as <see cref="AppVersionHeader"/>, or null for none.</param>
    public async Task<GetServiceInfoResponse> GetServiceInfoAsync(Metadata? headers = null) =>
        await new DiagnosticsService.DiagnosticsServiceClient(CreateChannel()).GetServiceInfoAsync(new GetServiceInfoRequest(), headers);

    #endregion Public Methods

    #region Protected Methods

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (environment is not null)
        {
            builder.UseEnvironment(environment);
        }

        // Tests never use the machine's own settings, even ones set in its environment.
        builder.UseSetting("ConnectionStrings:voyager", connectionString ?? string.Empty);
        builder.UseSetting("Seq:ServerUrl", seqServerUrl ?? string.Empty);
        builder.UseSetting("Voyager:LogDirectory", logDirectory ?? string.Empty);
        builder.UseSetting("Voyager:DevelopmentIdentity", developmentIdentity ? "true" : "false");
        builder.UseSetting("Voyager:ShippedPluginsDirectory", shippedPluginsDirectory ?? string.Empty);
        builder.UseSetting("Voyager:MinimumAppVersion", minimumAppVersion ?? string.Empty);

        builder.ConfigureLogging(logging => logging.AddProvider(_failureLog));

        builder.ConfigureTestServices(services =>
        {
            if (clock is not null)
            {
                services.AddSingleton(clock);
            }

            if (remoteAddress is not null)
            {
                services.AddSingleton<IStartupFilter>(new RemoteAddressFilter(remoteAddress));
            }
        });
    }

    #endregion Protected Methods

    #region Classes

    // Makes every call seem to come from another machine: the in-memory server gives no remote address.
    private sealed class RemoteAddressFilter(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = address;
                return nextMiddleware(context);
            });
            next(app);
        };
    }

    #endregion Classes
}
