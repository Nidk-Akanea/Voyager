// Group server host: shared by the groups it hosts, run online or under Docker (ADR-0003, ADR-0010). It holds the
// data, runs the core and the plugins, and serves the desktop apps (ADR-0018).
using Microsoft.Extensions.Options;

using NodaTime;

using Voyager.GroupServer;
using Voyager.GroupServer.Api;
using Voyager.GroupServer.Authentication;
using Voyager.Persistence.PostgreSql;
using Voyager.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.UseDefaultUrlsWhenNotConfigured("http://localhost:5180");

// The development settings stop the server outside Development, rather than open it to anyone (ADR-0019, decision 9).
builder.Services.AddOptions<GroupServerOptions>()
    .BindConfiguration(GroupServerOptions.SECTION)
    .Validate<IHostEnvironment>(
        (options, environment) => environment.IsDevelopment() || !options.DevelopmentIdentity,
        "Voyager:DevelopmentIdentity is only allowed in the Development environment.")
    .Validate<IHostEnvironment>(
        (options, environment) => environment.IsDevelopment() || string.IsNullOrWhiteSpace(options.ShippedPluginsDirectory),
        "Voyager:ShippedPluginsDirectory is only allowed in the Development environment: elsewhere the plugins are next to the server.")
    // A relative folder would depend on the working directory: Aspire always gives a full path.
    .Validate(
        options => string.IsNullOrWhiteSpace(options.ShippedPluginsDirectory) || Path.IsPathFullyQualified(options.ShippedPluginsDirectory),
        "Voyager:ShippedPluginsDirectory must be a full path.")
    // A minimum the server cannot read would silently serve every app: it stops the server instead.
    .Validate(
        options => string.IsNullOrWhiteSpace(options.MinimumAppVersion) || AppVersion.ParseCore(options.MinimumAppVersion) is not null,
        "Voyager:MinimumAppVersion must be a version in the form x.y.z, such as 0.4.0.")
    .ValidateOnStart();

builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton<ServerInfo>();
builder.Services.AddSingleton<DatabaseHealth>();

// Aspire gives the connection string of the "voyager" database; without one, the database is reported unavailable.
var connectionString = builder.Configuration.GetConnectionString("voyager");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddVoyagerPostgreSql(connectionString);
}

builder.Services.AddServerPlugins(builder.Environment);
builder.AddGroupServerAuthentication();

// The desktop app calls the server through its main process (ADR-0019, decision 4): gRPC-Web, without CORS.
builder.Services.AddGrpc(options => options.Interceptors.Add<AppVersionInterceptor>());

// Disposed even when startup fails, so that the last logs still reach their destination.
await using var app = builder.Build();

try
{
    // Invalid settings stop the server here, before anything else reads them and hides the cause.
    var options = app.Services.GetRequiredService<IOptions<GroupServerOptions>>().Value;
    if (options.DevelopmentIdentity)
    {
        GroupServerLog.DevelopmentIdentityOn(app.Logger);
    }

    // Created now so that its start time is the real start of the server.
    app.Services.GetRequiredService<ServerInfo>();

    app.LoadPlugins();

    app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapGrpcService<DiagnosticsGrpcService>().RequireAuthorization();
    app.MapGrpcService<PluginHostGrpcService>().RequireAuthorization();
    app.MapDefaultEndpoints();

    await app.RunAsync();
}
// Tools that only build the host (WebApplicationFactory, EF Core) stop it on purpose: not a failure.
catch (Exception exception) when (exception is not HostAbortedException)
{
    // Written before the host is disposed, so that the cause of the failure reaches every log output.
    GroupServerLog.ServerFailed(app.Logger, exception);
    throw;
}
