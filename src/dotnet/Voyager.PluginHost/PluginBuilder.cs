using Microsoft.Extensions.DependencyInjection;

using Voyager.Sdk;
using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginHost;

/// <summary>
/// What <see cref="IPlugin.Configure"/> receives: it only collects what the plugin registers. The host
/// checks it against the manifest and puts it in the registry afterwards, so that a plugin that fails
/// half way leaves nothing behind.
/// </summary>
internal sealed class PluginBuilder(string pluginId, IServiceCollection services) : IPluginBuilder
{
    #region Fields

    private readonly List<ExtensionRegistration> _registrations = [];

    #endregion Fields

    #region Properties

    /// <inheritdoc />
    public string PluginId { get; } = pluginId;

    /// <inheritdoc />
    public IServiceCollection Services { get; } = services;

    /// <summary>What the plugin registered, in order.</summary>
    public IReadOnlyList<ExtensionRegistration> Registrations => _registrations;

    #endregion Properties

    #region Public Methods

    /// <inheritdoc />
    public IPluginBuilder AddDiagnosticFactSource<TSource>()
        where TSource : class, IDiagnosticFactSource
    {
        // Registering the same source twice would list its facts twice.
        if (_registrations.Any(registration => registration.ImplementationType == typeof(TSource)))
        {
            return this;
        }

        Services.AddSingleton<TSource>();
        _registrations.Add(new ExtensionRegistration(ExtensionPoints.DIAGNOSTIC_FACT_SOURCE, typeof(TSource)));
        return this;
    }

    #endregion Public Methods
}
