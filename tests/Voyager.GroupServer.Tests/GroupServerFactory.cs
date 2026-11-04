using System.Net;

using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

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
internal sealed class GroupServerFactory(
    string? environment = null,
    bool developmentIdentity = true,
    string? shippedPluginsDirectory = null,
    string? minimumAppVersion = null,
    IClock? clock = null,
    string? connectionString = null,
    IPAddress? remoteAddress = null)
    : WebApplicationFactory<Program>
{
    #region Public Methods

    /// <summary>The headers in which the desktop app names itself and gives its version on a gRPC call.</summary>
    public static Metadata AppVersionHeader(string version) => new()
    {
        { AppVersionInterceptor.APP_HEADER, "desktop" },
        { AppVersionInterceptor.VERSION_HEADER, version },
    };

    /// <summary>Creates a gRPC channel that talks to the in-memory server.</summary>
    public GrpcChannel CreateChannel() =>
        GrpcChannel.ForAddress(Server.BaseAddress, new GrpcChannelOptions { HttpHandler = Server.CreateHandler() });

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
        builder.UseSetting("Seq:ServerUrl", string.Empty);
        builder.UseSetting("Voyager:LogDirectory", string.Empty);
        builder.UseSetting("Voyager:DevelopmentIdentity", developmentIdentity ? "true" : "false");
        builder.UseSetting("Voyager:ShippedPluginsDirectory", shippedPluginsDirectory ?? string.Empty);
        builder.UseSetting("Voyager:MinimumAppVersion", minimumAppVersion ?? string.Empty);

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
