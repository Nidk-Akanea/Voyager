using System.Text.Json.Nodes;

namespace Voyager.PluginHost.Tests;

/// <summary>
/// A temporary plugins folder of the user's data: the installed plugins, the requests of the desktop app and the
/// state of the service. Deleted afterwards.
/// </summary>
internal sealed class UserPluginFolder : IDisposable
{
    #region Public Constructors

    /// <summary>A new temporary folder, with no installed plugin, no request and no state.</summary>
    public UserPluginFolder() => Installed = new PluginFolder(System.IO.Path.Combine(Path, "installed"));

    #endregion Public Constructors

    #region Properties

    /// <summary>The plugins folder of the user's data.</summary>
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"voyager-user-plugins-{Guid.NewGuid():N}");

    /// <summary>The installed plugins, one sub-folder each.</summary>
    public PluginFolder Installed { get; }

    /// <summary>The file of requests written by the desktop app.</summary>
    public string RequestsFile => System.IO.Path.Combine(Path, "requests.json");

    /// <summary>The file of state written by the service.</summary>
    public string StateFile => System.IO.Path.Combine(Path, "state.json");

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// One entry of requests.json, as the desktop app writes it.
    /// </summary>
    /// <param name="id">The identifier of the request.</param>
    /// <param name="action">"enable", "disable", "uninstall" or "install".</param>
    /// <param name="pluginId">The identifier of the plugin.</param>
    /// <param name="origin">"shipped" or "installed".</param>
    public static string Request(string id, string action, string pluginId, string origin) =>
        $$"""{ "id": "{{id}}", "action": "{{action}}", "pluginId": "{{pluginId}}", "origin": "{{origin}}" }""";

    /// <summary>Writes requests.json with these entries, in this order, replacing what it held.</summary>
    /// <param name="requests">The entries, as written by <see cref="Request"/>.</param>
    public void WriteRequests(params string[] requests) =>
        WriteRequestsFile($$"""{ "requests": [{{string.Join(", ", requests)}}] }""");

    /// <summary>Writes requests.json as it is given, valid or not.</summary>
    public void WriteRequestsFile(string content)
    {
        Directory.CreateDirectory(Path);
        File.WriteAllText(RequestsFile, content);
    }

    /// <summary>Writes state.json as it is given, valid or not.</summary>
    public void WriteStateFile(string content)
    {
        Directory.CreateDirectory(Path);
        File.WriteAllText(StateFile, content);
    }

    /// <summary>Reads state.json as JSON.</summary>
    public JsonObject ReadState() => JsonNode.Parse(File.ReadAllText(StateFile))!.AsObject();

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            // Windows refuses to delete a read-only file, which some tests leave on purpose.
            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Path, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A plugin assembly stays locked by Windows while the test process lives (a load context is
            // not unloaded): the temporary folder is left to the operating system's cleanup.
        }
    }

    #endregion Public Methods
}
