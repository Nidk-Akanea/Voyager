// Local service host: runs next to the Electron app on each member's machine (ADR-0003).
// Unless Aspire or Electron decide otherwise, it listens on the loopback interface only, on a port
// chosen at startup: nothing outside this machine can reach it, and it never collides with another
// program.
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

using NodaTime;

using Voyager.LocalService;
using Voyager.LocalService.Api;
using Voyager.Persistence.Sqlite;
using Voyager.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.UseDefaultUrlsWhenNotConfigured("http://127.0.0.1:0");

// Without a valid local token, the service refuses to start rather than serve anyone (ADR-0003).
builder.Services.AddOptions<LocalServiceOptions>()
    .BindConfiguration(LocalServiceOptions.SECTION)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IClock>(SystemClock.Instance);
builder.Services.AddSingleton<ServiceInfo>();

builder.Services.AddSingleton<LocalDatabaseState>();
builder.Services.AddVoyagerSqlite(provider =>
    provider.GetRequiredService<IOptions<LocalServiceOptions>>().Value.ResolvedDatabasePath);

builder.Services.AddGrpc(options => options.Interceptors.Add<LocalTokenInterceptor>());

// The user interface calls the service from a browser context (gRPC-Web): only its own origins may.
builder.Services.AddCors();
builder.Services.AddOptions<CorsOptions>()
    .Configure<IOptions<LocalServiceOptions>>((cors, local) => cors.AddDefaultPolicy(policy => policy
        .WithOrigins([LocalServiceOptions.PACKAGED_UI_ORIGIN, .. local.Value.AllowedOrigins])
        .WithMethods(HttpMethods.Post)
        .AllowAnyHeader()
        // Without them, the browser hides the gRPC status of a call from the client, even a failed one.
        .WithExposedHeaders("grpc-status", "grpc-message", "grpc-status-details-bin", "grpc-encoding", "grpc-accept-encoding")));

// Disposed even when startup fails, so that the last logs still reach their destination.
await using var app = builder.Build();

try
{
    // Invalid settings stop the service here, before anything else reads them and hides the cause.
    _ = app.Services.GetRequiredService<IOptions<LocalServiceOptions>>().Value;

    // Created now so that its start time is the real start of the service.
    app.Services.GetRequiredService<ServiceInfo>();

    await app.PrepareLocalDatabaseAsync();
    app.LinkToDesktopApp();

    app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });
    app.UseCors();

    app.MapGrpcService<DiagnosticsGrpcService>();
    app.MapDefaultEndpoints();

    await app.RunAsync();
}
// Tools that only build the host (WebApplicationFactory, EF Core) stop it on purpose: not a failure.
catch (Exception exception) when (exception is not HostAbortedException)
{
    // Written before the host is disposed, so that the cause of the failure reaches every log output.
    LocalServiceLog.ServiceFailed(app.Logger, exception);
    throw;
}
