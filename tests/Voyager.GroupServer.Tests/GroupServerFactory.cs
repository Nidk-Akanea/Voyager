using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using Voyager.GroupServer.Api;

namespace Voyager.GroupServer.Tests;

/// <summary>
/// Starts the group server in memory, without a database: none of these tests needs PostgreSQL.
/// </summary>
/// <param name="environment">The hosting environment, or null for Development.</param>
/// <param name="developmentIdentity">Whether the server accepts calls on behalf of the development identity.</param>
/// <param name="shippedPluginsDirectory">The folder that holds the shipped plugins, or null for none.</param>
/// <param name="minimumAppVersion">The oldest version of the desktop app the server serves, or null for any.</param>
/// <param name="clock">The clock of the server, or null for the system clock.</param>
internal sealed class GroupServerFactory(
    string? environment = null,
    bool developmentIdentity = true,
    string? shippedPluginsDirectory = null,
    string? minimumAppVersion = null,
    IClock? clock = null)
    : WebApplicationFactory<Program>
{
    #region Public Methods

    /// <summary>The header that gives the version of the desktop app on a gRPC call.</summary>
    public static Metadata AppVersionHeader(string version) => new() { { AppVersionInterceptor.VERSION_HEADER, version } };

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
        builder.UseSetting("ConnectionStrings:voyager", string.Empty);
        builder.UseSetting("Seq:ServerUrl", string.Empty);
        builder.UseSetting("Voyager:LogDirectory", string.Empty);
        builder.UseSetting("Voyager:DevelopmentIdentity", developmentIdentity ? "true" : "false");
        builder.UseSetting("Voyager:ShippedPluginsDirectory", shippedPluginsDirectory ?? string.Empty);
        builder.UseSetting("Voyager:MinimumAppVersion", minimumAppVersion ?? string.Empty);

        if (clock is not null)
        {
            builder.ConfigureTestServices(services => services.AddSingleton(clock));
        }
    }

    #endregion Protected Methods
}
