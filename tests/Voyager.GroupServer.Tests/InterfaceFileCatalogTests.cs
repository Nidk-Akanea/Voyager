using System.Security.Cryptography;
using System.Text;

using AwesomeAssertions;

using Grpc.Core;

using Voyager.Contracts.PluginHost.V1;
using Voyager.PluginHost.Tests;

namespace Voyager.GroupServer.Tests;

/// <summary>
/// The files of the interfaces of the plugins, that the server publishes for the desktop app to download (ADR-0020,
/// decision 8; spec of the switch to client-server, lot 2c).
/// </summary>
[TestClass]
public sealed class InterfaceFileCatalogTests
{
    #region Fields

    private const string MODULE = "export function register() {}";
    private const string PLUGIN_ID = "fixture-good";
    private const string UI_PART = """, "ui": { "module": "index.js" }""";

    #endregion Fields

    #region Private Methods

    private static string Fingerprint(string content) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    // A plugin with an interface part: its module, a chunk it imports, a style sheet the app never serves, a hidden
    // script, and its French translations.
    private static PluginFolder PluginWithInterface(bool withModule = true)
    {
        var plugins = new PluginFolder();
        plugins.AddPlugin(PLUGIN_ID, "Good", PluginFolder.Manifest(PLUGIN_ID, "Voyager.PluginFixtures.Good.dll", uiPart: UI_PART));
        var folder = Path.Combine(plugins.Path, PLUGIN_ID);
        Directory.CreateDirectory(Path.Combine(folder, "ui", "chunks"));
        Directory.CreateDirectory(Path.Combine(folder, "i18n", "fr"));
        if (withModule)
        {
            File.WriteAllText(Path.Combine(folder, "ui", "index.js"), MODULE);
        }

        File.WriteAllText(Path.Combine(folder, "ui", "chunks", "panel.js"), "export const panel = 1;");
        File.WriteAllText(Path.Combine(folder, "ui", "style.css"), "body {}");
        File.WriteAllText(Path.Combine(folder, "ui", ".hidden.js"), "export {};");
        File.WriteAllText(Path.Combine(folder, "i18n", "fr", "messages.ftl"), "clock-title = Horloge");
        return plugins;
    }

    #endregion Private Methods

    #region Public Methods

    [TestMethod]
    public async Task ListPlugins_PluginWithInterface_ListsItsScriptsAndTranslationsWithTheirFingerprints()
    {
        using var plugins = PluginWithInterface();
        using var factory = new GroupServerFactory(shippedPluginsDirectory: plugins.Path);

        var response = await factory.CreatePluginHostClient().ListPluginsAsync(new ListPluginsRequest());

        var files = response.Plugins.Single(plugin => plugin.Id == PLUGIN_ID).InterfaceFiles;
        files.Select(file => file.Path).Should().Equal("i18n/fr/messages.ftl", "ui/chunks/panel.js", "ui/index.js");
        var module = files.Single(file => file.Path == "ui/index.js");
        module.Sha256.Should().Be(Fingerprint(MODULE));
        module.Size.Should().Be((ulong)MODULE.Length);
    }

    [TestMethod]
    public async Task ListPlugins_ModuleMissing_PublishesNoInterfaceFile()
    {
        using var plugins = PluginWithInterface(withModule: false);
        using var factory = new GroupServerFactory(shippedPluginsDirectory: plugins.Path);

        var response = await factory.CreatePluginHostClient().ListPluginsAsync(new ListPluginsRequest());

        response.Plugins.Single(plugin => plugin.Id == PLUGIN_ID).InterfaceFiles.Should().BeEmpty();
    }

    [TestMethod]
    public async Task ListPlugins_FileTooLarge_LeavesItOut()
    {
        using var plugins = PluginWithInterface();
        var large = Path.Combine(plugins.Path, PLUGIN_ID, "ui", "large.js");
        File.WriteAllBytes(large, new byte[InterfaceFileCatalog.MAXIMUM_FILE_BYTES + 1]);
        using var factory = new GroupServerFactory(shippedPluginsDirectory: plugins.Path);

        var response = await factory.CreatePluginHostClient().ListPluginsAsync(new ListPluginsRequest());

        response.Plugins.Single(plugin => plugin.Id == PLUGIN_ID).InterfaceFiles.Select(file => file.Path)
            .Should().Contain("ui/index.js").And.NotContain("ui/large.js");
    }

    [TestMethod]
    public async Task ListPlugins_PluginSetAside_PublishesNoInterfaceFile()
    {
        using var plugins = new PluginFolder();
        const string brokenId = "fixture-throws-on-configure";
        plugins.AddPlugin(brokenId, "ThrowsOnConfigure", PluginFolder.Manifest(brokenId, "Voyager.PluginFixtures.ThrowsOnConfigure.dll", uiPart: UI_PART));
        Directory.CreateDirectory(Path.Combine(plugins.Path, brokenId, "ui"));
        File.WriteAllText(Path.Combine(plugins.Path, brokenId, "ui", "index.js"), MODULE);
        using var factory = new GroupServerFactory(shippedPluginsDirectory: plugins.Path);

        var response = await factory.CreatePluginHostClient().ListPluginsAsync(new ListPluginsRequest());

        var broken = response.Plugins.Single(plugin => plugin.Id == brokenId);
        broken.Status.Should().Be(PluginStatus.Rejected);
        broken.InterfaceFiles.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetInterfaceFile_ListedFile_ReturnsItsBytes()
    {
        using var plugins = PluginWithInterface();
        using var factory = new GroupServerFactory(shippedPluginsDirectory: plugins.Path);

        var response = await factory.CreatePluginHostClient().GetInterfaceFileAsync(
            new GetInterfaceFileRequest { PluginId = PLUGIN_ID, Version = "1.0.0", Path = "ui/index.js" });

        response.Content.ToStringUtf8().Should().Be(MODULE);
    }

    [TestMethod]
    [DataRow(PLUGIN_ID, "2.0.0", "ui/index.js", DisplayName = "Another version")]
    [DataRow("nobody", "1.0.0", "ui/index.js", DisplayName = "Unknown plugin")]
    [DataRow(PLUGIN_ID, "1.0.0", "ui/style.css", DisplayName = "File the app never serves")]
    [DataRow(PLUGIN_ID, "1.0.0", "ui/.hidden.js", DisplayName = "Hidden file")]
    [DataRow(PLUGIN_ID, "1.0.0", "plugin.json", DisplayName = "Manifest")]
    [DataRow(PLUGIN_ID, "1.0.0", "ui/../plugin.json", DisplayName = "Out of the interface folder")]
    public async Task GetInterfaceFile_FileNotPublished_IsNotFound(string pluginId, string version, string path)
    {
        using var plugins = PluginWithInterface();
        using var factory = new GroupServerFactory(shippedPluginsDirectory: plugins.Path);

        var call = async () => await factory.CreatePluginHostClient().GetInterfaceFileAsync(
            new GetInterfaceFileRequest { PluginId = pluginId, Version = version, Path = path });

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.NotFound);
    }

    #endregion Public Methods
}
