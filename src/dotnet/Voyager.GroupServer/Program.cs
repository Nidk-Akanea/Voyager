// Group server host: shared by the groups it hosts, run online or under Docker (ADR-0003, ADR-0010).
using Voyager.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.UseDefaultUrlsWhenNotConfigured("http://localhost:5180");

// Disposed even when startup fails, so that the last logs still reach their destination.
await using var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();
