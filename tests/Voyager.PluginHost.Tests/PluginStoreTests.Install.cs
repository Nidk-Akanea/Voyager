using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NodaTime;

namespace Voyager.PluginHost.Tests;

public sealed partial class PluginStoreTests
{
    #region Fields

    private const string STAGED_NAME = "c0ffee01c0ffee01c0ffee01c0ffee01.voyagerplugin";
    private const string GONE_STAGED_NAME = "60e60e60e60e60e60e60e60e60e60e60.voyagerplugin";
    private const string PACKAGE_NAME = "travel-plugin.voyagerplugin";
    private const string OTHER_ID = "fixture-other";
    private const string GOOD_ID = "fixture-good";

    private static readonly Instant _now = Instant.FromUtc(2026, 3, 14, 9, 30);

    #endregion Fields

    #region Private Methods

    // Stages a package and writes the request to install it, with the fingerprint of the staged copy.
    private static string StageAndRequest(UserPluginFolder user, List<PackageEntry> entries, string pluginId = VIEW_ID, string packageName = PACKAGE_NAME)
    {
        var package = TestPackage.Stage(user, STAGED_NAME, entries);
        user.WriteRequests(UserPluginFolder.InstallRequest("r1", pluginId, STAGED_NAME, TestPackage.Sha256Of(package), packageName));
        return package;
    }

    private static string InstalledManifest(UserPluginFolder user, string pluginId = VIEW_ID) =>
        File.ReadAllText(Path.Combine(user.Installed.Path, pluginId, "plugin.json"));

    private static PluginRequestError InstallFailure(PluginRequestFailure reason) => new(PluginRequestAction.Install, reason);

    private static List<PackageEntry> RefusedPackage(string scenario) => scenario switch
    {
        "forbidden entry" => [.. TestPackage.View(VIEW_ID), PackageEntry.Text("../escaped.txt", "Traveler A")],
        "contract not supported" => TestPackage.View(VIEW_ID, requires: """{ "contract": "plugin", "version": 2 }"""),
        "invalid manifest" => [PackageEntry.Text("plugin.json", "{}"), PackageEntry.Text($"ui/{TestPackage.UI_MODULE}", "export default {};")],
        "declared file missing" => [PackageEntry.Text("plugin.json", TestPackage.Manifest(VIEW_ID))],
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "No such scenario."),
    };

    // A file made long ago: created and last written then.
    private static void MakeOld(string file, Duration age) => SetTimes(file, created: _now - age, written: _now - age);

    private static void SetTimes(string file, Instant created, Instant written)
    {
        File.SetCreationTimeUtc(file, created.ToDateTimeUtc());
        File.SetLastWriteTimeUtc(file, written.ToDateTimeUtc());
    }

    private static List<PackageEntry> ViewWithData() =>
        [.. TestPackage.View(VIEW_ID), PackageEntry.Text("data/rates.csv", "EUR;1.00")];

    #endregion Private Methods

    #region Public Methods

    [TestMethod]
    public void ApplyRequests_InstallANewPlugin_PutsItsFilesInTheInstalledFolder()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        StageAndRequest(user, TestPackage.View(VIEW_ID, "1.3.0"));

        Applied(shipped, user);

        InstalledManifest(user).Should().Contain("\"1.3.0\"");
        File.Exists(Path.Combine(user.Installed.Path, VIEW_ID, "ui", TestPackage.UI_MODULE)).Should().BeTrue();
    }

    [TestMethod]
    public void ApplyRequests_InstallANewPlugin_RemembersThePackageAndTheDateAndEnablesIt()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var package = StageAndRequest(user, TestPackage.View(VIEW_ID));
        var sha256 = TestPackage.Sha256Of(package);

        var store = Applied(shipped, user);

        store.Find(_installedView).Should().Be(new PluginRecord(
            _installedView,
            Enabled: true,
            InstalledAt: _now,
            PackageName: PACKAGE_NAME,
            PackageSha256: sha256,
            LastError: null));
    }

    [TestMethod]
    public void ApplyRequests_InstallFromAFileWithControlCharactersInItsName_KeepsTheNameCleaned()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        StageAndRequest(user, TestPackage.View(VIEW_ID), packageName: "Traveler A\\u202E\\nplugin.voyagerplugin");

        var store = Applied(shipped, user);

        store.Find(_installedView)!.PackageName.Should().Be("Traveler Aplugin.voyagerplugin");
    }

    [TestMethod]
    public void ApplyRequests_InstallFromAFileWithALongNameEndingInAnEmoji_StillWritesTheState()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        StageAndRequest(user, TestPackage.View(VIEW_ID), packageName: new string('a', 98) + "\\uD83C\\uDF0D" + new string('b', 50));

        var apply = () => Applied(shipped, user);

        apply.Should().NotThrow();
        user.ReadState()["appliedRequests"]!.AsArray().Select(id => id!.GetValue<string>()).Should().Equal("r1");
    }

    [TestMethod]
    public void ApplyRequests_InstallAPlugin_DeletesThePreparedCopyAndLeavesNoExtractionFolder()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        StageAndRequest(user, TestPackage.View(VIEW_ID));

        Applied(shipped, user);

        Directory.EnumerateFileSystemEntries(user.StagingDirectory).Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyRequests_ReplaceAnInstalledPlugin_PutsTheNewVersionAndLeavesNothingOfTheOld()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, TestPackage.Manifest(VIEW_ID, "1.0.0"));
        File.WriteAllText(Path.Combine(user.Installed.Path, VIEW_ID, "old-notes.pdb"), "version 1");
        StageAndRequest(user, TestPackage.View(VIEW_ID, "2.0.0"));

        Applied(shipped, user);

        InstalledManifest(user).Should().Contain("\"2.0.0\"");
        File.Exists(Path.Combine(user.Installed.Path, VIEW_ID, "old-notes.pdb")).Should().BeFalse();
        FoldersOf(user.Installed.Path).Should().Equal(VIEW_ID);
    }

    [TestMethod]
    public void ApplyRequests_ReplaceADisabledPlugin_KeepsItDisabled()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, TestPackage.Manifest(VIEW_ID, "1.0.0"));
        user.WriteRequests(UserPluginFolder.Request("r0", "disable", VIEW_ID, "installed"));
        Applied(shipped, user);
        StageAndRequest(user, TestPackage.View(VIEW_ID, "2.0.0"));

        var store = Applied(shipped, user);

        store.Find(_installedView)!.Enabled.Should().BeFalse();
        store.Find(_installedView)!.InstalledAt.Should().Be(_now);
        InstalledManifest(user).Should().Contain("\"2.0.0\"");
    }

    [TestMethod]
    public void ApplyRequests_PreparedCopyChangedSinceTheConfirmation_IsKeptAsPackageChangedAndInstallsNothing()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID));
        user.WriteRequests(UserPluginFolder.InstallRequest("r1", VIEW_ID, STAGED_NAME, UserPluginFolder.UNKNOWN_SHA256));

        var store = Applied(shipped, user);

        store.Find(_installedView)!.LastError.Should().Be(InstallFailure(PluginRequestFailure.PackageChanged));
        Directory.Exists(Path.Combine(user.Installed.Path, VIEW_ID)).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("forbidden entry")]
    [DataRow("contract not supported")]
    [DataRow("invalid manifest")]
    [DataRow("declared file missing")]
    public void ApplyRequests_RefusedPackage_IsKeptAsPackageRefusedAndInstallsNothing(string scenario)
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        StageAndRequest(user, RefusedPackage(scenario));

        var store = Applied(shipped, user);

        store.Find(_installedView)!.LastError.Should().Be(InstallFailure(PluginRequestFailure.PackageRefused));
        Directory.Exists(Path.Combine(user.Installed.Path, VIEW_ID)).Should().BeFalse();
    }

    [TestMethod]
    public void ApplyRequests_PreparedCopyThatIsNoArchive_IsKeptAsPackageRefused()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var package = TestPackage.StageBytes(user, STAGED_NAME, [0x13, 0x37, 0xC0, 0xFF, 0xEE]);
        user.WriteRequests(UserPluginFolder.InstallRequest("r1", VIEW_ID, STAGED_NAME, TestPackage.Sha256Of(package)));

        var store = Applied(shipped, user);

        store.Find(_installedView)!.LastError.Should().Be(InstallFailure(PluginRequestFailure.PackageRefused));
    }

    [TestMethod]
    public void ApplyRequests_PackageThatHoldsAnotherPlugin_IsKeptAsPackageRefusedAndInstallsNeither()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        StageAndRequest(user, TestPackage.View(OTHER_ID), pluginId: VIEW_ID);

        var store = Applied(shipped, user);

        store.Find(_installedView)!.LastError.Should().Be(InstallFailure(PluginRequestFailure.PackageRefused));
        Directory.Exists(user.Installed.Path).Should().BeFalse();
    }

    [TestMethod]
    public void ApplyRequests_RefusedReplacement_LeavesTheInstalledVersion()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, TestPackage.Manifest(VIEW_ID, "1.0.0"));
        StageAndRequest(user, RefusedPackage("forbidden entry"));

        Applied(shipped, user);

        InstalledManifest(user).Should().Contain("\"1.0.0\"");
        FoldersOf(user.Installed.Path).Should().Equal(VIEW_ID);
    }

    [TestMethod]
    [DataRow("forbidden entry")]
    [DataRow("contract not supported")]
    public void ApplyRequests_RefusedPackage_LeavesNoExtractionFolder(string scenario)
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        StageAndRequest(user, RefusedPackage(scenario));

        Applied(shipped, user);

        Directory.EnumerateDirectories(user.StagingDirectory).Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyRequests_InstallAsAShippedPlugin_IsKeptAsNotAllowedAndInstallsNothing()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID));
        user.WriteRequests($$"""{ "id": "r1", "action": "install", "pluginId": "{{VIEW_ID}}", "origin": "shipped", "package": "{{STAGED_NAME}}", "sha256": "{{TestPackage.Sha256Of(package)}}", "packageName": "{{PACKAGE_NAME}}" }""");

        var store = Applied(shipped, user);

        store.Find(_shippedView)!.LastError.Should().Be(InstallFailure(PluginRequestFailure.NotAllowed));
        Directory.Exists(Path.Combine(shipped.Path, VIEW_ID)).Should().BeFalse();
        Directory.Exists(Path.Combine(user.Installed.Path, VIEW_ID)).Should().BeFalse();
    }

    [TestMethod]
    public void ApplyRequests_LeftoverOfAReplacementWithoutItsPlugin_IsPutBack()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(".replacing-fixture-view-0123456789abcdef0123456789abcdef", TestPackage.Manifest(VIEW_ID, "1.0.0"));

        Applied(shipped, user);

        FoldersOf(user.Installed.Path).Should().Equal(VIEW_ID);
        InstalledManifest(user).Should().Contain("\"1.0.0\"");
    }

    [TestMethod]
    public void ApplyRequests_LeftoverOfAReplacementWhoseNewVersionIsThere_IsDeleted()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(".replacing-fixture-view-0123456789abcdef0123456789abcdef", TestPackage.Manifest(VIEW_ID, "1.0.0"));
        user.Installed.AddManifest(VIEW_ID, TestPackage.Manifest(VIEW_ID, "2.0.0"));

        Applied(shipped, user);

        FoldersOf(user.Installed.Path).Should().Equal(VIEW_ID);
        InstalledManifest(user).Should().Contain("\"2.0.0\"");
    }

    [TestMethod]
    public void ApplyRequests_FoldersLeftByExtractionsAndExaminations_AreDeleted()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        Directory.CreateDirectory(Path.Combine(user.StagingDirectory, ".extract-0123456789abcdef", "ui"));
        Directory.CreateDirectory(Path.Combine(user.StagingDirectory, ".examine-0123456789abcdef", "data"));
        File.WriteAllText(Path.Combine(user.StagingDirectory, ".examine-0123456789abcdef", "data", "rates.csv"), "EUR;1.00");

        Applied(shipped, user);

        Directory.EnumerateDirectories(user.StagingDirectory).Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyRequests_OrphanCopyOlderThanOneHour_IsDeleted()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var orphan = TestPackage.Stage(user, TestPackage.NewStagedName(), TestPackage.View(VIEW_ID));
        MakeOld(orphan, Duration.FromMinutes(61));

        Applied(shipped, user);

        File.Exists(orphan).Should().BeFalse();
    }

    [TestMethod]
    public void ApplyRequests_RecentOrphanCopy_IsKept()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var orphan = TestPackage.Stage(user, TestPackage.NewStagedName(), TestPackage.View(VIEW_ID));
        MakeOld(orphan, Duration.FromMinutes(10));

        Applied(shipped, user);

        File.Exists(orphan).Should().BeTrue();
    }

    [TestMethod]
    public void ApplyRequests_InstallationThatFails_DeletesItsPreparedCopy()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID));
        user.WriteRequests(UserPluginFolder.InstallRequest("r1", VIEW_ID, STAGED_NAME, UserPluginFolder.UNKNOWN_SHA256));

        Applied(shipped, user);

        File.Exists(package).Should().BeFalse();
    }

    [TestMethod]
    public void ApplyRequests_OldFileThatIsNoPackage_IsKept()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        Directory.CreateDirectory(user.StagingDirectory);
        var other = Path.Combine(user.StagingDirectory, "notes.txt");
        File.WriteAllText(other, "Traveler A");
        MakeOld(other, Duration.FromDays(2));

        Applied(shipped, user);

        File.Exists(other).Should().BeTrue();
    }

    [TestMethod]
    public void ApplyRequests_Installations_LogNeitherTheFileNamesNorThePaths()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID));
        var refusedName = TestPackage.NewStagedName();
        TestPackage.Stage(user, refusedName, RefusedPackage("forbidden entry"));
        user.WriteRequests(
            UserPluginFolder.InstallRequest("r1", VIEW_ID, STAGED_NAME, TestPackage.Sha256Of(package), "Traveler-A-honeymoon.voyagerplugin"),
            UserPluginFolder.InstallRequest("r2", OTHER_ID, refusedName, UserPluginFolder.UNKNOWN_SHA256, "Child-C-school-trip.voyagerplugin"),
            UserPluginFolder.InstallRequest("r3", "fixture-gone", GONE_STAGED_NAME, UserPluginFolder.UNKNOWN_SHA256, "Traveler-B-weekend.voyagerplugin"));
        var logs = new CapturingLoggerProvider();

        Applied(shipped, user, logs.CreateFactory());

        logs.Messages.Should().NotBeEmpty();
        logs.Messages.Should().AllSatisfy(message =>
        {
            message.Should().NotContain("honeymoon");
            message.Should().NotContain("school-trip");
            message.Should().NotContain("weekend");
            message.Should().NotContain(".voyagerplugin");
            message.Should().NotContain(user.Path, "paths hold the user's account name");
        });
    }

    [TestMethod]
    public void ApplyRequests_InstallAPackage_LogsItsIdentifierVersionAndFingerprint()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var package = StageAndRequest(user, TestPackage.View(VIEW_ID, "1.3.0"));
        var sha256 = TestPackage.Sha256Of(package);
        var logs = new CapturingLoggerProvider();

        Applied(shipped, user, logs.CreateFactory());

        logs.Messages.Should().Contain(message => message.Contains(VIEW_ID) && message.Contains("1.3.0") && message.Contains(sha256));
    }

    [TestMethod]
    public void ApplyRequests_InstallAPackageBuiltFromATestPlugin_IsLoadedAsAnInstalledPlugin()
    {
        using var source = new PluginFolder();
        source.AddPlugin(GOOD_ID, "Good");
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        StageAndRequest(user, TestPackage.FromFolder(Path.Combine(source.Path, GOOD_ID)), pluginId: GOOD_ID);
        var store = Applied(shipped, user);

        using var catalog = PluginCatalog.Load(store.Locations.Roots, ContractSupport.Application, store.DisabledPlugins, NullLoggerFactory.Instance);

        var plugin = catalog.Plugins.Should().ContainSingle().Subject;
        plugin.Id.Should().Be(GOOD_ID);
        plugin.Origin.Should().Be(PluginOrigin.Installed);
        plugin.Status.Should().Be(PluginStatus.Loaded);
        plugin.Verification.Should().Be(PluginVerification.Unverified);
        catalog.Registry.DiagnosticFactSources.Should().ContainSingle();
    }

    [TestMethod]
    [DataRow(@"C:\\Users\\Traveler A\\Downloads\\x.voyagerplugin", DisplayName = "Windows path")]
    [DataRow(@"\\\\nas\\Traveler A\\x.voyagerplugin", DisplayName = "network path")]
    [DataRow("/home/traveler-a/Downloads/x.voyagerplugin", DisplayName = "Unix path")]
    [DataRow("C:x.voyagerplugin", DisplayName = "drive without a folder")]
    public void ApplyRequests_InstallFromAFileGivenWithItsPath_KeepsOnlyTheFileName(string packageName)
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        StageAndRequest(user, TestPackage.View(VIEW_ID), packageName: packageName);

        var store = Applied(shipped, user);

        store.Find(_installedView)!.PackageName.Should().Be("x.voyagerplugin");
    }

    [TestMethod]
    public void ApplyRequests_PreparedCopyWithADamagedCentralDirectory_IsKeptAsPackageRefused()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID));
        TestPackage.DamageCentralDirectory(package);
        user.WriteRequests(UserPluginFolder.InstallRequest("r1", VIEW_ID, STAGED_NAME, TestPackage.Sha256Of(package)));

        var store = Applied(shipped, user);

        store.Find(_installedView)!.LastError.Should().Be(InstallFailure(PluginRequestFailure.PackageRefused));
        Directory.Exists(Path.Combine(user.Installed.Path, VIEW_ID)).Should().BeFalse();
    }

    [TestMethod]
    public void ApplyRequests_PreparedCopyWithADamagedCentralDirectory_StillAppliesTheOtherRequests()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(OTHER_ID, TestPackage.Manifest(OTHER_ID));
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID));
        TestPackage.DamageCentralDirectory(package);
        user.WriteRequests(
            UserPluginFolder.InstallRequest("r1", VIEW_ID, STAGED_NAME, TestPackage.Sha256Of(package)),
            UserPluginFolder.Request("r2", "disable", OTHER_ID, "shipped"));

        var store = Applied(shipped, user);

        store.DisabledPlugins.Should().BeEquivalentTo([new PluginKey(PluginOrigin.Shipped, OTHER_ID)]);
        user.ReadState()["appliedRequests"]!.AsArray().Select(id => id!.GetValue<string>()).Should().Equal("r1", "r2");
    }

    [TestMethod]
    public void ApplyRequests_PackageThatAnnouncesMoreThanAnyDiskHolds_IsKeptAsNotEnoughSpaceAndInstallsNothing()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var package = TestPackage.WriteRaw(Path.Combine(user.StagingDirectory, STAGED_NAME),
        [
            RawPackageEntry.Text("plugin.json", TestPackage.Manifest(VIEW_ID)),
            RawPackageEntry.Text($"ui/{TestPackage.UI_MODULE}", "export default {};"),
            new RawPackageEntry("data/tiles-a.mbtiles", [1, 2, 3], AnnouncedLength: 0x6000_0000_0000_0000),
            new RawPackageEntry("data/tiles-b.mbtiles", [4, 5, 6], AnnouncedLength: 0x6000_0000_0000_0000),
        ]);
        user.WriteRequests(UserPluginFolder.InstallRequest("r1", VIEW_ID, STAGED_NAME, TestPackage.Sha256Of(package)));

        var store = Applied(shipped, user);

        store.Find(_installedView)!.LastError.Should().Be(InstallFailure(PluginRequestFailure.NotEnoughSpace));
        Directory.Exists(Path.Combine(user.Installed.Path, VIEW_ID)).Should().BeFalse();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ApplyRequests_StateThatCannotBeWritten_KeepsThePreparedCopy()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var package = StageAndRequest(user, TestPackage.View(VIEW_ID));
        user.WriteStateFile("""{ "plugins": [], "appliedRequests": [] }""");
        File.SetAttributes(user.StateFile, FileAttributes.ReadOnly);

        Applied(shipped, user);

        File.Exists(package).Should().BeTrue();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ApplyRequests_StartAfterAStateThatCouldNotBeWritten_InstallsAgainAndRecordsTheInstallation()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var package = StageAndRequest(user, ViewWithData());
        var sha256 = TestPackage.Sha256Of(package);
        user.WriteStateFile("""{ "plugins": [], "appliedRequests": [] }""");
        File.SetAttributes(user.StateFile, FileAttributes.ReadOnly);
        Applied(shipped, user);
        File.SetAttributes(user.StateFile, FileAttributes.Normal);

        var store = Applied(shipped, user);

        store.Find(_installedView).Should().Be(new PluginRecord(
            _installedView,
            Enabled: true,
            InstalledAt: _now,
            PackageName: PACKAGE_NAME,
            PackageSha256: sha256,
            LastError: null));
        File.Exists(Path.Combine(user.Installed.Path, VIEW_ID, "data", "rates.csv")).Should().BeTrue();
        user.ReadState()["appliedRequests"]!.AsArray().Select(id => id!.GetValue<string>()).Should().Equal("r1");
        File.Exists(package).Should().BeFalse();
    }

    [TestMethod]
    // Linux keeps no creation time that a test can set: the copy would look as old as the file it was copied from.
    [OSCondition(OperatingSystems.Windows)]
    public void ApplyRequests_OrphanCopyOfAnOldFileCopiedRecently_IsKept()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var orphan = TestPackage.Stage(user, TestPackage.NewStagedName(), TestPackage.View(VIEW_ID));
        SetTimes(orphan, created: _now - Duration.FromMinutes(10), written: _now - Duration.FromDays(30));

        Applied(shipped, user);

        File.Exists(orphan).Should().BeTrue();
    }

    [TestMethod]
    public void ApplyRequests_InstallThatFailsOnTheFiles_IsKeptAsFileSystemError()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        StageAndRequest(user, TestPackage.View(VIEW_ID));
        // A file where the folder of the installed plugins should be: the plugin cannot be put in its place.
        File.WriteAllText(user.Installed.Path, "Traveler A");

        var store = Applied(shipped, user);

        store.Find(_installedView)!.LastError.Should().Be(InstallFailure(PluginRequestFailure.FileSystemError));
    }

    [TestMethod]
    public void ApplyRequests_InstallThatFailsOnTheFiles_LogsTheKindAndTheCodeOfTheErrorButNoPath()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        StageAndRequest(user, TestPackage.View(VIEW_ID));
        File.WriteAllText(user.Installed.Path, "Traveler A");
        var logs = new CapturingLoggerProvider();

        Applied(shipped, user, logs.CreateFactory());

        logs.Messages.Should().Contain(message => message.Contains(VIEW_ID) && message.Contains("IOException (0x"));
        logs.Messages.Should().AllSatisfy(message => message.Should().NotContain(user.Path, "paths hold the user's account name"));
    }

    #endregion Public Methods
}
