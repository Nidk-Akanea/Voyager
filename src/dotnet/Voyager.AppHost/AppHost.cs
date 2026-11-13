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

// The plugins that ship with Voyager, built into artifacts/plugins before anything loads them (ADR-0015): the group
// server loads them at startup, and the desktop app uses their interface modules instead of downloading them.
var repositoryRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "..", ".."));
var pluginsFolder = Path.Combine(repositoryRoot, "artifacts", "plugins");
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
var groupServer = builder.AddProject<Projects.Voyager_GroupServer>("group-server")
    .WithHttpEndpoint(port: 5180)
    .WithReference(database)
    .WithEnvironment("Voyager__DevelopmentIdentity", "true")
    .WithEnvironment("Voyager__ShippedPluginsDirectory", pluginsFolder)
    .WaitFor(database)
    .WaitForCompletion(plugins);

// The TypeScript packages must be installed beforehand (pnpm install at the repository root).
// The page reads the files of the plugins through the protocol of the desktop app, which serves those the server loaded.
var userInterface = builder.AddViteApp("ui", "../../ts/ui")
    .WithPnpm(install: false);

// The Electron window shows the Vite user interface and forwards its calls to the group server.
builder.AddJavaScriptApp("desktop", "../../ts/desktop", "dev")
    .WithPnpm(install: false)
    .WithEnvironment("VOYAGER_UI_URL", userInterface.GetEndpoint("http"))
    // The group server the window shows: the main process forwards the calls of the page to it (ADR-0019, decision 4).
    .WithEnvironment("VOYAGER_SERVER_URL", groupServer.GetEndpoint("http"))
    // The same folder as the server: the main process uses its interface modules instead of downloading them.
    .WithEnvironment("VOYAGER_PLUGINS_DIRECTORY", pluginsFolder)
    .WaitFor(userInterface)
    .WaitFor(groupServer);

await builder.Build().RunAsync();
