using AwesomeAssertions;

using Voyager.PluginHost.Packages;
using Voyager.Sdk;

namespace Voyager.PluginHost.Tests.Packages;

[TestClass]
public sealed class PluginPackagesTests
{
    #region Fields

    private const string VIEW_ID = "fixture-view";
    private const string STAGED_NAME = "a1b2c3d4e5f60718293a4b5c6d7e8f90.voyagerplugin";
    private const string ITINERARY_SOURCE = "itinerary-source";

    #endregion Fields

    #region Private Methods

    private static PluginPackages Packages(PluginFolder shipped, UserPluginFolder user, ContractSupport? contracts = null) =>
        new(new PluginLocations(shipped.Path, user.Path), contracts ?? ContractSupport.Application);

    #endregion Private Methods

    #region Public Methods

    [TestMethod]
    [DataRow("../a1b2c3d4e5f60718293a4b5c6d7e8f90.voyagerplugin", DisplayName = "parent folder")]
    [DataRow("staging/a1b2c3d4e5f60718293a4b5c6d7e8f90.voyagerplugin", DisplayName = "sub-folder")]
    [DataRow("C:\\Users\\a1b2c3d4e5f60718293a4b5c6d7e8f90.voyagerplugin", DisplayName = "absolute path")]
    [DataRow("..", DisplayName = "dot dot")]
    [DataRow("a1b2c3d4e5f60718293a4b5c6d7e8f90.zip", DisplayName = "other extension")]
    [DataRow("a1b2c3d4e5f60718293a4b5c6d7e8f90.voyagerplugin.exe", DisplayName = "extension then another")]
    [DataRow("a1b2c3d4e5f60718293a4b5c6d7e8f90.VOYAGERPLUGIN", DisplayName = "extension in upper case")]
    [DataRow(".voyagerplugin", DisplayName = "extension only")]
    [DataRow("A1B2C3D4E5F60718293A4B5C6D7E8F90.voyagerplugin", DisplayName = "hexadecimal in upper case")]
    [DataRow("a1b2c3d4e5f60718293a4b5c6d7e8f9.voyagerplugin", DisplayName = "31 characters")]
    [DataRow("a1b2c3d4e5f60718293a4b5c6d7e8f901.voyagerplugin", DisplayName = "33 characters")]
    [DataRow("g1b2c3d4e5f60718293a4b5c6d7e8f90.voyagerplugin", DisplayName = "letter that is not hexadecimal")]
    [DataRow("a1b2c3d4-e5f6-0718-293a-4b5c6d7e8f90.voyagerplugin", DisplayName = "identifier with dashes")]
    [DataRow("travel-plugin.voyagerplugin", DisplayName = "name of the file the user chose")]
    [DataRow("Traveler A.voyagerplugin", DisplayName = "name with a space")]
    [DataRow("a1b2c3d4e5f60718293a4b5c6d7e8f90.voyagerplugin\n", DisplayName = "final line break")]
    [DataRow("", DisplayName = "empty")]
    public async Task ExamineAsync_NameThatIsNotAPreparedCopy_ThrowsArgumentException(string stagedName)
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        using var packages = Packages(shipped, user);

        var examine = () => packages.ExamineAsync(stagedName, CancellationToken.None);

        await examine.Should().ThrowAsync<ArgumentException>();
    }

    [TestMethod]
    public async Task ExamineAsync_NameThatDiffersOnlyByCaseFromAStagedCopy_ThrowsArgumentException()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID));
        using var packages = Packages(shipped, user);

        var examine = () => packages.ExamineAsync(STAGED_NAME.ToUpperInvariant(), CancellationToken.None);

        await examine.Should().ThrowAsync<ArgumentException>();
    }

    [TestMethod]
    public void IsValidStagedName_NameChosenAtRandomByTheDesktopApp_IsValid()
    {
        var valid = PluginPackages.IsValidStagedName(TestPackage.NewStagedName());

        valid.Should().BeTrue();
    }

    [TestMethod]
    public async Task ExamineAsync_NoUserDirectory_ThrowsInvalidOperationException()
    {
        using var shipped = new PluginFolder();
        using var packages = new PluginPackages(new PluginLocations(shipped.Path, UserDirectory: null), ContractSupport.Application);

        var examine = () => packages.ExamineAsync(STAGED_NAME, CancellationToken.None);

        await examine.Should().ThrowAsync<InvalidOperationException>();
    }

    [TestMethod]
    public async Task ExamineAsync_MissingCopy_ThrowsFileNotFoundException()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        Directory.CreateDirectory(user.StagingDirectory);
        using var packages = Packages(shipped, user);

        var examine = () => packages.ExamineAsync(STAGED_NAME, CancellationToken.None);

        await examine.Should().ThrowAsync<FileNotFoundException>();
    }

    [TestMethod]
    public async Task ExamineAsync_CancelledBeforeItStarts_ThrowsAndLeavesNoExaminationFolder()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID));
        using var packages = Packages(shipped, user);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var examine = () => packages.ExamineAsync(STAGED_NAME, cancellation.Token);

        await examine.Should().ThrowAsync<OperationCanceledException>();
        Directory.EnumerateDirectories(user.StagingDirectory, ".examine-*").Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExamineAsync_DamagedCentralDirectory_IsRefusedAsUnreadable()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID));
        TestPackage.DamageCentralDirectory(package);
        using var packages = Packages(shipped, user);

        var examination = await packages.ExamineAsync(STAGED_NAME, CancellationToken.None);

        examination.Refusal.Should().Be(PackageRefusal.Unreadable);
        examination.Manifest.Should().BeNull();
        examination.Sha256.Should().Be(TestPackage.Sha256Of(package));
    }

    [TestMethod]
    public async Task ExamineAsync_PackageWithDataAndInterfaceFiles_LeavesOnlyTheCopyInTheStagingFolder()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME,
        [
            .. TestPackage.View(VIEW_ID),
            PackageEntry.Text("data/rates.csv", "EUR;1.00"),
            PackageEntry.Text("i18n/fr/messages.ftl", "view-title = Carnet"),
        ]);
        using var packages = Packages(shipped, user);

        await packages.ExamineAsync(STAGED_NAME, CancellationToken.None);

        Directory.EnumerateFileSystemEntries(user.StagingDirectory).Should().Equal(package);
    }

    [TestMethod]
    public async Task ExamineAsync_TwoExaminationsAtOnce_BothGiveTheirPlugin()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var otherName = TestPackage.NewStagedName();
        TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID));
        TestPackage.Stage(user, otherName, TestPackage.View("fixture-other"));
        using var packages = Packages(shipped, user);

        var examinations = await Task.WhenAll(
            Task.Run(() => packages.ExamineAsync(STAGED_NAME, CancellationToken.None)),
            Task.Run(() => packages.ExamineAsync(otherName, CancellationToken.None)));

        examinations.Select(examination => (examination.Refusal, examination.Manifest!.Id))
            .Should().Equal((null, VIEW_ID), (null, "fixture-other"));
    }

    [TestMethod]
    public async Task ExamineAsync_ValidPackage_GivesItsFingerprintAndSize()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID));
        using var packages = Packages(shipped, user);

        var examination = await packages.ExamineAsync(STAGED_NAME, CancellationToken.None);

        examination.Sha256.Should().Be(TestPackage.Sha256Of(package));
        examination.Size.Should().Be(new FileInfo(package).Length);
    }

    [TestMethod]
    public async Task ExamineAsync_ValidPackage_GivesItsManifestAndIsUnverified()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID, "1.4.0"));
        using var packages = Packages(shipped, user);

        var examination = await packages.ExamineAsync(STAGED_NAME, CancellationToken.None);

        examination.Refusal.Should().BeNull();
        examination.Manifest!.Id.Should().Be(VIEW_ID);
        examination.Manifest.Version.Should().Be("1.4.0");
        PackageExamination.Verification.Should().Be(PluginVerification.Unverified);
        examination.DuplicateOfShipped.Should().BeFalse();
    }

    [TestMethod]
    public async Task ExamineAsync_RefusedPackage_GivesTheReasonAndTheFingerprint()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, [.. TestPackage.View(VIEW_ID), PackageEntry.Text("../escaped.txt", "Traveler A")]);
        using var packages = Packages(shipped, user);

        var examination = await packages.ExamineAsync(STAGED_NAME, CancellationToken.None);

        examination.Refusal.Should().Be(PackageRefusal.ForbiddenEntry);
        examination.Manifest.Should().BeNull();
        examination.Sha256.Should().Be(TestPackage.Sha256Of(package));
    }

    [TestMethod]
    public async Task ExamineAsync_IdentifierOfAShippedPlugin_IsADuplicateButNotRefused()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, TestPackage.Manifest(VIEW_ID));
        using var user = new UserPluginFolder();
        TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID));
        using var packages = Packages(shipped, user);

        var examination = await packages.ExamineAsync(STAGED_NAME, CancellationToken.None);

        examination.DuplicateOfShipped.Should().BeTrue();
        examination.Refusal.Should().BeNull();
    }

    [TestMethod]
    [DataRow(null, "1.0.0", PackageInstallKind.New, DisplayName = "nothing installed")]
    [DataRow("1.0.0", "2.0.0", PackageInstallKind.Upgrade, DisplayName = "newer major")]
    [DataRow("1.2.0", "1.10.0", PackageInstallKind.Upgrade, DisplayName = "newer minor, compared as numbers")]
    [DataRow("1.0.0-beta.2", "1.0.0", PackageInstallKind.Upgrade, DisplayName = "release after its pre-release")]
    [DataRow("2.0.0", "1.9.9", PackageInstallKind.Downgrade, DisplayName = "older")]
    [DataRow("1.0.0", "1.0.0-rc.1", PackageInstallKind.Downgrade, DisplayName = "pre-release of the installed version")]
    [DataRow("1.0.0", "1.0.0", PackageInstallKind.Reinstall, DisplayName = "same version")]
    [DataRow("1.0.0+build.7", "1.0.0+build.9", PackageInstallKind.Reinstall, DisplayName = "same version, other build")]
    public async Task ExamineAsync_InstalledVersion_SaysWhatInstallingWouldDo(string? installedVersion, string packageVersion, PackageInstallKind expected)
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        if (installedVersion is not null)
        {
            user.Installed.AddManifest(VIEW_ID, TestPackage.Manifest(VIEW_ID, installedVersion));
        }

        TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID, packageVersion));
        using var packages = Packages(shipped, user);

        var examination = await packages.ExamineAsync(STAGED_NAME, CancellationToken.None);

        examination.Kind.Should().Be(expected);
        examination.InstalledVersion.Should().Be(installedVersion);
    }

    [TestMethod]
    public async Task ExamineAsync_InstalledFolderWithAnUnreadableManifest_IsNew()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, "not a manifest");
        TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID));
        using var packages = Packages(shipped, user);

        var examination = await packages.ExamineAsync(STAGED_NAME, CancellationToken.None);

        examination.InstalledVersion.Should().BeNull();
        examination.Kind.Should().Be(PackageInstallKind.New);
    }

    [TestMethod]
    public async Task ExamineAsync_DeprecatedContract_IsAcceptedAndShowsTheDeprecation()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var contracts = new ContractSupport(
            [
                new ContractVersion(ContractVersions.PLUGIN, 1, ContractVersionState.Supported),
                new ContractVersion(ITINERARY_SOURCE, 1, ContractVersionState.Deprecated),
            ],
            [ContractVersions.PLUGIN]);
        TestPackage.Stage(user, STAGED_NAME,
        [
            PackageEntry.Text("plugin.json", TestPackage.Manifest(VIEW_ID, provides: $$"""{ "contract": "{{ITINERARY_SOURCE}}", "version": 1 }""")),
            PackageEntry.Text($"ui/{TestPackage.UI_MODULE}", "export default {};"),
        ]);
        using var packages = Packages(shipped, user, contracts);

        var examination = await packages.ExamineAsync(STAGED_NAME, CancellationToken.None);

        examination.Refusal.Should().BeNull();
        examination.Contracts.Should().Equal(
            new ContractUse(ITINERARY_SOURCE, 1, ContractRole.Provided, ContractUseState.Deprecated),
            new ContractUse(ContractVersions.PLUGIN, 1, ContractRole.Required, ContractUseState.Supported));
    }

    [TestMethod]
    public async Task ExamineAsync_ContractNotSupported_IsRefusedWithTheContracts()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID, requires: """{ "contract": "plugin", "version": 2 }"""));
        using var packages = Packages(shipped, user);

        var examination = await packages.ExamineAsync(STAGED_NAME, CancellationToken.None);

        examination.Refusal.Should().Be(PackageRefusal.ContractNotSupported);
        examination.Contracts.Should().Equal(new ContractUse(ContractVersions.PLUGIN, 2, ContractRole.Required, ContractUseState.Unknown));
    }

    [TestMethod]
    [DataRow(true, DisplayName = "accepted package")]
    [DataRow(false, DisplayName = "refused package")]
    public async Task ExamineAsync_AnyPackage_LeavesNoExaminationFolder(bool valid)
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        TestPackage.Stage(user, STAGED_NAME, valid
            ? TestPackage.View(VIEW_ID)
            : TestPackage.View(VIEW_ID, requires: """{ "contract": "plugin", "version": 2 }"""));
        using var packages = Packages(shipped, user);

        await packages.ExamineAsync(STAGED_NAME, CancellationToken.None);

        Directory.EnumerateDirectories(user.StagingDirectory).Should().BeEmpty();
    }

    [TestMethod]
    public async Task ExamineAsync_ValidPackage_InstallsNothingAndKeepsTheCopy()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var package = TestPackage.Stage(user, STAGED_NAME, TestPackage.View(VIEW_ID));
        using var packages = Packages(shipped, user);

        await packages.ExamineAsync(STAGED_NAME, CancellationToken.None);

        Directory.Exists(Path.Combine(user.Installed.Path, VIEW_ID)).Should().BeFalse();
        Directory.EnumerateFiles(user.Path, "*", SearchOption.AllDirectories).Should().Equal([package], "the copy is kept and nothing else is written");
    }

    #endregion Public Methods
}
