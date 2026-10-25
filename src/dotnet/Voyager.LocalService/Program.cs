// Local service host: runs next to the Electron app on each member's machine (ADR-0003).
// Unless Aspire or Electron decide otherwise, it listens on the loopback interface only, on a port
// chosen at startup: nothing outside this machine can reach it, and it never collides with another
// program.
using Voyager.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.UseDefaultUrlsWhenNotConfigured("http://127.0.0.1:0");

var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();
