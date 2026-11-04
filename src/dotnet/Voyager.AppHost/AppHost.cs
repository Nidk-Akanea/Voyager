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

// Shared secret between the desktop app and the local service, generated anew on every run.
var localToken = builder.AddParameter(
    "local-token",
    new GenerateParameterDefault { MinLength = 43, Special = false },
    secret: true,
    persist: false);

// The plugins that ship with Voyager, built into artifacts/plugins before anything loads them (ADR-0015): the group
// server and the local service load them at startup, and the user interface served by Vite reads their modules from
// the same folder.
var repositoryRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", ".."));
var pluginsFolder = Path.Combine(repositoryRoot, "artifacts", "plugins");
// The plugins folder of the user's data in development (installed plugins, requests, state): under artifacts, never
// the one of the installed app, which the developer would otherwise share with it (ADR-0016).
var userPluginsFolder = Path.Combine(repositoryRoot, "artifacts", "user-plugins");
// A fixed version: development has no use for the one GitVersion computes, and skipping it saves seconds at every start.
// Debug, so that the code of a plugin can be debugged like the rest. Only the plugins that changed are built again
// (tools/build-plugins.ps1).
var plugins = builder.AddExecutable(
    "plugins",
    "pwsh",
    repositoryRoot,
    "-NoProfile",
    "-File",
    Path.Combine("tools", "build-plugins.ps1"),
    "-Version",
    "0.0.0-dev",
    "-Configuration",
    "Debug");

// The group server runs the plugins (ADR-0020) and serves the desktop app (ADR-0018). The development identity lets
// it accept calls without signing in, on behalf of a development account and group: Aspire alone sets it, and the
// server refuses it outside the Development environment (ADR-0019, decision 9).
builder.AddProject<Projects.Voyager_GroupServer>("group-server")
    .WithHttpEndpoint(port: 5180)
    .WithReference(database)
    .WithEnvironment("Voyager__DevelopmentIdentity", "true")
    .WithEnvironment("Voyager__ShippedPluginsDirectory", pluginsFolder)
    .WaitFor(database)
    .WaitForCompletion(plugins);

// The TypeScript packages must be installed beforehand (pnpm install at the repository root).
var userInterface = builder.AddViteApp("ui", "../../ts/ui")
    .WithPnpm(install: false)
    .WithEnvironment("VOYAGER_PLUGINS_DIRECTORY", pluginsFolder)
    .WithEnvironment("VOYAGER_USER_PLUGINS_DIRECTORY", userPluginsFolder);

var localService = builder.AddProject<Projects.Voyager_LocalService>("local-service")
    .WithHttpEndpoint()
    .WithEnvironment("Voyager__LocalToken", localToken)
    .WithEnvironment("Voyager__ShippedPluginsDirectory", pluginsFolder)
    .WithEnvironment("Voyager__UserPluginsDirectory", userPluginsFolder)
    .WaitForCompletion(plugins)
    // The user interface served by Vite calls the local service from its own origin.
    .WithEnvironment("Voyager__AllowedOrigins__0", userInterface.GetEndpoint("http"));

// The Electron window shows the Vite user interface and talks to the local service started above,
// instead of starting its own.
builder.AddJavaScriptApp("desktop", "../../ts/desktop", "dev")
    .WithPnpm(install: false)
    .WithEnvironment("VOYAGER_UI_URL", userInterface.GetEndpoint("http"))
    .WithEnvironment("VOYAGER_LOCAL_SERVICE_URL", localService.GetEndpoint("http"))
    .WithEnvironment("VOYAGER_LOCAL_TOKEN", localToken)
    // The same folders as the service: the main process reads the plugins from them and writes the requests there.
    .WithEnvironment("VOYAGER_PLUGINS_DIRECTORY", pluginsFolder)
    .WithEnvironment("VOYAGER_USER_PLUGINS_DIRECTORY", userPluginsFolder)
    .WaitFor(userInterface)
    .WaitFor(localService);

await builder.Build().RunAsync();
