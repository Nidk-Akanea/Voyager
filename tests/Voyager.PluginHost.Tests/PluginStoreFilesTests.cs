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

        var requests = PluginStoreFiles.ReadRequests(path, out var ignored);

        ignored.Should().Be(0);
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
}
