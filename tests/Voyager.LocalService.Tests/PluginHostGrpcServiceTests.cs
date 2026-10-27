using AwesomeAssertions;

using Grpc.Core;

using NodaTime;
using NodaTime.Testing;

using Voyager.Contracts.PluginHost.V1;
using Voyager.PluginHost.Tests;

namespace Voyager.LocalService.Tests;

[TestClass]
public sealed class PluginHostGrpcServiceTests
{
    private static readonly Instant _start = Instant.FromUtc(2026, 3, 14, 9, 30);

    private static Metadata AuthorizedHeaders => LocalServiceFactory.TokenHeader(LocalServiceFactory.LOCAL_TOKEN);

    private static PluginHostService.PluginHostServiceClient CreateClient(LocalServiceFactory factory) =>
        new(factory.CreateChannel());

    [TestMethod]
    public async Task ListPlugins_WithoutPluginFolder_ListsNothingAndTheServiceStarts()
    {
        using var factory = new LocalServiceFactory();

        var response = await CreateClient(factory).ListPluginsAsync(
            new ListPluginsRequest(),
            AuthorizedHeaders);

        response.Plugins.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ListPlugins_GoodAndBrokenPlugins_StartsAndTellsWhichOneIsSetAside()
    {
        using var plugins = new PluginFolder();
        plugins.AddPlugin("fixture-good", "Good");
        plugins.AddPlugin("fixture-throws-on-configure", "ThrowsOnConfigure");
        plugins.AddManifest("fixture-no-ui", PluginFolder.Manifest("fixture-no-ui", assembly: null, provides: string.Empty, uiPart: ", \"ui\": { \"module\": \"views/index.js\" }"));
        using var factory = new LocalServiceFactory(pluginsDirectory: plugins.Path);

        var response = await CreateClient(factory).ListPluginsAsync(
            new ListPluginsRequest(),
            AuthorizedHeaders);

        response.Plugins.Select(plugin => (plugin.Id, plugin.Status, plugin.RejectionReason, plugin.UiModule)).Should().Equal(
            ("fixture-good", PluginStatus.Loaded, PluginRejectionReason.Unspecified, string.Empty),
            ("fixture-no-ui", PluginStatus.Loaded, PluginRejectionReason.Unspecified, "views/index.js"),
            ("fixture-throws-on-configure", PluginStatus.Rejected, PluginRejectionReason.LoadFailed, string.Empty));
        response.Plugins[0].Version.Should().Be("1.0.0");
        response.Plugins[0].Provides.Should().Equal("diagnostic-fact-source");
    }

    [TestMethod]
    public async Task GetDiagnosticFacts_ServiceAndPlugin_ReturnsTheFactsOfBothSources()
    {
        using var plugins = new PluginFolder();
        plugins.AddPlugin("fixture-good", "Good");
        using var factory = new LocalServiceFactory(clock: new FakeClock(_start), pluginsDirectory: plugins.Path);

        var response = await CreateClient(factory).GetDiagnosticFactsAsync(
            new GetDiagnosticFactsRequest(),
            AuthorizedHeaders);

        response.Sources.Select(source => (source.SourceId, source.Outcome)).Should().BeEquivalentTo(
        [
            ("fixture-good", FactsOutcome.Succeeded),
            ("service", FactsOutcome.Succeeded),
        ]);
        var service = response.Sources.Single(source => source.SourceId == "service");
        service.Facts.Select(fact => fact.LabelId).Should().Equal(
            "service-fact-version",
            "service-fact-runtime",
            "service-fact-environment",
            "service-fact-started-at",
            "service-fact-database");
        service.Facts.Single(fact => fact.LabelId == "service-fact-started-at").Instant.ToDateTimeOffset()
            .Should().Be(_start.ToDateTimeOffset());
        service.Facts.Single(fact => fact.LabelId == "service-fact-database").Text.Should().Be("available");

        var good = response.Sources.Single(source => source.SourceId == "fixture-good");
        good.Facts.Select(fact => fact.ValueCase).Should().Equal(Fact.ValueOneofCase.Text, Fact.ValueOneofCase.Number, Fact.ValueOneofCase.Instant);
        good.Facts[1].Number.Should().Be(42.5);
    }

    [TestMethod]
    public async Task GetDiagnosticFacts_WithASourceId_ReturnsOnlyThatSource()
    {
        using var plugins = new PluginFolder();
        plugins.AddPlugin("fixture-good", "Good");
        using var factory = new LocalServiceFactory(pluginsDirectory: plugins.Path);

        var response = await CreateClient(factory).GetDiagnosticFactsAsync(
            new GetDiagnosticFactsRequest { SourceId = "fixture-good" },
            AuthorizedHeaders);

        response.Sources.Should().ContainSingle().Which.SourceId.Should().Be("fixture-good");
    }

    [TestMethod]
    public async Task GetDiagnosticFacts_UnknownSource_IsNotFound()
    {
        using var factory = new LocalServiceFactory();

        var call = async () => await CreateClient(factory).GetDiagnosticFactsAsync(
            new GetDiagnosticFactsRequest { SourceId = "nobody" },
            AuthorizedHeaders);

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [TestMethod]
    public async Task ListPlugins_WithoutLocalToken_IsRefused()
    {
        using var factory = new LocalServiceFactory();

        var call = async () => await CreateClient(factory).ListPluginsAsync(new ListPluginsRequest());

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }
}
