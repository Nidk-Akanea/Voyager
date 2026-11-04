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
    #region Fields

    private const string STAGED_NAME = "a1b2c3d4e5f60718293a4b5c6d7e8f90.voyagerplugin";
    private const string GONE_STAGED_NAME = "60e60e60e60e60e60e60e60e60e60e60.voyagerplugin";

    private static readonly Instant _start = Instant.FromUtc(2026, 3, 14, 9, 30);

    #endregion Fields

    #region Properties

    private static Metadata AuthorizedHeaders => LocalServiceFactory.TokenHeader(LocalServiceFactory.LOCAL_TOKEN);

    #endregion Properties

    #region Private Methods

    private static PluginHostService.PluginHostServiceClient CreateClient(LocalServiceFactory factory) =>
        new(factory.CreateChannel());

    // A plugin with only an interface part: nothing to load, so that the tests of origins and requests stay quick.
    private static string ViewManifest(string id) =>
        PluginFolder.Manifest(id, assembly: null, provides: string.Empty, uiPart: ", \"ui\": { \"module\": \"index.js\" }");

    #endregion Private Methods

    #region Public Methods

    [TestMethod]
    public async Task ExaminePackage_InstalledOlderVersion_IsAnUpgradeThatNamesTheInstalledVersion()
    {
        using var user = new UserPluginFolder();
        user.Installed.AddManifest("fixture-view", TestPackage.Manifest("fixture-view", "1.0.0"));
        TestPackage.Stage(user, STAGED_NAME, TestPackage.View("fixture-view", "1.1.0"));
        using var factory = new LocalServiceFactory(userPluginsDirectory: user.Path);

        var response = await CreateClient(factory).ExaminePackageAsync(new ExaminePackageRequest { StagedName = STAGED_NAME }, AuthorizedHeaders);

        response.InstallKind.Should().Be(PackageInstallKind.Upgrade);
        response.InstalledVersion.Should().Be("1.0.0");
    }

    [TestMethod]
    [DataRow("../a.voyagerplugin", DisplayName = "parent folder")]
    [DataRow(@"C:\Users\a.voyagerplugin", DisplayName = "absolute path")]
    [DataRow("a1b2c3d4e5f60718293a4b5c6d7e8f90.zip", DisplayName = "other extension")]
    [DataRow("travel-plugin.voyagerplugin", DisplayName = "name of the file the user chose")]
    [DataRow("A1B2C3D4E5F60718293A4B5C6D7E8F90.voyagerplugin", DisplayName = "hexadecimal in upper case")]
    [DataRow("", DisplayName = "empty")]
    public async Task ExaminePackage_NameThatIsNotAPreparedCopy_IsInvalidArgument(string stagedName)
    {
        using var user = new UserPluginFolder();
        using var factory = new LocalServiceFactory(userPluginsDirectory: user.Path);

        var call = async () => await CreateClient(factory).ExaminePackageAsync(new ExaminePackageRequest { StagedName = stagedName }, AuthorizedHeaders);

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [TestMethod]
    public async Task ExaminePackage_MissingCopy_IsNotFound()
    {
        using var user = new UserPluginFolder();
        Directory.CreateDirectory(user.StagingDirectory);
        using var factory = new LocalServiceFactory(userPluginsDirectory: user.Path);

        var call = async () => await CreateClient(factory).ExaminePackageAsync(new ExaminePackageRequest { StagedName = STAGED_NAME }, AuthorizedHeaders);

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [TestMethod]
    public async Task ExaminePackage_RefusedPackage_ReturnsTheRefusalAndNoPlugin()
    {
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, [.. TestPackage.View("fixture-view"), PackageEntry.Text("../escaped.txt", "Traveler A")]);
        using var factory = new LocalServiceFactory(userPluginsDirectory: user.Path);

        var response = await CreateClient(factory).ExaminePackageAsync(new ExaminePackageRequest { StagedName = STAGED_NAME }, AuthorizedHeaders);

        response.Refusal.Should().Be(PackageRefusal.ForbiddenEntry);
        response.Id.Should().BeEmpty();
        response.InstallKind.Should().Be(PackageInstallKind.Unspecified);
        response.Sha256.Should().Be(TestPackage.Sha256Of(package));
    }

    [TestMethod]
    public async Task ExaminePackage_ValidPackage_ReturnsWhatItHoldsAndWhatInstallingWouldDo()
    {
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, TestPackage.View("fixture-view", "1.2.0"));
        using var factory = new LocalServiceFactory(userPluginsDirectory: user.Path);

        var response = await CreateClient(factory).ExaminePackageAsync(new ExaminePackageRequest { StagedName = STAGED_NAME }, AuthorizedHeaders);

        response.Refusal.Should().Be(PackageRefusal.Unspecified);
        response.Id.Should().Be("fixture-view");
        response.Version.Should().Be("1.2.0");
        response.Contracts.Select(contract => (contract.Contract, contract.Version, contract.Role, contract.State))
            .Should().Equal(("plugin", 1u, ContractRole.Required, ContractUseState.Supported));
        response.HasUi.Should().BeTrue();
        response.HasDotnet.Should().BeFalse();
        response.Verification.Should().Be(PluginVerification.Unverified);
        response.DuplicateOfShipped.Should().BeFalse();
        response.InstalledVersion.Should().BeEmpty();
        response.InstallKind.Should().Be(PackageInstallKind.New);
        response.Sha256.Should().Be(TestPackage.Sha256Of(package));
        response.Size.Should().Be((ulong)new FileInfo(package).Length);
    }

    [TestMethod]
    public async Task ExaminePackage_WithoutUserPluginFolder_IsFailedPrecondition()
    {
        using var factory = new LocalServiceFactory();

        var call = async () => await CreateClient(factory).ExaminePackageAsync(new ExaminePackageRequest { StagedName = STAGED_NAME }, AuthorizedHeaders);

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.FailedPrecondition);
    }

    [TestMethod]
    public async Task ExaminePackage_DamagedCentralDirectory_ReturnsTheRefusalUnreadable()
    {
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, TestPackage.View("fixture-view"));
        TestPackage.DamageCentralDirectory(package);
        using var factory = new LocalServiceFactory(userPluginsDirectory: user.Path);

        var response = await CreateClient(factory).ExaminePackageAsync(new ExaminePackageRequest { StagedName = STAGED_NAME }, AuthorizedHeaders);

        response.Refusal.Should().Be(PackageRefusal.Unreadable);
        response.Id.Should().BeEmpty();
        response.Sha256.Should().Be(TestPackage.Sha256Of(package));
    }

    [TestMethod]
    public async Task ExaminePackage_ValidPackage_LeavesOnlyTheCopyInTheStagingFolder()
    {
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, [.. TestPackage.View("fixture-view"), PackageEntry.Text("data/rates.csv", "EUR;1.00")]);
        using var factory = new LocalServiceFactory(userPluginsDirectory: user.Path);

        await CreateClient(factory).ExaminePackageAsync(new ExaminePackageRequest { StagedName = STAGED_NAME }, AuthorizedHeaders);

        Directory.EnumerateFileSystemEntries(user.StagingDirectory).Should().Equal(package);
    }

    [TestMethod]
    public async Task GetDiagnosticFacts_ServiceAndPlugin_ReturnsTheFactsOfBothSources()
    {
        using var plugins = new PluginFolder();
        plugins.AddPlugin("fixture-good", "Good");
        using var factory = new LocalServiceFactory(clock: new FakeClock(_start), shippedPluginsDirectory: plugins.Path);

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
    public async Task GetDiagnosticFacts_UnknownSource_IsNotFound()
    {
        using var factory = new LocalServiceFactory();

        var call = async () => await CreateClient(factory).GetDiagnosticFactsAsync(
            new GetDiagnosticFactsRequest { SourceId = "nobody" },
            AuthorizedHeaders);

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.NotFound);
    }

    [TestMethod]
    public async Task GetDiagnosticFacts_WithASourceId_ReturnsOnlyThatSource()
    {
        using var plugins = new PluginFolder();
        plugins.AddPlugin("fixture-good", "Good");
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path);

        var response = await CreateClient(factory).GetDiagnosticFactsAsync(
            new GetDiagnosticFactsRequest { SourceId = "fixture-good" },
            AuthorizedHeaders);

        response.Sources.Should().ContainSingle().Which.SourceId.Should().Be("fixture-good");
    }

    [TestMethod]
    public async Task ListPlugins_DisableRequestWrittenBeforeStart_IsDisabled()
    {
        using var plugins = new PluginFolder();
        plugins.AddPlugin("fixture-good", "Good");
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", "fixture-good", "shipped"));
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path, userPluginsDirectory: user.Path);

        var response = await CreateClient(factory).ListPluginsAsync(
            new ListPluginsRequest(),
            AuthorizedHeaders);

        var plugin = response.Plugins.Should().ContainSingle().Subject;
        plugin.Status.Should().Be(PluginStatus.Disabled);
        plugin.RejectionReason.Should().Be(PluginRejectionReason.Unspecified);
        plugin.PendingRequest.Should().Be(PluginRequestAction.Unspecified);
        plugin.FailedRequest.Should().Be(PluginRequestAction.Unspecified);
    }

    [TestMethod]
    public async Task ListPlugins_PluginThePreviousRunCrashedWhileLoading_IsSuspended()
    {
        using var plugins = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest("fixture-view", ViewManifest("fixture-view"));
        File.WriteAllText(
            Path.Combine(user.Path, "activity.json"),
            """{ "loading": { "origin": "installed", "id": "fixture-view" }, "calls": [] }""");
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path, userPluginsDirectory: user.Path);

        var response = await CreateClient(factory).ListPluginsAsync(new ListPluginsRequest(), AuthorizedHeaders);

        var plugin = response.Plugins.Should().ContainSingle().Subject;
        plugin.Status.Should().Be(PluginStatus.Suspended);
        plugin.RejectionReason.Should().Be(PluginRejectionReason.Unspecified);
        plugin.UiModule.Should().BeEmpty("the interface of a suspended plugin is never shown");
        plugin.DisabledForRecovery.Should().BeFalse();
    }

    [TestMethod]
    public async Task ListPlugins_StartWithoutTheInstalledPlugins_TellsWhichOnesAreDisabledForRecovery()
    {
        using var plugins = new PluginFolder();
        plugins.AddManifest("fixture-clock", ViewManifest("fixture-clock"));
        using var user = new UserPluginFolder();
        user.Installed.AddManifest("fixture-view", ViewManifest("fixture-view"));
        user.WriteRequestsFile("""{ "requests": [], "recovery": "rec-5e1f" }""");
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path, userPluginsDirectory: user.Path);

        var response = await CreateClient(factory).ListPluginsAsync(new ListPluginsRequest(), AuthorizedHeaders);

        response.Plugins.Select(plugin => (plugin.Id, plugin.Origin, plugin.Status, plugin.DisabledForRecovery)).Should().Equal(
            ("fixture-clock", PluginOrigin.Shipped, PluginStatus.Loaded, false),
            ("fixture-view", PluginOrigin.Installed, PluginStatus.Disabled, true));
    }

    [TestMethod]
    public async Task ListPlugins_PluginTheUserDisabled_IsNotDisabledForRecovery()
    {
        using var plugins = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest("fixture-view", ViewManifest("fixture-view"));
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", "fixture-view", "installed"));
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path, userPluginsDirectory: user.Path);

        var response = await CreateClient(factory).ListPluginsAsync(new ListPluginsRequest(), AuthorizedHeaders);

        var plugin = response.Plugins.Should().ContainSingle().Subject;
        plugin.Status.Should().Be(PluginStatus.Disabled);
        plugin.DisabledForRecovery.Should().BeFalse();
    }

    [TestMethod]
    public async Task ListPlugins_PluginDisabledForRecoveryThenEnabledAgain_IsLoadedWithoutTheMention()
    {
        using var plugins = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest("fixture-view", ViewManifest("fixture-view"));
        user.WriteRequestsFile($$"""{ "requests": [{{UserPluginFolder.Request("r1", "enable", "fixture-view", "installed")}}], "recovery": "rec-5e1f" }""");
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path, userPluginsDirectory: user.Path);

        var response = await CreateClient(factory).ListPluginsAsync(new ListPluginsRequest(), AuthorizedHeaders);

        var plugin = response.Plugins.Should().ContainSingle().Subject;
        plugin.Status.Should().Be(PluginStatus.Loaded);
        plugin.DisabledForRecovery.Should().BeFalse();
    }

    [TestMethod]
    public async Task ListPlugins_InstalledPluginWithTheIdOfAShippedOne_IsRejectedAsDuplicateOfShipped()
    {
        using var plugins = new PluginFolder();
        plugins.AddManifest("fixture-view", ViewManifest("fixture-view"));
        using var user = new UserPluginFolder();
        user.Installed.AddManifest("fixture-view", ViewManifest("fixture-view"));
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path, userPluginsDirectory: user.Path);

        var response = await CreateClient(factory).ListPluginsAsync(
            new ListPluginsRequest(),
            AuthorizedHeaders);

        response.Plugins.Select(plugin => (plugin.Origin, plugin.Status, plugin.RejectionReason)).Should().Equal(
            (PluginOrigin.Shipped, PluginStatus.Loaded, PluginRejectionReason.Unspecified),
            (PluginOrigin.Installed, PluginStatus.Rejected, PluginRejectionReason.DuplicateOfShipped));
    }

    [TestMethod]
    public async Task ListPlugins_StrayCopyWithTheIdOfAnotherInstalledPlugin_ShowsNoneOfItsRequests()
    {
        using var plugins = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest("fixture-good", ViewManifest("fixture-good"));
        user.Installed.AddManifest("fixture-good-copy", ViewManifest("fixture-good"));
        user.WriteRequests(UserPluginFolder.InstallRequest("r1", "fixture-good", GONE_STAGED_NAME, UserPluginFolder.UNKNOWN_SHA256));
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path, userPluginsDirectory: user.Path);
        var client = CreateClient(factory);
        await client.ListPluginsAsync(new ListPluginsRequest(), AuthorizedHeaders);
        user.WriteRequests(
            UserPluginFolder.InstallRequest("r1", "fixture-good", GONE_STAGED_NAME, UserPluginFolder.UNKNOWN_SHA256),
            UserPluginFolder.Request("r2", "disable", "fixture-good", "installed"));

        var response = await client.ListPluginsAsync(new ListPluginsRequest(), AuthorizedHeaders);

        response.Plugins.Select(plugin => (plugin.Folder, plugin.RejectionReason, plugin.PendingRequest, plugin.FailedRequest, plugin.FailedRequestReason))
            .Should().Equal(
                ("fixture-good", PluginRejectionReason.Unspecified, PluginRequestAction.Disable, PluginRequestAction.Install, PluginRequestFailure.PackageMissing),
                ("fixture-good-copy", PluginRejectionReason.DuplicateId, PluginRequestAction.Unspecified, PluginRequestAction.Unspecified, PluginRequestFailure.Unspecified));
    }

    [TestMethod]
    public async Task ListPlugins_GoodAndBrokenPlugins_StartsAndTellsWhichOneIsSetAside()
    {
        using var plugins = new PluginFolder();
        plugins.AddPlugin("fixture-good", "Good");
        plugins.AddPlugin("fixture-throws-on-configure", "ThrowsOnConfigure");
        plugins.AddManifest("fixture-no-ui", PluginFolder.Manifest("fixture-no-ui", assembly: null, provides: string.Empty, uiPart: ", \"ui\": { \"module\": \"views/index.js\" }"));
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path);

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
    public async Task ListPlugins_LoadedPlugin_ReturnsItsContractsWithTheirRoleAndState()
    {
        using var plugins = new PluginFolder();
        plugins.AddPlugin("fixture-good", "Good");
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path);

        var response = await CreateClient(factory).ListPluginsAsync(
            new ListPluginsRequest(),
            AuthorizedHeaders);

        response.Plugins.Should().ContainSingle().Which.Contracts.Select(use => (use.Contract, use.Version, use.Role, use.State)).Should().Equal(
            ("diagnostic-fact-source", 1u, ContractRole.Provided, ContractUseState.Supported),
            ("plugin", 1u, ContractRole.Required, ContractUseState.Supported));
    }

    [TestMethod]
    public async Task ListPlugins_PendingRequestForTheInstalledPlugin_IsNotShownOnTheShippedOne()
    {
        using var plugins = new PluginFolder();
        plugins.AddManifest("fixture-view", ViewManifest("fixture-view"));
        using var user = new UserPluginFolder();
        user.Installed.AddManifest("fixture-view", ViewManifest("fixture-view"));
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path, userPluginsDirectory: user.Path);
        var client = CreateClient(factory);
        await client.ListPluginsAsync(new ListPluginsRequest(), AuthorizedHeaders);
        user.WriteRequests(UserPluginFolder.Request("r1", "uninstall", "fixture-view", "installed"));

        var response = await client.ListPluginsAsync(new ListPluginsRequest(), AuthorizedHeaders);

        response.Plugins.Select(plugin => (plugin.Origin, plugin.PendingRequest)).Should().Equal(
            (PluginOrigin.Shipped, PluginRequestAction.Unspecified),
            (PluginOrigin.Installed, PluginRequestAction.Uninstall));
    }

    [TestMethod]
    public async Task ListPlugins_PluginWrittenForAnUnknownContractVersion_IsRejectedAsContractNotSupported()
    {
        using var plugins = new PluginFolder();
        plugins.AddPlugin(
            "fixture-good",
            "Good",
            PluginFolder.Manifest("fixture-good", "Voyager.PluginFixtures.Good.dll", provides: """{ "contract": "diagnostic-fact-source", "version": 2 }"""));
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path);

        var response = await CreateClient(factory).ListPluginsAsync(
            new ListPluginsRequest(),
            AuthorizedHeaders);

        var plugin = response.Plugins.Should().ContainSingle().Subject;
        plugin.Status.Should().Be(PluginStatus.Rejected);
        plugin.RejectionReason.Should().Be(PluginRejectionReason.ContractNotSupported);
        plugin.Contracts.Select(use => (use.Contract, use.Version, use.Role, use.State)).Should().Equal(
            ("diagnostic-fact-source", 2u, ContractRole.Provided, ContractUseState.Unknown),
            ("plugin", 1u, ContractRole.Required, ContractUseState.Supported));
    }

    [TestMethod]
    public async Task ListPlugins_RequestsWrittenAfterStart_ArePendingWithTheLastOneShown()
    {
        using var plugins = new PluginFolder();
        plugins.AddManifest("fixture-view", ViewManifest("fixture-view"));
        using var user = new UserPluginFolder();
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path, userPluginsDirectory: user.Path);
        var client = CreateClient(factory);
        await client.ListPluginsAsync(new ListPluginsRequest(), AuthorizedHeaders);
        user.WriteRequests(
            UserPluginFolder.Request("r1", "uninstall", "fixture-view", "shipped"),
            UserPluginFolder.Request("r2", "disable", "fixture-view", "shipped"));

        var response = await client.ListPluginsAsync(new ListPluginsRequest(), AuthorizedHeaders);

        var plugin = response.Plugins.Should().ContainSingle().Subject;
        plugin.Status.Should().Be(PluginStatus.Loaded);
        plugin.PendingRequest.Should().Be(PluginRequestAction.Disable);
        plugin.FailedRequest.Should().Be(PluginRequestAction.Unspecified);
    }

    [TestMethod]
    public async Task ListPlugins_ShippedAndInstalledPlugins_TellTheirOriginAndVerification()
    {
        using var plugins = new PluginFolder();
        plugins.AddManifest("fixture-view", ViewManifest("fixture-view"));
        using var user = new UserPluginFolder();
        user.Installed.AddManifest("fixture-other", ViewManifest("fixture-other"));
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path, userPluginsDirectory: user.Path);

        var response = await CreateClient(factory).ListPluginsAsync(
            new ListPluginsRequest(),
            AuthorizedHeaders);

        response.Plugins.Select(plugin => (plugin.Id, plugin.Origin, plugin.Verification, plugin.Status)).Should().Equal(
            ("fixture-view", PluginOrigin.Shipped, PluginVerification.GuaranteedByApplication, PluginStatus.Loaded),
            ("fixture-other", PluginOrigin.Installed, PluginVerification.Unverified, PluginStatus.Loaded));
    }

    [TestMethod]
    public async Task ListPlugins_ShippedPlugin_TellsWhichPartsItBrings()
    {
        using var plugins = new PluginFolder();
        plugins.AddPlugin(
            "fixture-good",
            "Good",
            PluginFolder.Manifest("fixture-good", "Voyager.PluginFixtures.Good.dll", uiPart: ", \"ui\": { \"module\": \"index.js\" }"));
        plugins.AddManifest("fixture-view", ViewManifest("fixture-view"));
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path);

        var response = await CreateClient(factory).ListPluginsAsync(
            new ListPluginsRequest(),
            AuthorizedHeaders);

        response.Plugins.Select(plugin => (plugin.Id, plugin.HasDotnet, plugin.HasUi)).Should().Equal(
            ("fixture-good", true, true),
            ("fixture-view", false, true));
    }

    [TestMethod]
    public async Task ListPlugins_UninstallRequestForAShippedPlugin_FailsAsNotAllowed()
    {
        using var plugins = new PluginFolder();
        plugins.AddManifest("fixture-view", ViewManifest("fixture-view"));
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "uninstall", "fixture-view", "shipped"));
        using var factory = new LocalServiceFactory(shippedPluginsDirectory: plugins.Path, userPluginsDirectory: user.Path);

        var response = await CreateClient(factory).ListPluginsAsync(
            new ListPluginsRequest(),
            AuthorizedHeaders);

        var plugin = response.Plugins.Should().ContainSingle().Subject;
        plugin.Status.Should().Be(PluginStatus.Loaded);
        plugin.FailedRequest.Should().Be(PluginRequestAction.Uninstall);
        plugin.FailedRequestReason.Should().Be(PluginRequestFailure.NotAllowed);
        plugin.PendingRequest.Should().Be(PluginRequestAction.Unspecified);
    }

    [TestMethod]
    public async Task ListPlugins_WithoutLocalToken_IsRefused()
    {
        using var factory = new LocalServiceFactory();

        var call = async () => await CreateClient(factory).ListPluginsAsync(new ListPluginsRequest());

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }

    [TestMethod]
    public async Task ListPlugins_WithoutPluginFolder_ListsNothingAndTheServiceStarts()
    {
        using var factory = new LocalServiceFactory();

        var response = await CreateClient(factory).ListPluginsAsync(
            new ListPluginsRequest(),
            AuthorizedHeaders);

        response.Plugins.Should().BeEmpty();
    }

    #endregion Public Methods
}
