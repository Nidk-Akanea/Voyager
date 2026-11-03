using System.Diagnostics;

using AwesomeAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Voyager.PluginHost.Tests;

[TestClass]
public sealed class PluginStoreTests
{
    #region Fields

    private const string VIEW_ID = "fixture-view";

    private static readonly PluginKey _shippedView = new(PluginOrigin.Shipped, VIEW_ID);

    private static readonly PluginKey _installedView = new(PluginOrigin.Installed, VIEW_ID);

    #endregion Fields

    #region Private Methods

    // A plugin with only an interface part: no assembly is ever loaded, so its folder can always be removed.
    private static string ViewManifest(string id) =>
        PluginFolder.Manifest(id, assembly: null, provides: string.Empty, uiPart: ", \"ui\": { \"module\": \"index.js\" }");

    private static PluginStore Store(PluginFolder shipped, UserPluginFolder user, ILoggerFactory? loggerFactory = null) =>
        new(new PluginLocations(shipped.Path, user.Path), (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<PluginStore>());

    private static PluginStore Applied(PluginFolder shipped, UserPluginFolder user, ILoggerFactory? loggerFactory = null)
    {
        var store = Store(shipped, user, loggerFactory);
        store.ApplyRequests();
        return store;
    }

    // A junction, which Windows lets any user create, unlike a symbolic link.
    private static void CreateJunction(string link, string target)
    {
        using var mklink = Process.Start(new ProcessStartInfo("cmd", ["/c", "mklink", "/J", link, target])
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        })!;
        mklink.StandardOutput.ReadToEnd();
        mklink.WaitForExit();
        if (mklink.ExitCode != 0 || !new DirectoryInfo(link).Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidOperationException("The junction of the test could not be created.");
        }
    }

    private static string[] FoldersOf(string directory) =>
        [.. Directory.EnumerateDirectories(directory).Select(folder => Path.GetFileName(folder))];

    #endregion Private Methods

    #region Public Methods

    [TestMethod]
    public void ApplyRequests_AppliedRequestStillInTheFile_IsNotAppliedAgainAtTheNextStart()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.WriteRequests(UserPluginFolder.Request("r1", "uninstall", VIEW_ID, "installed"));
        Applied(shipped, user);
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));

        Applied(shipped, user);

        Directory.Exists(Path.Combine(user.Installed.Path, VIEW_ID)).Should().BeTrue();
    }

    [TestMethod]
    public void ApplyRequests_AppliedRequestThatLeftTheFile_IsForgotten()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"));
        Applied(shipped, user);
        user.WriteRequests();

        Applied(shipped, user);

        user.ReadState()["appliedRequests"]!.AsArray().Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyRequests_DisableAShippedPlugin_LeavesTheInstalledOneWithTheSameIdEnabled()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"));

        var store = Applied(shipped, user);

        store.DisabledPlugins.Should().BeEquivalentTo([_shippedView]);
    }

    [TestMethod]
    [DataRow(PluginOrigin.Shipped, "shipped")]
    [DataRow(PluginOrigin.Installed, "installed")]
    public void ApplyRequests_DisablePlugin_DisablesIt(PluginOrigin origin, string originInFile)
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        (origin == PluginOrigin.Shipped ? shipped : user.Installed).AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, originInFile));

        var store = Applied(shipped, user);

        store.DisabledPlugins.Should().BeEquivalentTo([new PluginKey(origin, VIEW_ID)]);
        store.Find(new PluginKey(origin, VIEW_ID))!.Enabled.Should().BeFalse();
    }

    [TestMethod]
    public void ApplyRequests_DisableThenEnableInTheSameFile_EndsEnabled()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(
            UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"),
            UserPluginFolder.Request("r2", "enable", VIEW_ID, "shipped"));

        var store = Applied(shipped, user);

        store.DisabledPlugins.Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyRequests_EnableADisabledPluginAtTheNextStart_EnablesItAgain()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"));
        Applied(shipped, user);
        user.WriteRequests(UserPluginFolder.Request("r2", "enable", VIEW_ID, "shipped"));

        var store = Applied(shipped, user);

        store.DisabledPlugins.Should().BeEmpty();
        store.Find(_shippedView)!.Enabled.Should().BeTrue();
    }

    [TestMethod]
    public void ApplyRequests_FailedRequestThenAnAppliedOne_ClearsTheLastError()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(
            UserPluginFolder.Request("r1", "uninstall", VIEW_ID, "shipped"),
            UserPluginFolder.Request("r2", "disable", VIEW_ID, "shipped"));

        var store = Applied(shipped, user);

        store.Find(_shippedView).Should().Be(
            new PluginRecord(_shippedView, Enabled: false, InstalledAt: null, PackageName: null, PackageSha256: null, LastError: null));
    }

    [TestMethod]
    public void ApplyRequests_InstallRequest_IsKeptAsNotSupported()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.WriteRequests(UserPluginFolder.Request("r1", "install", VIEW_ID, "installed"));

        var store = Applied(shipped, user);

        store.Find(_installedView)!.LastError.Should().Be(new PluginRequestError(PluginRequestAction.Install, PluginRequestFailure.NotSupported));
        Directory.Exists(Path.Combine(user.Installed.Path, VIEW_ID)).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("""{ "id": "r1", "action": "disable", "pluginId": "Not Valid", "origin": "shipped" }""", DisplayName = "plugin id that is not an identifier")]
    [DataRow("""{ "id": "r1", "action": "disable", "pluginId": "..\\escape", "origin": "shipped" }""", DisplayName = "plugin id that leaves the folder")]
    [DataRow("""{ "id": "r1", "action": "explode", "pluginId": "fixture-other", "origin": "shipped" }""", DisplayName = "unknown action")]
    [DataRow("""{ "id": "r1", "action": 3, "pluginId": "fixture-other", "origin": "shipped" }""", DisplayName = "action as a number")]
    [DataRow("""{ "id": "r1", "action": "disable", "pluginId": "fixture-other", "origin": "borrowed" }""", DisplayName = "unknown origin")]
    [DataRow("""{ "action": "disable", "pluginId": "fixture-other", "origin": "shipped" }""", DisplayName = "no request id")]
    [DataRow("""{ "id": "r 1/..", "action": "disable", "pluginId": "fixture-other", "origin": "shipped" }""", DisplayName = "request id with forbidden characters")]
    [DataRow("""{ "id": "r1", "pluginId": "fixture-other", "origin": "shipped" }""", DisplayName = "no action")]
    [DataRow("""null""", DisplayName = "null entry")]
    public void ApplyRequests_InvalidEntry_IsIgnoredAndTheOthersApply(string invalidEntry)
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        shipped.AddManifest("fixture-other", ViewManifest("fixture-other"));
        using var user = new UserPluginFolder();
        user.WriteRequests(
            invalidEntry,
            UserPluginFolder.Request("r2", "disable", VIEW_ID, "shipped"));

        var store = Applied(shipped, user);

        store.DisabledPlugins.Should().BeEquivalentTo([_shippedView]);
    }

    [TestMethod]
    public void ApplyRequests_InvalidEntry_IsLogged()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(
            UserPluginFolder.Request("r1", "disable", "Not Valid", "shipped"),
            UserPluginFolder.Request("r2", "disable", VIEW_ID, "shipped"));
        var logs = new CapturingLoggerProvider();

        Applied(shipped, user, logs.CreateFactory());

        logs.Messages.Should().Contain("1 plugin requests are not valid and are ignored");
    }

    [TestMethod]
    public void ApplyRequests_LeftoverOfAnUnfinishedRemoval_IsDeleted()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(".removing-fixture-view-0123456789abcdef", ViewManifest(VIEW_ID));

        Applied(shipped, user);

        Directory.EnumerateDirectories(user.Installed.Path).Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyRequests_NoRequestsFile_ChangesNothingAndWritesTheState()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();

        var store = Applied(shipped, user);

        store.Records.Should().BeEmpty();
        File.Exists(user.StateFile).Should().BeTrue();
    }

    [TestMethod]
    public void ApplyRequests_NoUserDirectory_DoesNothing()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        var store = new PluginStore(new PluginLocations(shipped.Path, UserDirectory: null), NullLogger<PluginStore>.Instance);

        store.ApplyRequests();

        store.Records.Should().BeEmpty();
        store.DisabledPlugins.Should().BeEmpty();
        store.ReadPendingRequests().Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyRequests_PluginNotFound_RecordsNothing()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.WriteRequests(
            UserPluginFolder.Request("r1", "disable", "fixture-missing", "shipped"),
            UserPluginFolder.Request("r2", "uninstall", "fixture-missing", "installed"));

        var store = Applied(shipped, user);

        store.Records.Should().BeEmpty();
        user.ReadState()["plugins"]!.AsArray().Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyRequests_PluginNotFound_LogsTheFailure()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", "fixture-missing", "shipped"));
        var logs = new CapturingLoggerProvider();

        Applied(shipped, user, logs.CreateFactory());

        logs.Messages.Should().Contain("The request to Disable plugin fixture-missing (Shipped) failed: NotFound");
    }

    [TestMethod]
    public void ApplyRequests_Requests_LogsNoFolderPath()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.Installed.AddManifest("fixture-other", ViewManifest("fixture-other"));
        user.Installed.AddManifest(".removing-fixture-old-0123456789abcdef", ViewManifest("fixture-old"));
        user.WriteRequests(
            UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"),
            UserPluginFolder.Request("r2", "uninstall", VIEW_ID, "shipped"),
            UserPluginFolder.Request("r3", "uninstall", "fixture-other", "installed"),
            UserPluginFolder.Request("r4", "enable", "fixture-missing", "installed"),
            UserPluginFolder.Request("r5", "disable", "Not Valid", "installed"));
        var logs = new CapturingLoggerProvider();

        Applied(shipped, user, logs.CreateFactory());

        logs.Messages.Should().NotBeEmpty();
        logs.Messages.Should().AllSatisfy(message =>
        {
            message.Should().NotContain(user.Path, "paths hold the user's account name");
            message.Should().NotContain(shipped.Path, "paths hold the user's account name");
            message.Should().NotContain("Not Valid");
        });
    }

    [TestMethod]
    public void ApplyRequests_State_IsWrittenInTheFormatOfStateJson()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(
            UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"),
            UserPluginFolder.Request("r2", "uninstall", VIEW_ID, "shipped"));

        Applied(shipped, user);

        var state = user.ReadState();
        var plugin = state["plugins"]!.AsArray().Should().ContainSingle().Subject!.AsObject();
        plugin["id"]!.GetValue<string>().Should().Be(VIEW_ID);
        plugin["origin"]!.GetValue<string>().Should().Be("shipped");
        plugin["enabled"]!.GetValue<bool>().Should().BeFalse();
        plugin["lastError"]!["action"]!.GetValue<string>().Should().Be("uninstall");
        plugin["lastError"]!["reason"]!.GetValue<string>().Should().Be("notAllowed");
        state["appliedRequests"]!.AsArray().Select(id => id!.GetValue<string>()).Should().Equal("r1", "r2");
    }

    [TestMethod]
    public void ApplyRequests_State_LeavesNoTemporaryFile()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"));
        Applied(shipped, user);
        user.WriteRequests(UserPluginFolder.Request("r2", "enable", VIEW_ID, "shipped"));

        Applied(shipped, user);

        File.Exists(user.StateFile).Should().BeTrue();
        Directory.EnumerateFiles(user.Path, "*.tmp").Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyRequests_StateOfAPreviousStart_KeepsADisabledPluginDisabled()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"));
        Applied(shipped, user);
        user.WriteRequests();

        var store = Applied(shipped, user);

        store.DisabledPlugins.Should().BeEquivalentTo([_shippedView]);
    }

    [TestMethod]
    public void ApplyRequests_StateOfAPreviousStart_KeepsTheLastError()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "uninstall", VIEW_ID, "shipped"));
        Applied(shipped, user);

        var store = Applied(shipped, user);

        store.Find(_shippedView)!.LastError.Should().Be(new PluginRequestError(PluginRequestAction.Uninstall, PluginRequestFailure.NotAllowed));
    }

    [TestMethod]
    [DataRow("this is not json", DisplayName = "not JSON")]
    [DataRow("""[ { "id": "r1" } ]""", DisplayName = "an array instead of an object")]
    [DataRow("""{ "requests": "disable everything" }""", DisplayName = "requests that are not a list")]
    public void ApplyRequests_UnreadableRequestsFile_DoesNotThrowAndKeepsTheState(string content)
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"));
        Applied(shipped, user);
        user.WriteRequestsFile(content);
        var store = Store(shipped, user);

        var apply = store.ApplyRequests;

        apply.Should().NotThrow();
        store.DisabledPlugins.Should().BeEquivalentTo([_shippedView]);
    }

    [TestMethod]
    public void ApplyRequests_RequestsFileLargerThanOneMegabyte_IsNotRead()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        var padding = new string(' ', 1024 * 1024);
        user.WriteRequestsFile($$"""{ "requests": [{{UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped")}}] }{{padding}}""");
        var store = Store(shipped, user);

        var apply = store.ApplyRequests;

        apply.Should().NotThrow();
        store.DisabledPlugins.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("this is not json", DisplayName = "not JSON")]
    [DataRow("""{ "plugins": [ { "id": "fixture-view", "origin": "lent", "enabled": false } ] }""", DisplayName = "unknown origin")]
    [DataRow("""{ "plugins": "all of them" }""", DisplayName = "plugins that are not a list")]
    public void ApplyRequests_UnreadableStateFile_DoesNotThrowAndEnablesEveryPlugin(string content)
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteStateFile(content);
        var store = Store(shipped, user);

        var apply = store.ApplyRequests;

        apply.Should().NotThrow();
        store.DisabledPlugins.Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyRequests_UnreadableStateFile_StillAppliesTheRequests()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteStateFile("this is not json");
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"));

        var store = Applied(shipped, user);

        store.DisabledPlugins.Should().BeEquivalentTo([_shippedView]);
    }

    [TestMethod]
    public void ApplyRequests_UninstallADisabledInstalledPlugin_ForgetsIt()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "installed"));
        Applied(shipped, user);
        user.WriteRequests(UserPluginFolder.Request("r2", "uninstall", VIEW_ID, "installed"));

        var store = Applied(shipped, user);

        store.Find(_installedView).Should().BeNull();
        store.DisabledPlugins.Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyRequests_UninstallAnInstalledPlugin_RemovesItsFolderAndLeavesNothingBehind()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.Installed.AddManifest("fixture-other", ViewManifest("fixture-other"));
        user.WriteRequests(UserPluginFolder.Request("r1", "uninstall", VIEW_ID, "installed"));

        Applied(shipped, user);

        Directory.EnumerateDirectories(user.Installed.Path).Select(Path.GetFileName).Should().Equal("fixture-other");
    }

    [TestMethod]
    public void ApplyRequests_UninstallAnInstalledPlugin_LeavesTheShippedOneWithTheSameId()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.WriteRequests(UserPluginFolder.Request("r1", "uninstall", VIEW_ID, "installed"));

        Applied(shipped, user);

        Directory.Exists(Path.Combine(shipped.Path, VIEW_ID)).Should().BeTrue();
        Directory.Exists(Path.Combine(user.Installed.Path, VIEW_ID)).Should().BeFalse();
    }

    [TestMethod]
    public void ApplyRequests_UninstallAShippedPlugin_IsKeptAsNotAllowedAndRemovesNothing()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "uninstall", VIEW_ID, "shipped"));

        var store = Applied(shipped, user);

        store.Find(_shippedView)!.LastError.Should().Be(new PluginRequestError(PluginRequestAction.Uninstall, PluginRequestFailure.NotAllowed));
        store.Find(_shippedView)!.Enabled.Should().BeTrue();
        Directory.Exists(Path.Combine(shipped.Path, VIEW_ID)).Should().BeTrue();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ApplyRequests_UninstallAPluginWhoseFileIsLocked_IsKeptAsFileSystemErrorAndLeavesThePlugin()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.WriteRequests(UserPluginFolder.Request("r1", "uninstall", VIEW_ID, "installed"));
        using var locked = File.Open(Path.Combine(user.Installed.Path, VIEW_ID, "plugin.json"), FileMode.Open, FileAccess.Read, FileShare.None);

        var store = Applied(shipped, user);

        store.Find(_installedView)!.LastError.Should().Be(new PluginRequestError(PluginRequestAction.Uninstall, PluginRequestFailure.FileSystemError));
        Directory.EnumerateDirectories(user.Installed.Path).Select(Path.GetFileName).Should().Equal(VIEW_ID);
    }

    [TestMethod]
    public void ApplyRequests_UnreadableRequestsFile_KeepsTheAppliedRequestsInTheState()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"));
        Applied(shipped, user);
        user.WriteRequestsFile("this is not json");

        Applied(shipped, user);

        user.ReadState()["appliedRequests"]!.AsArray().Select(id => id!.GetValue<string>()).Should().Equal("r1");
    }

    [TestMethod]
    public void ApplyRequests_AppliedRequestBackInTheFileAfterAnUnreadableStart_IsNotAppliedAgain()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.WriteRequests(UserPluginFolder.Request("r1", "uninstall", VIEW_ID, "installed"));
        Applied(shipped, user);
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.WriteRequestsFile("this is not json");
        Applied(shipped, user);
        user.WriteRequests(UserPluginFolder.Request("r1", "uninstall", VIEW_ID, "installed"));

        Applied(shipped, user);

        Directory.Exists(Path.Combine(user.Installed.Path, VIEW_ID)).Should().BeTrue();
    }

    [TestMethod]
    public void ApplyRequests_SecondStart_ReadsTheWrittenStateBackAndLeavesNoTemporaryFile()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(
            UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"),
            UserPluginFolder.Request("r2", "uninstall", VIEW_ID, "shipped"));
        Applied(shipped, user);
        user.WriteRequests();

        var store = Applied(shipped, user);

        store.Find(_shippedView).Should().Be(new PluginRecord(
            _shippedView,
            Enabled: false,
            InstalledAt: null,
            PackageName: null,
            PackageSha256: null,
            LastError: new PluginRequestError(PluginRequestAction.Uninstall, PluginRequestFailure.NotAllowed)));
        Directory.EnumerateFiles(user.Path, "*.tmp").Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyRequests_TemporaryStateFileLeftByAnotherWriter_StillWritesTheState()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"));
        File.WriteAllText(user.StateFile + ".tmp", "{ \"plugins\": [");

        Applied(shipped, user);

        user.ReadState()["appliedRequests"]!.AsArray().Select(id => id!.GetValue<string>()).Should().Equal("r1");
    }

    [TestMethod]
    public void ApplyRequests_TemporaryStateFilesLeftByAWriteCutShort_DeletesTheOldOnesAndKeepsTheRecentOnes()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        Directory.CreateDirectory(Path.GetDirectoryName(user.StateFile)!);
        var old = $"{user.StateFile}.{Guid.NewGuid():N}.tmp";
        var recent = $"{user.StateFile}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(old, "{ \"plugins\": [");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-1));
        File.WriteAllText(recent, "{ \"plugins\": [");

        Applied(shipped, user);

        File.Exists(old).Should().BeFalse();
        File.Exists(recent).Should().BeTrue();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ApplyRequests_StateFileThatCannotBeReplaced_DoesNotThrowAndLeavesNoTemporaryFile()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"));
        user.WriteStateFile("""{ "plugins": [], "appliedRequests": [] }""");
        File.SetAttributes(user.StateFile, FileAttributes.ReadOnly);
        var store = Store(shipped, user);

        var apply = store.ApplyRequests;

        apply.Should().NotThrow();
        Directory.EnumerateFiles(user.Path, "*.tmp").Should().BeEmpty();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ApplyRequests_UninstallAPluginThatHoldsAJunction_RemovesThePluginAndLeavesTheTargetIntact()
    {
        using var shipped = new PluginFolder();
        using var outside = new PluginFolder();
        outside.AddManifest("travel-photos", "{}");
        File.WriteAllText(Path.Combine(outside.Path, "travel-photos", "beach-day-2.jpg"), "fictional photo");
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        CreateJunction(Path.Combine(user.Installed.Path, VIEW_ID, "assets"), Path.Combine(outside.Path, "travel-photos"));
        user.WriteRequests(UserPluginFolder.Request("r1", "uninstall", VIEW_ID, "installed"));

        var store = Applied(shipped, user);

        store.Find(_installedView).Should().BeNull();
        FoldersOf(user.Installed.Path).Should().BeEmpty();
        Directory.EnumerateFiles(Path.Combine(outside.Path, "travel-photos")).Select(file => Path.GetFileName(file))
            .Should().BeEquivalentTo("plugin.json", "beach-day-2.jpg");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ApplyRequests_UninstallAPluginWhoseFolderIsAJunction_RemovesTheLinkAndLeavesTheTargetIntact()
    {
        using var shipped = new PluginFolder();
        using var outside = new PluginFolder();
        outside.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        Directory.CreateDirectory(user.Installed.Path);
        CreateJunction(Path.Combine(user.Installed.Path, VIEW_ID), Path.Combine(outside.Path, VIEW_ID));
        user.WriteRequests(UserPluginFolder.Request("r1", "uninstall", VIEW_ID, "installed"));

        var store = Applied(shipped, user);

        store.Find(_installedView).Should().BeNull();
        FoldersOf(user.Installed.Path).Should().BeEmpty();
        File.Exists(Path.Combine(outside.Path, VIEW_ID, "plugin.json")).Should().BeTrue();
    }

    [TestMethod]
    public void ApplyRequests_UninstallAPluginThatHoldsReadOnlyFiles_RemovesItAndLeavesNothingBehind()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        var ui = Directory.CreateDirectory(Path.Combine(user.Installed.Path, VIEW_ID, "ui")).FullName;
        File.WriteAllText(Path.Combine(ui, "index.js"), "export default {};");
        File.SetAttributes(Path.Combine(ui, "index.js"), FileAttributes.ReadOnly);
        File.SetAttributes(Path.Combine(user.Installed.Path, VIEW_ID, "plugin.json"), FileAttributes.ReadOnly);
        user.WriteRequests(UserPluginFolder.Request("r1", "uninstall", VIEW_ID, "installed"));

        var store = Applied(shipped, user);

        store.Find(_installedView).Should().BeNull();
        FoldersOf(user.Installed.Path).Should().BeEmpty();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ApplyRequests_SeveralLeftoversOneOfThemLocked_DeletesTheOthers()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(".removing-fixture-alpha-0123456789abcdef", ViewManifest("fixture-alpha"));
        user.Installed.AddManifest(".removing-fixture-bravo-0123456789abcdef", ViewManifest("fixture-bravo"));
        user.Installed.AddManifest(".removing-fixture-charlie-0123456789abcdef", ViewManifest("fixture-charlie"));
        using var locked = File.Open(
            Path.Combine(user.Installed.Path, ".removing-fixture-alpha-0123456789abcdef", "plugin.json"),
            FileMode.Open,
            FileAccess.Read,
            FileShare.None);

        Applied(shipped, user);

        FoldersOf(user.Installed.Path).Should().Equal(".removing-fixture-alpha-0123456789abcdef");
    }

    [TestMethod]
    public void Find_PluginTheUserNeverChanged_ReturnsNull()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();

        var store = Applied(shipped, user);

        store.Find(_shippedView).Should().BeNull();
    }

    [TestMethod]
    public void ReadPendingRequests_NothingWrittenSinceStart_ReturnsNone()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"));
        var store = Applied(shipped, user);

        var pending = store.ReadPendingRequests();

        pending.Should().BeEmpty();
    }

    [TestMethod]
    public void ReadPendingRequests_RequestsWrittenSinceStart_ReturnsOnlyThoseInTheirOrder()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.Installed.AddManifest("fixture-other", ViewManifest("fixture-other"));
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"));
        var store = Applied(shipped, user);
        user.WriteRequests(
            UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"),
            UserPluginFolder.Request("r2", "uninstall", "fixture-other", "installed"),
            UserPluginFolder.Request("r3", "enable", VIEW_ID, "shipped"));

        var pending = store.ReadPendingRequests();

        pending.Should().Equal(
            new PluginRequest("r2", PluginRequestAction.Uninstall, new PluginKey(PluginOrigin.Installed, "fixture-other")),
            new PluginRequest("r3", PluginRequestAction.Enable, _shippedView));
        store.DisabledPlugins.Should().BeEquivalentTo([_shippedView]);
        Directory.Exists(Path.Combine(user.Installed.Path, "fixture-other")).Should().BeTrue();
    }

    [TestMethod]
    public void ReadPendingRequests_UnreadableRequestsFile_ReturnsNone()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        var store = Applied(shipped, user);
        user.WriteRequestsFile("this is not json");

        var pending = store.ReadPendingRequests();

        pending.Should().BeEmpty();
    }

    [TestMethod]
    public void ReadPendingRequests_FileReadableAgainAfterAnUnreadableStart_LeavesOutTheAppliedRequests()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"));
        Applied(shipped, user);
        user.WriteRequestsFile("this is not json");
        var store = Applied(shipped, user);
        user.WriteRequests(
            UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"),
            UserPluginFolder.Request("r2", "enable", VIEW_ID, "shipped"));

        var pending = store.ReadPendingRequests();

        pending.Should().Equal(new PluginRequest("r2", PluginRequestAction.Enable, _shippedView));
    }

    [TestMethod]
    public void ReadPendingRequests_FileOpenForWritingByTheDesktopApp_StillReadsIt()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        var store = Applied(shipped, user);
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"));
        using var writer = new FileStream(user.RequestsFile, FileMode.Open, FileAccess.Write, FileShare.Read | FileShare.Write);

        var pending = store.ReadPendingRequests();

        pending.Should().Equal(new PluginRequest("r1", PluginRequestAction.Disable, _shippedView));
    }

    [TestMethod]
    public void ReadPendingRequests_FileReplacedByTheDesktopAppAfterARead_ReadsTheNewFile()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        var store = Applied(shipped, user);
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"));
        _ = store.ReadPendingRequests();
        var replacement = Path.Combine(user.Path, "requests.json.new");
        File.WriteAllText(replacement, $$"""{ "requests": [{{UserPluginFolder.Request("r2", "enable", VIEW_ID, "shipped")}}] }""");

        var replace = () => File.Move(replacement, user.RequestsFile, overwrite: true);

        replace.Should().NotThrow();
        store.ReadPendingRequests().Should().Equal(new PluginRequest("r2", PluginRequestAction.Enable, _shippedView));
    }

    [TestMethod]
    public void ReadPendingRequestByPlugin_SeveralRequestsForAPlugin_KeepsTheLastOne()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        var store = Applied(shipped, user);
        user.WriteRequests(
            UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"),
            UserPluginFolder.Request("r2", "uninstall", VIEW_ID, "shipped"),
            UserPluginFolder.Request("r3", "enable", VIEW_ID, "shipped"));

        var pending = store.ReadPendingRequestByPlugin();

        pending.Should().ContainSingle().Which.Value.Should().Be(new PluginRequest("r3", PluginRequestAction.Enable, _shippedView));
    }

    [TestMethod]
    public void ReadPendingRequestByPlugin_SameIdInBothOrigins_KeepsOneRequestPerOrigin()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        var store = Applied(shipped, user);
        user.WriteRequests(
            UserPluginFolder.Request("r1", "disable", VIEW_ID, "shipped"),
            UserPluginFolder.Request("r2", "uninstall", VIEW_ID, "installed"));

        var pending = store.ReadPendingRequestByPlugin();

        pending.Should().BeEquivalentTo(new Dictionary<PluginKey, PluginRequest>
        {
            [_shippedView] = new("r1", PluginRequestAction.Disable, _shippedView),
            [_installedView] = new("r2", PluginRequestAction.Uninstall, _installedView),
        });
    }

    #endregion Public Methods
}
