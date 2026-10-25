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

builder.AddProject<Projects.Voyager_LocalService>("local-service")
    .WithHttpEndpoint();

await builder.Build().RunAsync();
