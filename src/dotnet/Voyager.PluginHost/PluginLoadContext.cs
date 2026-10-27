using System.Reflection;
using System.Runtime.Loader;

namespace Voyager.PluginHost;

/// <summary>
/// The assembly load context of one plugin. The assemblies shared with the host are never loaded a
/// second time: a type that existed twice would have two identities, and passing it from the plugin to
/// the host would fail (ADR-0015, decision 2). Everything else the plugin ships is its own.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    #region Fields

    // The Voyager.Sdk contract and the two packages it exposes to a plugin. Keep in step with the
    // allowed references checked by PluginAssemblyTests.
    private static readonly HashSet<string> _sharedAssemblies = new(StringComparer.Ordinal)
    {
        "Voyager.Sdk",
        "Voyager.Core",
        "NodaTime",
        "Microsoft.Extensions.DependencyInjection.Abstractions",
        "Microsoft.Extensions.Logging.Abstractions",
    };

    // The platform and the shared framework belong to the host, whatever a plugin folder holds.
    private static readonly string[] _platformPrefixes = ["System.", "Microsoft.", "netstandard", "mscorlib", "WindowsBase"];

    private readonly string _folder;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Creates the context of one plugin. It is not collectible: the host never unloads a plugin while it runs.
    /// </summary>
    /// <param name="pluginId">The identifier of the plugin, to tell its context apart in diagnostics.</param>
    /// <param name="folder">The folder of the plugin, where its assemblies are.</param>
    public PluginLoadContext(string pluginId, string folder)
        : base($"plugin:{pluginId}", isCollectible: false)
    {
        _folder = folder;
    }

    #endregion Constructors

    #region Public Methods

    /// <summary>Whether the host and every plugin share this assembly instead of each loading its own.</summary>
    /// <param name="assemblyName">The simple name of the assembly.</param>
    public static bool IsSharedWithHost(string assemblyName) => _sharedAssemblies.Contains(assemblyName);

    #endregion Public Methods

    #region Protected Methods

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        if (name is null
            || IsSharedWithHost(name)
            || _platformPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
        {
            // Null hands the request to the default context, which holds the host's own copy.
            return null;
        }

        var path = Path.Combine(_folder, name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }

    #endregion Protected Methods
}
