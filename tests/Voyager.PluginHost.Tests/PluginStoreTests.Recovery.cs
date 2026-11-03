using AwesomeAssertions;

namespace Voyager.PluginHost.Tests;

/// <summary>
/// After a crash (spec of the plugin management, "Quand Voyager plante à cause d'un plugin"): the suspect is suspended
/// until the user decides, and a start without the installed plugins disables them all for the user to enable again one
/// by one.
/// </summary>
public sealed partial class PluginStoreTests
{
    #region Fields

    private const string RECOVERY_ID = "rec-5e1f";
    private const string MAP_ID = "fixture-map";

    private static readonly PluginKey _installedMap = new(PluginOrigin.Installed, MAP_ID);

    #endregion Fields

    #region Private Methods

    private static PluginStore AppliedWithSuspect(PluginFolder shipped, UserPluginFolder user, PluginKey suspect)
    {
        var store = Store(shipped, user);
        store.ApplyRequests(suspect);
        return store;
    }

    // requests.json with the request to start without the installed plugins, and these requests.
    private static void WriteRecovery(UserPluginFolder user, params string[] requests) =>
        user.WriteRequestsFile($$"""{ "requests": [{{string.Join(", ", requests)}}], "recovery": "{{RECOVERY_ID}}" }""");

    #endregion Private Methods

    #region Public Methods

    [TestMethod]
    [DataRow(PluginOrigin.Shipped)]
    [DataRow(PluginOrigin.Installed)]
    public void ApplyRequests_SuspectThatIsEnabled_IsSuspended(PluginOrigin origin)
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        (origin == PluginOrigin.Shipped ? shipped : user.Installed).AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        var suspect = new PluginKey(origin, VIEW_ID);

        var store = AppliedWithSuspect(shipped, user, suspect);

        store.SuspendedPlugins.Should().BeEquivalentTo([suspect]);
        store.DisabledPlugins.Should().BeEmpty("a suspended plugin is not disabled: the user has not decided yet");
        store.Find(suspect)!.Suspended.Should().BeTrue();
    }

    [TestMethod]
    public void ApplyRequests_SuspectThatIsEnabled_IsWrittenSuspendedInTheState()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));

        AppliedWithSuspect(shipped, user, _installedView);

        var plugin = user.ReadState()["plugins"]!.AsArray().Should().ContainSingle().Subject!.AsObject();
        plugin["id"]!.GetValue<string>().Should().Be(VIEW_ID);
        plugin["origin"]!.GetValue<string>().Should().Be("installed");
        plugin["enabled"]!.GetValue<bool>().Should().BeTrue();
        plugin["suspended"]!.GetValue<bool>().Should().BeTrue();
    }

    [TestMethod]
    public void ApplyRequests_StartWithoutInstalledPlugins_KeepsASuspendedPluginSuspended()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.Installed.AddManifest(MAP_ID, ViewManifest(MAP_ID));
        AppliedWithSuspect(shipped, user, _installedView);
        WriteRecovery(user);

        var store = Applied(shipped, user);

        // Still suspended, so that enabling it again recalls the suspicion; the other one is disabled for recovery.
        store.SuspendedPlugins.Should().BeEquivalentTo([_installedView]);
        store.Find(_installedView)!.DisabledForRecovery.Should().BeFalse();
        store.Find(_installedMap)!.DisabledForRecovery.Should().BeTrue();
    }

    [TestMethod]
    public void ApplyRequests_NextStartsWithoutDecision_KeepTheSuspension()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        AppliedWithSuspect(shipped, user, _installedView);
        Applied(shipped, user);

        var store = Applied(shipped, user);

        store.SuspendedPlugins.Should().BeEquivalentTo([_installedView]);
    }

    [TestMethod]
    public void ApplyRequests_SuspectNoLongerThere_SuspendsNothingAndRemembersNothing()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();

        var store = AppliedWithSuspect(shipped, user, _installedView);

        store.SuspendedPlugins.Should().BeEmpty();
        store.Records.Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyRequests_SuspectOfTheOtherOrigin_DoesNotSuspendThisOne()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));

        var store = AppliedWithSuspect(shipped, user, _shippedView);

        store.SuspendedPlugins.Should().BeEmpty();
    }

    [TestMethod]
    public void ApplyRequests_SuspectTheUserDisabled_StaysDisabledWithoutSuspension()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "installed"));
        Applied(shipped, user);

        var store = AppliedWithSuspect(shipped, user, _installedView);

        store.SuspendedPlugins.Should().BeEmpty();
        store.DisabledPlugins.Should().BeEquivalentTo([_installedView]);
        store.Find(_installedView)!.Suspended.Should().BeFalse();
    }

    [TestMethod]
    public void ApplyRequests_EnableASuspendedPlugin_LiftsTheSuspension()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        AppliedWithSuspect(shipped, user, _installedView);
        user.WriteRequests(UserPluginFolder.Request("r1", "enable", VIEW_ID, "installed"));

        var store = Applied(shipped, user);

        store.SuspendedPlugins.Should().BeEmpty();
        store.DisabledPlugins.Should().BeEmpty();
        store.Find(_installedView)!.Should().Match<PluginRecord>(record => record.Enabled && !record.Suspended);
    }

    [TestMethod]
    public void ApplyRequests_DisableASuspendedPlugin_DisablesItForGood()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        AppliedWithSuspect(shipped, user, _installedView);
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "installed"));

        var store = Applied(shipped, user);

        store.SuspendedPlugins.Should().BeEmpty();
        store.DisabledPlugins.Should().BeEquivalentTo([_installedView]);
        store.Find(_installedView)!.Should().Match<PluginRecord>(record => !record.Enabled && !record.Suspended);
    }

    [TestMethod]
    public void ApplyRequests_ReplaceASuspendedPlugin_LiftsTheSuspensionAndKeepsItEnabled()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, TestPackage.Manifest(VIEW_ID, "1.0.0"));
        AppliedWithSuspect(shipped, user, _installedView);
        StageAndRequest(user, TestPackage.View(VIEW_ID, "1.0.1"));

        var store = Applied(shipped, user);

        InstalledManifest(user).Should().Contain("\"1.0.1\"");
        store.SuspendedPlugins.Should().BeEmpty();
        store.DisabledPlugins.Should().BeEmpty();
        store.Find(_installedView)!.Should().Match<PluginRecord>(record => record.Enabled && !record.Suspended);
    }

    [TestMethod]
    public void ApplyRequests_StartWithoutTheInstalledPlugins_DisablesEachInstalledPluginForRecovery()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest("fixture-clock", ViewManifest("fixture-clock"));
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.Installed.AddManifest(MAP_ID, ViewManifest(MAP_ID));
        WriteRecovery(user);

        var store = Applied(shipped, user);

        store.DisabledPlugins.Should().BeEquivalentTo([_installedView, _installedMap]);
        store.Records.Should().AllSatisfy(record => record.DisabledForRecovery.Should().BeTrue());
    }

    [TestMethod]
    public void ApplyRequests_StartWithoutTheInstalledPlugins_LeavesTheShippedOnesEnabled()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(MAP_ID, ViewManifest(MAP_ID));
        WriteRecovery(user);

        var store = Applied(shipped, user);

        store.DisabledPlugins.Should().NotContain(_shippedView);
        store.Find(_shippedView).Should().BeNull();
    }

    [TestMethod]
    public void ApplyRequests_StartWithoutTheInstalledPlugins_LeavesAPluginTheUserDisabledAsTheUserDisabledIt()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.WriteRequests(UserPluginFolder.Request("r1", "disable", VIEW_ID, "installed"));
        Applied(shipped, user);
        WriteRecovery(user);

        var store = Applied(shipped, user);

        store.Find(_installedView)!.Should().Match<PluginRecord>(record => !record.Enabled && !record.DisabledForRecovery);
    }

    [TestMethod]
    public void ApplyRequests_StartWithoutTheInstalledPlugins_IsWrittenInTheState()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        WriteRecovery(user);

        Applied(shipped, user);

        var state = user.ReadState();
        var plugin = state["plugins"]!.AsArray().Should().ContainSingle().Subject!.AsObject();
        plugin["enabled"]!.GetValue<bool>().Should().BeFalse();
        plugin["disabledForRecovery"]!.GetValue<bool>().Should().BeTrue();
        state["appliedRequests"]!.AsArray().Select(id => id!.GetValue<string>()).Should().Equal(RECOVERY_ID);
    }

    [TestMethod]
    public void ApplyRequests_StartWithoutTheInstalledPluginsStillInTheFile_IsNotAppliedAgain()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.Installed.AddManifest(MAP_ID, ViewManifest(MAP_ID));
        WriteRecovery(user);
        Applied(shipped, user);
        WriteRecovery(user, UserPluginFolder.Request("r1", "enable", VIEW_ID, "installed"));
        Applied(shipped, user);

        var store = Applied(shipped, user);

        store.DisabledPlugins.Should().BeEquivalentTo([_installedMap], "the plugin enabled again one by one stays enabled");
    }

    [TestMethod]
    public void ApplyRequests_EnableWrittenAfterTheStartWithoutTheInstalledPlugins_EnablesThatPluginAgain()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.Installed.AddManifest(MAP_ID, ViewManifest(MAP_ID));
        WriteRecovery(user);
        Applied(shipped, user);
        WriteRecovery(user, UserPluginFolder.Request("r1", "enable", VIEW_ID, "installed"));

        var store = Applied(shipped, user);

        store.Find(_installedView)!.Should().Match<PluginRecord>(record => record.Enabled && !record.DisabledForRecovery);
        store.Find(_installedMap)!.Should().Match<PluginRecord>(record => !record.Enabled && record.DisabledForRecovery);
    }

    [TestMethod]
    public void ApplyRequests_EnableInTheSameFileAsTheStartWithoutTheInstalledPlugins_IsAppliedAfterIt()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.Installed.AddManifest(MAP_ID, ViewManifest(MAP_ID));
        WriteRecovery(user, UserPluginFolder.Request("r1", "enable", VIEW_ID, "installed"));

        var store = Applied(shipped, user);

        store.DisabledPlugins.Should().BeEquivalentTo([_installedMap]);
        store.Find(_installedView)!.DisabledForRecovery.Should().BeFalse();
    }

    [TestMethod]
    public void ApplyRequests_DisableAPluginDisabledForRecovery_KeepsItDisabledByTheUser()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        WriteRecovery(user);
        Applied(shipped, user);
        WriteRecovery(user, UserPluginFolder.Request("r1", "disable", VIEW_ID, "installed"));

        var store = Applied(shipped, user);

        store.Find(_installedView)!.Should().Match<PluginRecord>(record => !record.Enabled && !record.DisabledForRecovery);
    }

    [TestMethod]
    public void ApplyRequests_StartWithoutTheInstalledPluginsWhenNoneIsInstalled_DisablesNothing()
    {
        using var shipped = new PluginFolder();
        shipped.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        using var user = new UserPluginFolder();
        WriteRecovery(user);

        var store = Applied(shipped, user);

        store.DisabledPlugins.Should().BeEmpty();
        user.ReadState()["appliedRequests"]!.AsArray().Select(id => id!.GetValue<string>()).Should().Equal(RECOVERY_ID);
    }

    [TestMethod]
    public void ApplyRequests_StartWithoutTheInstalledPlugins_SkipsLeftoversAndFoldersThatAreNoIdentifier()
    {
        using var shipped = new PluginFolder();
        using var user = new UserPluginFolder();
        user.Installed.AddManifest(VIEW_ID, ViewManifest(VIEW_ID));
        user.Installed.AddManifest("Not Valid", ViewManifest(MAP_ID));
        WriteRecovery(user);

        var store = Applied(shipped, user);

        store.Records.Select(record => record.Plugin).Should().Equal(_installedView);
    }

    #endregion Public Methods
}
