// Group server host: shared by the groups it hosts, run online or under Docker (ADR-0003, ADR-0010).
using Voyager.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.UseDefaultUrlsWhenNotConfigured("http://localhost:5180");

var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();
