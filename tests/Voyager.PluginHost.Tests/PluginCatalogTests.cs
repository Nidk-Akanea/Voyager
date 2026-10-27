using AwesomeAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Voyager.PluginHost.Tests;

[TestClass]
public sealed class PluginCatalogTests
{
    private static readonly Version _sdkVersion = new(0, 1, 0);

    private static PluginCatalog Load(PluginFolder folder, ILoggerFactory? loggerFactory = null) =>
        PluginCatalog.Load(folder.Path, _sdkVersion, loggerFactory ?? NullLoggerFactory.Instance);

    [TestMethod]
    public void Load_NoFolderConfigured_FindsNoPlugin()
    {
        using var catalog = PluginCatalog.Load(null, _sdkVersion, NullLoggerFactory.Instance);

        catalog.Plugins.Should().BeEmpty();
        catalog.Registry.DiagnosticFactSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Load_FolderThatDoesNotExist_FindsNoPlugin()
    {
        using var catalog = PluginCatalog.Load(
            Path.Combine(Path.GetTempPath(), $"voyager-missing-{Guid.NewGuid():N}"),
            _sdkVersion,
            NullLoggerFactory.Instance);

        catalog.Plugins.Should().BeEmpty();
    }

    [TestMethod]
    public void Load_ValidPlugin_IsLoadedAndRegistersItsSource()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("fixture-good", "Good");

        using var catalog = Load(folder);

        var plugin = catalog.Plugins.Should().ContainSingle().Subject;
        plugin.Should().BeEquivalentTo(new PluginState(
            "fixture-good",
            "fixture-good",
            "1.0.0",
            PluginStatus.Loaded,
            RejectionReason: null,
            UiModule: null,
            Provides: ["diagnostic-fact-source"]));
        catalog.Registry.DiagnosticFactSources.Should().ContainSingle().Which.OwnerId.Should().Be("fixture-good");
    }

    [TestMethod]
    public void Load_PluginWithAnInterfacePart_ExposesItsModule()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin(
            "fixture-good",
            "Good",
            PluginFolder.Manifest("fixture-good", "Voyager.PluginFixtures.Good.dll", uiPart: ", \"ui\": { \"module\": \"index.js\" }"));

        using var catalog = Load(folder);

        catalog.Plugins.Should().ContainSingle().Which.UiModule.Should().Be("index.js");
    }

    [TestMethod]
    public void Load_PluginWithOnlyAnInterfacePart_IsLoadedWithoutAnySource()
    {
        using var folder = new PluginFolder();
        folder.AddManifest("fixture-view", PluginFolder.Manifest("fixture-view", assembly: null, provides: string.Empty, uiPart: ", \"ui\": { \"module\": \"index.js\" }"));

        using var catalog = Load(folder);

        catalog.Plugins.Should().ContainSingle().Which.Status.Should().Be(PluginStatus.Loaded);
        catalog.Registry.DiagnosticFactSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Load_SeveralPlugins_ListsThemInAlphabeticalOrderOfFolders()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("fixture-probe", "Probe");
        folder.AddPlugin("fixture-good", "Good");

        using var catalog = Load(folder);

        catalog.Plugins.Select(plugin => plugin.Folder).Should().Equal("fixture-good", "fixture-probe");
    }

    [TestMethod]
    public async Task Load_PluginsThatShip_LoadAndGiveTheirFacts()
    {
        using var folder = new PluginFolder();
        folder.AddShippedPlugin("diagnostics-clock", "Voyager.Plugins.DiagnosticsClock");
        folder.AddShippedPlugin("diagnostics-runtime", "Voyager.Plugins.DiagnosticsRuntime");
        // The version of the SDK of the build, not a fixed one: the plugins say which one they were written for.
        using var catalog = PluginCatalog.Load(folder.Path, Voyager.Sdk.SdkInfo.Version, NullLoggerFactory.Instance);

        catalog.Plugins.Select(plugin => (plugin.Id, plugin.Status)).Should().Equal(
            ("diagnostics-clock", PluginStatus.Loaded),
            ("diagnostics-runtime", PluginStatus.Loaded));
        var collector = new DiagnosticFactCollector(catalog.Registry, PluginHostOptions.Default, NullLogger<DiagnosticFactCollector>.Instance);
        var results = await collector.CollectAsync(null, CancellationToken.None);
        results.Should().AllSatisfy(result => result.Outcome.Should().Be(DiagnosticFactOutcome.Succeeded));
        results.Select(result => result.Facts.Count).Should().Equal(2, 3);
    }

    [TestMethod]
    public void Load_PluginWithoutManifest_IsRejectedAsInvalidManifest()
    {
        using var folder = new PluginFolder();
        folder.AddEmptyFolder("no-manifest");

        using var catalog = Load(folder);

        catalog.Plugins.Should().ContainSingle().Which.Should().Match<PluginState>(plugin =>
            plugin.Id == "no-manifest"
            && plugin.Status == PluginStatus.Rejected
            && plugin.RejectionReason == PluginRejectionReason.ManifestInvalid);
    }

    [TestMethod]
    public void Load_ManifestThatIsNotJson_IsRejectedAsInvalidManifest()
    {
        using var folder = new PluginFolder();
        folder.AddManifest("garbage", "this is not json");

        using var catalog = Load(folder);

        catalog.Plugins.Should().ContainSingle().Which.RejectionReason.Should().Be(PluginRejectionReason.ManifestInvalid);
    }

    [TestMethod]
    public void Load_FolderNameDifferentFromId_IsRejectedAsInvalidManifest()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("renamed-folder", "Good", PluginFolder.Manifest("fixture-good", "Voyager.PluginFixtures.Good.dll"));

        using var catalog = Load(folder);

        catalog.Plugins.Should().ContainSingle().Which.RejectionReason.Should().Be(PluginRejectionReason.ManifestInvalid);
        catalog.Registry.DiagnosticFactSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Load_PluginThatTakesTheIdentifierOfTheService_IsRejected()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("service", "Good", PluginFolder.Manifest("service", "Voyager.PluginFixtures.Good.dll"));

        using var catalog = Load(folder);

        catalog.Plugins.Should().ContainSingle().Which.RejectionReason.Should().Be(PluginRejectionReason.ManifestInvalid);
    }

    [TestMethod]
    public void Load_TwoPluginsWithTheSameId_KeepsTheFirstAndRejectsTheSecond()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("fixture-good", "Good");
        folder.AddPlugin("fixture-zzz-copy", "Good", PluginFolder.Manifest("fixture-good", "Voyager.PluginFixtures.Good.dll"));

        using var catalog = Load(folder);

        catalog.Plugins.Select(plugin => (plugin.Folder, plugin.Status, plugin.RejectionReason)).Should().Equal(
            ("fixture-good", PluginStatus.Loaded, (PluginRejectionReason?)null),
            ("fixture-zzz-copy", PluginStatus.Rejected, PluginRejectionReason.DuplicateId));
        catalog.Registry.DiagnosticFactSources.Should().ContainSingle();
    }

    [TestMethod]
    public void Load_StrayCopyThatSortsBeforeTheOriginal_DoesNotMakeTheOriginalADuplicate()
    {
        using var folder = new PluginFolder();
        // Capital letters sort first: the copy is read before the plugin it was copied from.
        folder.AddPlugin("Copy of fixture-good", "Good", PluginFolder.Manifest("fixture-good", "Voyager.PluginFixtures.Good.dll"));
        folder.AddPlugin("fixture-good", "Good");

        using var catalog = Load(folder);

        catalog.Plugins.Select(plugin => (plugin.Folder, plugin.Status, plugin.RejectionReason)).Should().Equal(
            ("Copy of fixture-good", PluginStatus.Rejected, (PluginRejectionReason?)PluginRejectionReason.ManifestInvalid),
            ("fixture-good", PluginStatus.Loaded, null));
    }

    [TestMethod]
    public void Load_PluginWrittenForAnotherSdkVersion_IsRejectedAsIncompatible()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("fixture-good", "Good", PluginFolder.Manifest("fixture-good", "Voyager.PluginFixtures.Good.dll", sdk: "0.2"));

        using var catalog = Load(folder);

        catalog.Plugins.Should().ContainSingle().Which.RejectionReason.Should().Be(PluginRejectionReason.SdkIncompatible);
    }

    [TestMethod]
    [DataRow("ThrowsOnConfigure", PluginRejectionReason.LoadFailed)]
    [DataRow("InvalidContainer", PluginRejectionReason.LoadFailed)]
    [DataRow("NoPluginType", PluginRejectionReason.LoadFailed)]
    [DataRow("UndeclaredRegistration", PluginRejectionReason.UndeclaredRegistration)]
    public void Load_BrokenPlugin_IsRejectedWithItsReasonAndRegistersNothing(string fixture, PluginRejectionReason expected)
    {
        using var folder = new PluginFolder();
        var id = "fixture-broken";
        var provides = fixture == "UndeclaredRegistration" ? string.Empty : "\"diagnostic-fact-source\"";
        folder.AddPlugin(id, fixture, PluginFolder.Manifest(id, $"Voyager.PluginFixtures.{fixture}.dll", provides));

        using var catalog = Load(folder);

        catalog.Plugins.Should().ContainSingle().Which.Should().Match<PluginState>(plugin =>
            plugin.Status == PluginStatus.Rejected && plugin.RejectionReason == expected);
        catalog.Registry.DiagnosticFactSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Load_AssemblyFileThatIsMissing_IsRejectedAsLoadFailed()
    {
        using var folder = new PluginFolder();
        folder.AddManifest("fixture-missing", PluginFolder.Manifest("fixture-missing", "Voyager.PluginFixtures.Nothing.dll"));

        using var catalog = Load(folder);

        catalog.Plugins.Should().ContainSingle().Which.RejectionReason.Should().Be(PluginRejectionReason.LoadFailed);
    }

    [TestMethod]
    public void Load_BrokenPluginNextToGoodOnes_DoesNotAffectThem()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("fixture-a-broken", "ThrowsOnConfigure", PluginFolder.Manifest("fixture-a-broken", "Voyager.PluginFixtures.ThrowsOnConfigure.dll"));
        folder.AddPlugin("fixture-good", "Good");
        folder.AddPlugin("fixture-probe", "Probe");

        using var catalog = Load(folder);

        catalog.Plugins.Select(plugin => (plugin.Id, plugin.Status)).Should().Equal(
            ("fixture-a-broken", PluginStatus.Rejected),
            ("fixture-good", PluginStatus.Loaded),
            ("fixture-probe", PluginStatus.Loaded));
        catalog.Registry.DiagnosticFactSources.Select(source => source.OwnerId).Should().Equal("fixture-good", "fixture-probe");
    }

    [TestMethod]
    public async Task Load_Plugin_RunsInItsOwnContextAndSharesTheSdkWithTheHost()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("fixture-probe", "Probe");
        var logs = new CapturingLoggerProvider();
        using var catalog = Load(folder, logs.CreateFactory());

        var facts = await catalog.Registry.DiagnosticFactSources.Single().Source.GetFactsAsync(CancellationToken.None);

        facts.Single(fact => fact.LabelId == "fixture-probe-own-context").Value.Text.Should().Be("plugin:fixture-probe");
        // The SDK of the plugin is the host's own copy: otherwise IPlugin would not even be the same type.
        facts.Single(fact => fact.LabelId == "fixture-probe-sdk-context").Value.Text.Should().Be("Default");
        logs.Messages.Should().Contain("The probe plugin was asked for its facts", "plugins log through the host");
    }

    [TestMethod]
    public void Load_Plugin_LogsNoFolderPathOrManifestContent()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("fixture-good", "Good");
        folder.AddManifest("secret-folder", "{ \"id\": \"Not Valid\" }");
        var logs = new CapturingLoggerProvider();

        using var catalog = Load(folder, logs.CreateFactory());

        logs.Messages.Should().NotBeEmpty();
        logs.Messages.Should().AllSatisfy(message =>
        {
            message.Should().NotContain(folder.Path, "paths hold the user's account name");
            message.Should().NotContain("Not Valid");
        });
    }

    [TestMethod]
    public void Load_PluginWrittenForAnotherSdkVersion_LogsBothVersions()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("fixture-good", "Good", PluginFolder.Manifest("fixture-good", "Voyager.PluginFixtures.Good.dll", sdk: "0.2"));
        var logs = new CapturingLoggerProvider();

        using var catalog = Load(folder, logs.CreateFactory());

        logs.Messages.Should().Contain(message => message.Contains("SDK 0.2", StringComparison.Ordinal) && message.Contains("SDK 0.1", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Dispose_PluginWhoseServiceFailsToShutDown_DoesNotThrowAndIsLogged()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("fixture-throws-on-dispose", "ThrowsOnDispose");
        folder.AddPlugin("fixture-good", "Good");
        var logs = new CapturingLoggerProvider();
        var catalog = Load(folder, logs.CreateFactory());
        // The singleton exists since the plugin loaded: its source was resolved to check the container.
        var dispose = catalog.Dispose;

        dispose.Should().NotThrow();
        logs.Messages.Should().Contain("The container of plugin fixture-throws-on-dispose did not shut down cleanly");
    }
}
