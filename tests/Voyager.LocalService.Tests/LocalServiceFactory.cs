using Grpc.Core;
using Grpc.Net.Client;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using Voyager.LocalService.Api;

namespace Voyager.LocalService.Tests;

/// <summary>
/// Starts the local service in memory, on a SQLite file of its own that is deleted afterwards.
/// </summary>
internal sealed class LocalServiceFactory : WebApplicationFactory<Program>
{
    /// <summary>A token long enough for the validation of the local service settings.</summary>
    public const string LOCAL_TOKEN = "test-local-token-0123456789abcdef-0123456789";

    private readonly string? _localToken;
    private readonly IClock? _clock;
    private readonly string? _seqServerUrl;
    private readonly string? _environment;

    /// <param name="localToken">The token given to the service, or null to start it without one.</param>
    /// <param name="databasePath">The SQLite file, or null for a new temporary file.</param>
    /// <param name="clock">The clock of the service, or null for the system clock.</param>
    /// <param name="seqServerUrl">The Seq server to write logs to, or null for none.</param>
    /// <param name="environment">The hosting environment, or null for Development.</param>
    public LocalServiceFactory(
        string? localToken = LOCAL_TOKEN,
        string? databasePath = null,
        IClock? clock = null,
        string? seqServerUrl = null,
        string? environment = null)
    {
        _environment = environment;
        _localToken = localToken;
        _clock = clock;
        _seqServerUrl = seqServerUrl;
        DatabasePath = databasePath ?? Path.Combine(Path.GetTempPath(), $"voyager-tests-{Guid.NewGuid():N}.db");
    }

    /// <summary>Path of the SQLite file used by this instance of the service.</summary>
    public string DatabasePath { get; }

    /// <summary>Creates a gRPC channel that talks to the in-memory service.</summary>
    public GrpcChannel CreateChannel() =>
        GrpcChannel.ForAddress(Server.BaseAddress, new GrpcChannelOptions { HttpHandler = Server.CreateHandler() });

    /// <summary>The header that carries a local token on a gRPC call.</summary>
    public static Metadata TokenHeader(string token) => new() { { LocalTokenInterceptor.TOKEN_HEADER, token } };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_environment is not null)
        {
            builder.UseEnvironment(_environment);
        }

        builder.UseSetting("Voyager:DatabasePath", DatabasePath);
        // Tests never write to a Seq server, even one set in the machine's environment.
        builder.UseSetting("Seq:ServerUrl", _seqServerUrl ?? string.Empty);
        if (_localToken is not null)
        {
            builder.UseSetting("Voyager:LocalToken", _localToken);
        }

        if (_clock is not null)
        {
            builder.ConfigureTestServices(services => services.AddSingleton(_clock));
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // SQLite keeps pooled connections open, which would lock the file on Windows.
        SqliteConnection.ClearAllPools();
        if (File.Exists(DatabasePath))
        {
            File.Delete(DatabasePath);
        }
    }
}
