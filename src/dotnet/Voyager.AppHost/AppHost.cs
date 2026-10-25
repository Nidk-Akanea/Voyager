// Development orchestration (ADR-0011): starts everything Voyager needs on the developer machine
// and shows it in the Aspire dashboard. Nothing here is shipped to users.
// It must run in the Development environment (Properties/launchSettings.json): only then does it read
// back the database password saved in the user secrets, which the data volume was created with.
var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddPostgres("postgres")
    .WithImage("postgis/postgis", "18-3.6")
    // PostgreSQL 18 images keep their data under /var/lib/postgresql, not /var/lib/postgresql/data.
    .WithVolume("voyager-postgres-data", "/var/lib/postgresql")
    // One container reused across runs: faster starts, and never two servers on the same volume.
    .WithLifetime(ContainerLifetime.Persistent)
    .AddDatabase("voyager");

builder.AddProject<Projects.Voyager_GroupServer>("group-server")
    .WithHttpEndpoint(port: 5180)
    .WithReference(database)
    .WaitFor(database);

// Shared secret between the desktop app and the local service, generated anew on every run.
var localToken = builder.AddParameter(
    "local-token",
    new GenerateParameterDefault { MinLength = 43, Special = false },
    secret: true,
    persist: false);

// The TypeScript packages must be installed beforehand (pnpm install at the repository root).
var userInterface = builder.AddViteApp("ui", "../../ts/ui")
    .WithPnpm(install: false);

var localService = builder.AddProject<Projects.Voyager_LocalService>("local-service")
    .WithHttpEndpoint()
    .WithEnvironment("Voyager__LocalToken", localToken)
    // The user interface served by Vite calls the local service from its own origin.
    .WithEnvironment("Voyager__AllowedOrigins__0", userInterface.GetEndpoint("http"));

// The Electron window shows the Vite user interface and talks to the local service started above,
// instead of starting its own.
builder.AddJavaScriptApp("desktop", "../../ts/desktop", "dev")
    .WithPnpm(install: false)
    .WithEnvironment("VOYAGER_UI_URL", userInterface.GetEndpoint("http"))
    .WithEnvironment("VOYAGER_LOCAL_SERVICE_URL", localService.GetEndpoint("http"))
    .WithEnvironment("VOYAGER_LOCAL_TOKEN", localToken)
    .WaitFor(userInterface)
    .WaitFor(localService);

await builder.Build().RunAsync();
