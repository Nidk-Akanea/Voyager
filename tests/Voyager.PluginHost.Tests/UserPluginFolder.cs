namespace Voyager.PluginHost.Tests;

/// <summary>
/// A temporary folder of the user's plugins: the installed plugins and the copies of packages to examine. Deleted
/// afterwards.
/// </summary>
internal sealed class UserPluginFolder : IDisposable
{
    #region Public Constructors

    /// <summary>A new temporary folder, with no installed plugin and no package.</summary>
    public UserPluginFolder() => Installed = new PluginFolder(System.IO.Path.Combine(Path, "installed"));

    #endregion Public Constructors

    #region Properties

    /// <summary>The folder of the user's plugins.</summary>
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"voyager-user-plugins-{Guid.NewGuid():N}");

    /// <summary>The installed plugins, one sub-folder each.</summary>
    public PluginFolder Installed { get; }

    /// <summary>The private copies of the packages to examine.</summary>
    public string StagingDirectory => System.IO.Path.Combine(Path, "staging");

    #endregion Properties

    #region Public Methods

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
