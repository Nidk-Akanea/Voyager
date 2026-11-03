using System.Text.Json.Nodes;

using AwesomeAssertions;

namespace Voyager.PluginHost.Tests;

/// <summary>
/// The file of requests the desktop app writes: tests/fixtures/plugin-requests.json is written exactly so by the tests of
/// src/ts/desktop/src/main/plugins/requests.test.ts, and read here as the service reads it.
/// </summary>
[TestClass]
public sealed class PluginStoreFilesTests
{
    [TestMethod]
    public void ReadRequests_FileTheDesktopAppWrites_ReadsEveryRequest()
    {
        var path = Path.Combine(PluginFolder.RepositoryRoot(), "tests", "fixtures", "plugin-requests.json");

        var requests = PluginStoreFiles.ReadRequests(path, out var ignored, out var recovery);

        ignored.Should().Be(0);
        recovery.Should().Be("4f5a6b7c-8d9e-4f0a-1b2c-3d4e5f6a7b8c");
        requests.Should().Equal(
            new PluginRequest(
                "0b9f8c1e-1d2a-4c3b-9e8f-7a6b5c4d3e2f",
                PluginRequestAction.Install,
                new PluginKey(PluginOrigin.Installed, "fixture-tool"),
                new PluginPackageReference(
                    "0123456789abcdef0123456789abcdef.voyagerplugin",
                    new string('a', 64),
                    "fixture-tool-1.2.0.voyagerplugin")),
            new PluginRequest(
                "1c2d3e4f-5a6b-4c7d-8e9f-0a1b2c3d4e5f",
                PluginRequestAction.Disable,
                new PluginKey(PluginOrigin.Shipped, "diagnostics-clock")),
            new PluginRequest(
                "2d3e4f5a-6b7c-4d8e-9f0a-1b2c3d4e5f6a",
                PluginRequestAction.Enable,
                new PluginKey(PluginOrigin.Shipped, "diagnostics-runtime")),
            new PluginRequest(
                "3e4f5a6b-7c8d-4e9f-0a1b-2c3d4e5f6a7b",
                PluginRequestAction.Uninstall,
                new PluginKey(PluginOrigin.Installed, "fixture-old")));
    }

    [TestMethod]
    public void ReadRequests_RequestToStartWithoutTheInstalledPlugins_ReadsItsIdentifierApartFromTheRequests()
    {
        using var user = new UserPluginFolder();
        user.WriteRequestsFile($$"""{ "requests": [{{UserPluginFolder.Request("r1", "enable", "fixture-tool", "installed")}}], "recovery": "rec-0b9f8c1e" }""");

        var requests = PluginStoreFiles.ReadRequests(user.RequestsFile, out var ignored, out var recovery);

        recovery.Should().Be("rec-0b9f8c1e");
        ignored.Should().Be(0);
        requests.Should().Equal(new PluginRequest("r1", PluginRequestAction.Enable, new PluginKey(PluginOrigin.Installed, "fixture-tool")));
    }

    [TestMethod]
    public void ReadRequests_NoRequestToStartWithoutTheInstalledPlugins_HasNone()
    {
        using var user = new UserPluginFolder();
        user.WriteRequests(UserPluginFolder.Request("r1", "enable", "fixture-tool", "installed"));

        PluginStoreFiles.ReadRequests(user.RequestsFile, out var ignored, out var recovery);

        recovery.Should().BeNull();
        ignored.Should().Be(0);
    }

    [TestMethod]
    [DataRow("\"rec 1/..\"", DisplayName = "forbidden characters")]
    [DataRow("\"\"", DisplayName = "empty")]
    [DataRow("\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"", DisplayName = "longer than 64 characters")]
    [DataRow("3", DisplayName = "a number")]
    [DataRow("true", DisplayName = "a boolean")]
    [DataRow("{ \"id\": \"rec-1\" }", DisplayName = "an object")]
    public void ReadRequests_InvalidRequestToStartWithoutTheInstalledPlugins_IsIgnoredAndCounted(string recoveryValue)
    {
        using var user = new UserPluginFolder();
        user.WriteRequestsFile($$"""{ "requests": [{{UserPluginFolder.Request("r1", "enable", "fixture-tool", "installed")}}], "recovery": {{recoveryValue}} }""");

        var requests = PluginStoreFiles.ReadRequests(user.RequestsFile, out var ignored, out var recovery);

        recovery.Should().BeNull();
        ignored.Should().Be(1);
        requests.Should().ContainSingle("an invalid recovery leaves out only itself");
    }

    [TestMethod]
    public void WriteState_SuspendedPluginAndPluginDisabledForRecovery_AreReadBackAsWritten()
    {
        using var user = new UserPluginFolder();
        PluginRecord[] records =
        [
            new(new PluginKey(PluginOrigin.Installed, "fixture-tool"), Enabled: true, InstalledAt: null, PackageName: null, PackageSha256: null, LastError: null, Suspended: true),
            new(new PluginKey(PluginOrigin.Installed, "fixture-map"), Enabled: false, InstalledAt: null, PackageName: null, PackageSha256: null, LastError: null, DisabledForRecovery: true),
            new(new PluginKey(PluginOrigin.Shipped, "fixture-view"), Enabled: false, InstalledAt: null, PackageName: null, PackageSha256: null, LastError: null),
        ];

        PluginStoreFiles.WriteState(user.StateFile, records, ["rec-1"]);

        var (read, applied) = PluginStoreFiles.ReadState(user.StateFile);
        read.Should().Equal(records);
        applied.Should().Equal("rec-1");
    }

    [TestMethod]
    public void WriteState_SuspendedPluginAndPluginDisabledForRecovery_WritesTheirMarksInCamelCase()
    {
        using var user = new UserPluginFolder();
        PluginRecord[] records =
        [
            new(new PluginKey(PluginOrigin.Installed, "fixture-tool"), Enabled: true, InstalledAt: null, PackageName: null, PackageSha256: null, LastError: null, Suspended: true),
            new(new PluginKey(PluginOrigin.Installed, "fixture-map"), Enabled: false, InstalledAt: null, PackageName: null, PackageSha256: null, LastError: null, DisabledForRecovery: true),
        ];

        PluginStoreFiles.WriteState(user.StateFile, records, []);

        var plugins = user.ReadState()["plugins"]!.AsArray();
        plugins[0]!["suspended"]!.GetValue<bool>().Should().BeTrue();
        plugins[1]!["disabledForRecovery"]!.GetValue<bool>().Should().BeTrue();
    }

    [TestMethod]
    public void WriteState_PluginNeitherSuspendedNorDisabledForRecovery_WritesNeitherMark()
    {
        using var user = new UserPluginFolder();
        PluginRecord[] records =
        [
            new(new PluginKey(PluginOrigin.Shipped, "fixture-view"), Enabled: false, InstalledAt: null, PackageName: null, PackageSha256: null, LastError: null),
        ];

        PluginStoreFiles.WriteState(user.StateFile, records, []);

        var plugin = user.ReadState()["plugins"]!.AsArray().Single()!.AsObject();
        plugin.ContainsKey("suspended").Should().BeFalse();
        plugin.ContainsKey("disabledForRecovery").Should().BeFalse();
    }

    [TestMethod]
    public void ReadState_StateOfAnEarlierVersionWithoutTheMarks_ReadsNeitherSuspendedNorDisabledForRecovery()
    {
        using var user = new UserPluginFolder();
        user.WriteStateFile("""{ "plugins": [ { "id": "fixture-tool", "origin": "installed", "enabled": false } ], "appliedRequests": [] }""");

        var (records, _) = PluginStoreFiles.ReadState(user.StateFile);

        records.Should().ContainSingle().Which.Should().Match<PluginRecord>(record => !record.Suspended && !record.DisabledForRecovery);
    }

    [TestMethod]
    public void WriteActivity_PluginLoadingAndPluginsCalled_AreReadBackAsWritten()
    {
        using var user = new UserPluginFolder();
        var file = Path.Combine(user.Path, "activity.json");
        var loading = new PluginKey(PluginOrigin.Installed, "fixture-tool");
        PluginKey[] calls = [new(PluginOrigin.Shipped, "fixture-view"), new(PluginOrigin.Installed, "fixture-map")];

        PluginStoreFiles.WriteActivity(file, loading, calls);

        var read = PluginStoreFiles.ReadActivity(file);
        read.Loading.Should().Be(loading);
        read.Calls.Should().Equal(calls);
        JsonNode.Parse(File.ReadAllText(file))!["loading"]!["origin"]!.GetValue<string>().Should().Be("installed");
    }
}
