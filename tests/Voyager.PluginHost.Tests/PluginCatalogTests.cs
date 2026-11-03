using AwesomeAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Voyager.Sdk;

namespace Voyager.PluginHost.Tests;

[TestClass]
public sealed class PluginCatalogTests
{
    #region Fields

    private static readonly ContractSupport _contracts = ContractSupport.Application;

    // The application of a later release, where the diagnostic fact source, version 1, is about to be removed.
    private static readonly ContractSupport _deprecatedFactSource = new(
        [
            new ContractVersion(ContractVersions.PLUGIN, 1, ContractVersionState.Supported),
            new ContractVersion(ExtensionPoints.DIAGNOSTIC_FACT_SOURCE, 1, ContractVersionState.Deprecated),
        ],
        [ContractVersions.PLUGIN]);

    #endregion Fields

    #region Private Methods

    private static PluginCatalog Load(PluginFolder folder, ILoggerFactory? loggerFactory = null) =>
        PluginCatalog.Load(folder.Path, _contracts, loggerFactory ?? NullLoggerFactory.Instance);

    private static PluginCatalog Load(PluginFolder shipped, PluginFolder installed, params PluginKey[] disabled) =>
        PluginCatalog.Load(
            [new PluginRoot(shipped.Path, PluginOrigin.Shipped), new PluginRoot(installed.Path, PluginOrigin.Installed)],
            _contracts,
            disabled.ToHashSet(),
            NullLoggerFactory.Instance);

    // A plugin with only an interface part: nothing to load, which keeps the tests of order and roots quick.
    private static string ViewManifest(string id) =>
        PluginFolder.Manifest(id, assembly: null, provides: string.Empty, uiPart: ", \"ui\": { \"module\": \"index.js\" }");

    #endregion Private Methods

    #region Public Methods

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

    [TestMethod]
    public void Load_AssemblyFileThatIsMissing_IsRejectedAsLoadFailed()
    {
        using var folder = new PluginFolder();
        folder.AddManifest("fixture-missing", PluginFolder.Manifest("fixture-missing", "Voyager.PluginFixtures.Nothing.dll"));

        using var catalog = Load(folder);

        catalog.Plugins.Should().ContainSingle().Which.RejectionReason.Should().Be(PluginRejectionReason.LoadFailed);
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
        var provides = fixture == "UndeclaredRegistration" ? string.Empty : PluginFolder.PROVIDES_FACT_SOURCE;
        folder.AddPlugin(id, fixture, PluginFolder.Manifest(id, $"Voyager.PluginFixtures.{fixture}.dll", provides));

        using var catalog = Load(folder);

        catalog.Plugins.Should().ContainSingle().Which.Should().Match<PluginState>(plugin =>
            plugin.Status == PluginStatus.Rejected && plugin.RejectionReason == expected);
        catalog.Registry.DiagnosticFactSources.Should().BeEmpty();
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
    public void Load_FolderNameDifferentFromId_IsRejectedAsInvalidManifest()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("renamed-folder", "Good", PluginFolder.Manifest("fixture-good", "Voyager.PluginFixtures.Good.dll"));

        using var catalog = Load(folder);

        catalog.Plugins.Should().ContainSingle().Which.RejectionReason.Should().Be(PluginRejectionReason.ManifestInvalid);
        catalog.Registry.DiagnosticFactSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Load_DisabledPlugin_IsListedAsDisabledAndRegistersNothing()
    {
        using var shipped = new PluginFolder();
        shipped.AddPlugin("fixture-good", "Good");
        using var installed = new PluginFolder();

        using var catalog = Load(shipped, installed, new PluginKey(PluginOrigin.Shipped, "fixture-good"));

        catalog.Plugins.Should().ContainSingle().Which.Should().Match<PluginState>(plugin =>
            plugin.Id == "fixture-good"
            && plugin.Origin == PluginOrigin.Shipped
            && plugin.Status == PluginStatus.Disabled
            && plugin.RejectionReason == null
            && plugin.UiModule == null
            && plugin.Provides.Count == 0);
        catalog.Registry.DiagnosticFactSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Load_DisabledPluginOfTheOtherOrigin_DoesNotDisableThisOne()
    {
        using var shipped = new PluginFolder();
        using var installed = new PluginFolder();
        installed.AddManifest("fixture-view", ViewManifest("fixture-view"));

        using var catalog = Load(shipped, installed, new PluginKey(PluginOrigin.Shipped, "fixture-view"));

        catalog.Plugins.Should().ContainSingle().Which.Status.Should().Be(PluginStatus.Loaded);
    }

    [TestMethod]
    public void Load_DisabledShippedPlugin_LetsTheInstalledOneWithTheSameIdLoad()
    {
        using var shipped = new PluginFolder();
        shipped.AddPlugin("fixture-good", "Good");
        using var installed = new PluginFolder();
        installed.AddPlugin("fixture-good", "Good");

        using var catalog = Load(shipped, installed, new PluginKey(PluginOrigin.Shipped, "fixture-good"));

        catalog.Plugins.Select(plugin => (plugin.Origin, plugin.Status, plugin.RejectionReason)).Should().Equal(
            (PluginOrigin.Shipped, PluginStatus.Disabled, (PluginRejectionReason?)null),
            (PluginOrigin.Installed, PluginStatus.Loaded, null));
        catalog.Registry.DiagnosticFactSources.Should().ContainSingle().Which.OwnerId.Should().Be("fixture-good");
    }

    [TestMethod]
    public void Load_FolderStartingWithADot_IsSkipped()
    {
        using var folder = new PluginFolder();
        folder.AddManifest(".removing-fixture-view-0123456789abcdef", ViewManifest("fixture-view"));
        folder.AddManifest(".hidden", ViewManifest("fixture-hidden"));
        folder.AddManifest("fixture-other", ViewManifest("fixture-other"));

        using var catalog = Load(folder);

        catalog.Plugins.Select(plugin => plugin.Folder).Should().Equal("fixture-other");
    }

    [TestMethod]
    public void Load_FolderThatDoesNotExist_FindsNoPlugin()
    {
        using var catalog = PluginCatalog.Load(
            Path.Combine(Path.GetTempPath(), $"voyager-missing-{Guid.NewGuid():N}"),
            _contracts,
            NullLoggerFactory.Instance);

        catalog.Plugins.Should().BeEmpty();
    }

    [TestMethod]
    public void Load_InstalledPluginWithTheIdOfAShippedOne_IsRejectedAsDuplicateOfShipped()
    {
        using var shipped = new PluginFolder();
        shipped.AddPlugin("fixture-good", "Good");
        using var installed = new PluginFolder();
        installed.AddPlugin("fixture-good", "Good");

        using var catalog = Load(shipped, installed);

        catalog.Plugins.Select(plugin => (plugin.Origin, plugin.Status, plugin.RejectionReason)).Should().Equal(
            (PluginOrigin.Shipped, PluginStatus.Loaded, (PluginRejectionReason?)null),
            (PluginOrigin.Installed, PluginStatus.Rejected, PluginRejectionReason.DuplicateOfShipped));
        catalog.Registry.DiagnosticFactSources.Should().ContainSingle();
    }

    [TestMethod]
    public void Load_InstalledCopyUnderAnotherFolderNameWithTheIdOfAShippedOne_IsRejectedAsDuplicateOfShipped()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest("fixture-view", ViewManifest("fixture-view"));
        using var installed = new PluginFolder();
        installed.AddManifest("fixture-view-copy", ViewManifest("fixture-view"));

        using var catalog = Load(shipped, installed);

        catalog.Plugins.Select(plugin => (plugin.Folder, plugin.Origin, plugin.Status, plugin.RejectionReason)).Should().Equal(
            ("fixture-view", PluginOrigin.Shipped, PluginStatus.Loaded, (PluginRejectionReason?)null),
            ("fixture-view-copy", PluginOrigin.Installed, PluginStatus.Rejected, PluginRejectionReason.DuplicateOfShipped));
    }

    [TestMethod]
    public void Load_TwoInstalledFoldersWithTheSameId_RejectsTheSecondAsDuplicateIdNotDuplicateOfShipped()
    {
        using var shipped = new PluginFolder();
        using var installed = new PluginFolder();
        installed.AddManifest("fixture-view", ViewManifest("fixture-view"));
        installed.AddManifest("fixture-view-copy", ViewManifest("fixture-view"));

        using var catalog = Load(shipped, installed);

        catalog.Plugins.Select(plugin => (plugin.Folder, plugin.Origin, plugin.Status, plugin.RejectionReason)).Should().Equal(
            ("fixture-view", PluginOrigin.Installed, PluginStatus.Loaded, (PluginRejectionReason?)null),
            ("fixture-view-copy", PluginOrigin.Installed, PluginStatus.Rejected, PluginRejectionReason.DuplicateId));
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
    public void Load_NoFolderConfigured_FindsNoPlugin()
    {
        using var catalog = PluginCatalog.Load(null, _contracts, NullLoggerFactory.Instance);

        catalog.Plugins.Should().BeEmpty();
        catalog.Registry.DiagnosticFactSources.Should().BeEmpty();
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
    [DataRow(true, false, DisplayName = ".NET part only")]
    [DataRow(false, true, DisplayName = "interface part only")]
    [DataRow(true, true, DisplayName = "both parts")]
    public void Load_Plugin_TellsWhichPartsItBrings(bool dotnet, bool ui)
    {
        using var folder = new PluginFolder();
        folder.AddPlugin(
            "fixture-good",
            "Good",
            PluginFolder.Manifest(
                "fixture-good",
                dotnet ? "Voyager.PluginFixtures.Good.dll" : null,
                provides: dotnet ? PluginFolder.PROVIDES_FACT_SOURCE : string.Empty,
                uiPart: ui ? ", \"ui\": { \"module\": \"index.js\" }" : string.Empty));

        using var catalog = Load(folder);

        catalog.Plugins.Should().ContainSingle().Which.Should().Match<PluginState>(plugin =>
            plugin.Status == PluginStatus.Loaded && plugin.HasDotnet == dotnet && plugin.HasUi == ui);
    }

    [TestMethod]
    public void Load_PluginNotInUse_StillTellsWhichPartsItBrings()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin(
            "fixture-good",
            "Good",
            PluginFolder.Manifest(
                "fixture-good",
                "Voyager.PluginFixtures.Good.dll",
                provides: """{ "contract": "diagnostic-fact-source", "version": 2 }""",
                uiPart: ", \"ui\": { \"module\": \"index.js\" }"));

        using var catalog = Load(folder);

        catalog.Plugins.Should().ContainSingle().Which.Should().Match<PluginState>(plugin =>
            plugin.Status == PluginStatus.Rejected && plugin.HasDotnet && plugin.HasUi);
    }

    [TestMethod]
    public void Load_PluginWithAnUnreadableManifest_BringsNoKnownPart()
    {
        using var folder = new PluginFolder();
        folder.AddManifest("garbage", "this is not json");

        using var catalog = Load(folder);

        catalog.Plugins.Should().ContainSingle().Which.Should().Match<PluginState>(plugin => !plugin.HasDotnet && !plugin.HasUi);
    }

    [TestMethod]
    public void Load_PluginRejectedForAContractNextToAGoodOne_DoesNotAffectIt()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin(
            "fixture-a-too-recent",
            "Good",
            PluginFolder.Manifest("fixture-a-too-recent", "Voyager.PluginFixtures.Good.dll", provides: """{ "contract": "diagnostic-fact-source", "version": 2 }"""));
        folder.AddPlugin("fixture-good", "Good");

        using var catalog = Load(folder);

        catalog.Plugins.Select(plugin => (plugin.Id, plugin.Status, plugin.RejectionReason)).Should().Equal(
            ("fixture-a-too-recent", PluginStatus.Rejected, (PluginRejectionReason?)PluginRejectionReason.ContractNotSupported),
            ("fixture-good", PluginStatus.Loaded, null));
        catalog.Registry.DiagnosticFactSources.Should().ContainSingle().Which.OwnerId.Should().Be("fixture-good");
    }

    [TestMethod]
    public async Task Load_PluginsThatShip_LoadAndGiveTheirFacts()
    {
        using var folder = new PluginFolder();
        folder.AddShippedPlugin("diagnostics-clock", "Voyager.Plugins.DiagnosticsClock");
        folder.AddShippedPlugin("diagnostics-runtime", "Voyager.Plugins.DiagnosticsRuntime");
        // The contracts of the SDK of the build and the manifests of the repository: they must agree.
        using var catalog = PluginCatalog.Load(folder.Path, ContractSupport.Application, NullLoggerFactory.Instance);

        catalog.Plugins.Select(plugin => (plugin.Id, plugin.Status)).Should().Equal(
            ("diagnostics-clock", PluginStatus.Loaded),
            ("diagnostics-runtime", PluginStatus.Loaded));
        var collector = new DiagnosticFactCollector(catalog.Registry, PluginHostOptions.Default, NullLogger<DiagnosticFactCollector>.Instance);
        var results = await collector.CollectAsync(null, CancellationToken.None);
        results.Should().AllSatisfy(result => result.Outcome.Should().Be(DiagnosticFactOutcome.Succeeded));
        results.Select(result => result.Facts.Count).Should().Equal(2, 3);
    }

    [TestMethod]
    public void Load_PluginThatProvidesADeprecatedContract_IsLoadedAndShowsTheDeprecation()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("fixture-good", "Good");

        using var catalog = PluginCatalog.Load(folder.Path, _deprecatedFactSource, NullLoggerFactory.Instance);

        var plugin = catalog.Plugins.Should().ContainSingle().Subject;
        plugin.Status.Should().Be(PluginStatus.Loaded);
        plugin.RejectionReason.Should().BeNull();
        plugin.Contracts.Should().Equal(
            new ContractUse("diagnostic-fact-source", 1, ContractRole.Provided, ContractUseState.Deprecated),
            new ContractUse("plugin", 1, ContractRole.Required, ContractUseState.Supported));
        catalog.Registry.DiagnosticFactSources.Should().ContainSingle().Which.OwnerId.Should().Be("fixture-good");
    }

    [TestMethod]
    public void Load_PluginThatProvidesADeprecatedContract_LogsTheDeprecation()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("fixture-good", "Good");
        var logs = new CapturingLoggerProvider();

        using var catalog = PluginCatalog.Load(folder.Path, _deprecatedFactSource, logs.CreateFactory());

        logs.Messages.Should().ContainSingle(message =>
            message.Contains("fixture-good", StringComparison.Ordinal)
            && message.Contains("contract diagnostic-fact-source version 1", StringComparison.Ordinal)
            && message.Contains("a later release will remove", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Load_PluginThatRequiresAnObsoleteContract_IsRejectedAsContractNotSupported()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("fixture-good", "Good");
        var contracts = new ContractSupport(
        [
            new ContractVersion("plugin", 1, ContractVersionState.Obsolete),
            new ContractVersion("plugin", 2, ContractVersionState.Supported),
            new ContractVersion("diagnostic-fact-source", 1, ContractVersionState.Supported),
        ],
        [ContractVersions.PLUGIN]);

        using var catalog = PluginCatalog.Load(folder.Path, contracts, NullLoggerFactory.Instance);

        var plugin = catalog.Plugins.Should().ContainSingle().Subject;
        plugin.Status.Should().Be(PluginStatus.Rejected);
        plugin.RejectionReason.Should().Be(PluginRejectionReason.ContractNotSupported);
        plugin.Contracts.Should().Equal(
            new ContractUse("diagnostic-fact-source", 1, ContractRole.Provided, ContractUseState.Supported),
            new ContractUse("plugin", 1, ContractRole.Required, ContractUseState.Obsolete));
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
    public void Load_PluginWithTwoUnsupportedContracts_ListsBoth()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin(
            "fixture-good",
            "Good",
            PluginFolder.Manifest(
                "fixture-good",
                "Voyager.PluginFixtures.Good.dll",
                provides: """{ "contract": "diagnostic-fact-source", "version": 2 }""",
                requires: """{ "contract": "plugin", "version": 7 }"""));

        using var catalog = Load(folder);

        var plugin = catalog.Plugins.Should().ContainSingle().Subject;
        plugin.RejectionReason.Should().Be(PluginRejectionReason.ContractNotSupported);
        plugin.Contracts.Should().Equal(
            new ContractUse("diagnostic-fact-source", 2, ContractRole.Provided, ContractUseState.Unknown),
            new ContractUse("plugin", 7, ContractRole.Required, ContractUseState.Unknown));
    }

    [TestMethod]
    public void Load_PluginWithTwoUnsupportedContracts_LogsOneLinePerContract()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin(
            "fixture-good",
            "Good",
            PluginFolder.Manifest(
                "fixture-good",
                "Voyager.PluginFixtures.Good.dll",
                provides: """{ "contract": "diagnostic-fact-source", "version": 2 }""",
                requires: """{ "contract": "plugin", "version": 7 }"""));
        var logs = new CapturingLoggerProvider();

        using var catalog = Load(folder, logs.CreateFactory());

        logs.Messages.Where(message => message.Contains("is set aside: it uses contract", StringComparison.Ordinal)).Should().SatisfyRespectively(
            message => message.Should().Contain("contract diagnostic-fact-source version 2").And.Contain("Unknown"),
            message => message.Should().Contain("contract plugin version 7").And.Contain("Unknown"));
    }

    [TestMethod]
    public void Load_PluginWrittenForAnUnknownContractVersion_IsRejectedAsContractNotSupported()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin(
            "fixture-good",
            "Good",
            PluginFolder.Manifest("fixture-good", "Voyager.PluginFixtures.Good.dll", provides: """{ "contract": "diagnostic-fact-source", "version": 2 }"""));

        using var catalog = Load(folder);

        var plugin = catalog.Plugins.Should().ContainSingle().Subject;
        plugin.RejectionReason.Should().Be(PluginRejectionReason.ContractNotSupported);
        plugin.Contracts.Should().Contain(new ContractUse("diagnostic-fact-source", 2, ContractRole.Provided, ContractUseState.Unknown));
        catalog.Registry.DiagnosticFactSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Load_PluginWrittenForAnUnknownContractVersion_LogsTheContractAndItsVersion()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin(
            "fixture-good",
            "Good",
            PluginFolder.Manifest("fixture-good", "Voyager.PluginFixtures.Good.dll", requires: """{ "contract": "plugin", "version": 7 }"""));
        var logs = new CapturingLoggerProvider();

        using var catalog = Load(folder, logs.CreateFactory());

        logs.Messages.Should().Contain(message =>
            message.Contains("contract plugin version 7", StringComparison.Ordinal) && message.Contains("Unknown", StringComparison.Ordinal));
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
    public void Load_TwoRoots_ListsTheShippedPluginsBeforeTheInstalledOnes()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest("fixture-zulu", ViewManifest("fixture-zulu"));
        using var installed = new PluginFolder();
        installed.AddManifest("fixture-bravo", ViewManifest("fixture-bravo"));
        installed.AddManifest("fixture-alpha", ViewManifest("fixture-alpha"));

        using var catalog = Load(shipped, installed);

        catalog.Plugins.Select(plugin => (plugin.Id, plugin.Origin, plugin.Status)).Should().Equal(
            ("fixture-zulu", PluginOrigin.Shipped, PluginStatus.Loaded),
            ("fixture-alpha", PluginOrigin.Installed, PluginStatus.Loaded),
            ("fixture-bravo", PluginOrigin.Installed, PluginStatus.Loaded));
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
    public void Load_ValidPlugin_IsLoadedAndRegistersItsSource()
    {
        using var folder = new PluginFolder();
        folder.AddPlugin("fixture-good", "Good");

        using var catalog = Load(folder);

        var plugin = catalog.Plugins.Should().ContainSingle().Subject;
        plugin.Should().BeEquivalentTo(new PluginState(
            "fixture-good",
            PluginOrigin.Shipped,
            "fixture-good",
            "1.0.0",
            PluginStatus.Loaded,
            RejectionReason: null,
            UiModule: null,
            Provides: ["diagnostic-fact-source"],
            Contracts:
            [
                new ContractUse("diagnostic-fact-source", 1, ContractRole.Provided, ContractUseState.Supported),
                new ContractUse("plugin", 1, ContractRole.Required, ContractUseState.Supported),
            ],
            HasDotnet: true,
            HasUi: false));
        catalog.Registry.DiagnosticFactSources.Should().ContainSingle().Which.OwnerId.Should().Be("fixture-good");
    }

    #endregion Public Methods
}
