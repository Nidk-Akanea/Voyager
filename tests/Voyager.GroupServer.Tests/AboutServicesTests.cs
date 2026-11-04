using System.Net;
using System.Net.Http.Headers;
using System.Text;

using AwesomeAssertions;

using Grpc.Core;

using NodaTime;
using NodaTime.Testing;

using Voyager.Contracts.Diagnostics.V1;
using Voyager.Contracts.PluginHost.V1;
using Voyager.PluginHost.Tests;

namespace Voyager.GroupServer.Tests;

/// <summary>
/// The services of the "About" page, served by the group server (spec of the switch to client-server, lot 1).
/// </summary>
[TestClass]
public sealed class AboutServicesTests
{
    #region Fields

    private static readonly Instant _start = Instant.FromUtc(2026, 3, 14, 9, 30);

    #endregion Fields

    #region Public Methods

    [TestMethod]
    public async Task GetServiceInfo_WithoutDatabase_ReturnsVersionStartAndUnavailableDatabase()
    {
        using var factory = new GroupServerFactory(clock: new FakeClock(_start));

        var response = await new DiagnosticsService.DiagnosticsServiceClient(factory.CreateChannel())
            .GetServiceInfoAsync(new GetServiceInfoRequest());

        response.Version.Should().NotBeNullOrWhiteSpace().And.NotContain("+");
        response.Runtime.Should().StartWith(".NET");
        response.StartedAt.ToDateTimeOffset().Should().Be(_start.ToDateTimeOffset());
        response.DatabaseStatus.Should().Be(DatabaseStatus.Unavailable, "the tests give the server no database");
    }

    [TestMethod]
    public async Task GetServiceInfo_OverGrpcWebOnHttp11_IsServed()
    {
        using var factory = new GroupServerFactory();
        using var client = factory.CreateClient();
        // An empty request, framed as gRPC-Web frames it: no compression, a length of zero.
        using var content = new ByteArrayContent([0, 0, 0, 0, 0]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/grpc-web+proto");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/voyager.diagnostics.v1.DiagnosticsService/GetServiceInfo")
        {
            Content = content,
            Version = HttpVersion.Version11,
        };

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/grpc-web");
        // gRPC-Web carries the status in a trailer frame at the end of the body.
        Encoding.ASCII.GetString(await response.Content.ReadAsByteArrayAsync()).Should().Contain("grpc-status: 0");
    }

    [TestMethod]
    public async Task GetServiceInfo_DatabaseThatRefusesConnections_ReportsUnavailableDatabase()
    {
        // Port 1 on this machine: nothing listens there, so the connection is refused at once.
        using var factory = new GroupServerFactory(connectionString: "Host=127.0.0.1;Port=1;Database=voyager;Username=voyager;Timeout=2");

        var response = await new DiagnosticsService.DiagnosticsServiceClient(factory.CreateChannel())
            .GetServiceInfoAsync(new GetServiceInfoRequest());

        response.DatabaseStatus.Should().Be(DatabaseStatus.Unavailable);
    }

    [TestMethod]
    public async Task ListPlugins_GoodAndBrokenShippedPlugins_ListsBothAndSetsTheBrokenOneAside()
    {
        using var plugins = new PluginFolder();
        plugins.AddPlugin("fixture-good", "Good");
        plugins.AddPlugin("fixture-throws-on-configure", "ThrowsOnConfigure");
        using var factory = new GroupServerFactory(shippedPluginsDirectory: plugins.Path);

        var response = await new PluginHostService.PluginHostServiceClient(factory.CreateChannel())
            .ListPluginsAsync(new ListPluginsRequest());

        response.Plugins.Select(plugin => (plugin.Id, plugin.Status, plugin.Origin)).Should().BeEquivalentTo(
        [
            ("fixture-good", PluginStatus.Loaded, PluginOrigin.Shipped),
            ("fixture-throws-on-configure", PluginStatus.Rejected, PluginOrigin.Shipped),
        ]);
        response.Plugins.Single(plugin => plugin.Id == "fixture-throws-on-configure").RejectionReason
            .Should().Be(PluginRejectionReason.LoadFailed);
        response.Plugins.Should().AllSatisfy(plugin =>
        {
            plugin.PendingRequest.Should().Be(PluginRequestAction.Unspecified, "the server has no requests yet");
            plugin.PackageSha256.Should().BeEmpty();
        });
    }

    [TestMethod]
    public async Task GetDiagnosticFacts_ServerAndPlugin_ReturnsTheFactsOfBothSources()
    {
        using var plugins = new PluginFolder();
        plugins.AddPlugin("fixture-good", "Good");
        using var factory = new GroupServerFactory(shippedPluginsDirectory: plugins.Path, clock: new FakeClock(_start));

        var response = await new PluginHostService.PluginHostServiceClient(factory.CreateChannel())
            .GetDiagnosticFactsAsync(new GetDiagnosticFactsRequest());

        response.Sources.Select(source => (source.SourceId, source.Outcome)).Should().BeEquivalentTo(
        [
            ("fixture-good", FactsOutcome.Succeeded),
            ("service", FactsOutcome.Succeeded),
        ]);
        var server = response.Sources.Single(source => source.SourceId == "service");
        server.Facts.Select(fact => fact.LabelId).Should().Equal(
            "service-fact-version",
            "service-fact-runtime",
            "service-fact-environment",
            "service-fact-started-at",
            "service-fact-database");
        server.Facts.Single(fact => fact.LabelId == "service-fact-database").Text.Should().Be("unavailable");
    }

    [TestMethod]
    public async Task GetDiagnosticFacts_UnknownSource_IsNotFound()
    {
        using var factory = new GroupServerFactory();

        var call = async () => await new PluginHostService.PluginHostServiceClient(factory.CreateChannel())
            .GetDiagnosticFactsAsync(new GetDiagnosticFactsRequest { SourceId = "nobody" });

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [TestMethod]
    public async Task ExaminePackage_OnTheServer_IsNotServedYet()
    {
        using var factory = new GroupServerFactory();

        var call = async () => await new PluginHostService.PluginHostServiceClient(factory.CreateChannel())
            .ExaminePackageAsync(new ExaminePackageRequest { StagedName = "a1b2c3d4e5f60718293a4b5c6d7e8f90.voyagerplugin" });

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.Unimplemented);
    }

    #endregion Public Methods
}
