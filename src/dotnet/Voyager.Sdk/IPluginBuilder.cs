using Microsoft.Extensions.DependencyInjection;

using Voyager.Sdk.Diagnostics;

namespace Voyager.Sdk;

/// <summary>
/// What a plugin sees of the host while it is configured. A plugin registers; it never replaces: the
/// services of a plugin live in a container of their own, apart from the host and the other plugins.
/// </summary>
public interface IPluginBuilder
{
    /// <summary>The identifier of the plugin being configured, as written in its manifest.</summary>
    string PluginId { get; }

    /// <summary>
    /// The services of this plugin. They are private to the plugin; <c>ILogger&lt;T&gt;</c> is already
    /// available to them. The container is validated when it is built: a missing dependency stops this
    /// plugin from loading, and only this one.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Provides a source of diagnostic facts (<see cref="ExtensionPoints.DIAGNOSTIC_FACT_SOURCE"/>). The
    /// plugin must list this extension point in the <c>provides</c> section of its manifest. The source is
    /// registered as a singleton and created when the plugin loads, so a constructor that throws sets the
    /// plugin aside. Registering the same source type twice has no further effect.
    /// </summary>
    /// <typeparam name="TSource">The source; it is created by the container of the plugin.</typeparam>
    /// <returns>This builder, to chain registrations.</returns>
    IPluginBuilder AddDiagnosticFactSource<TSource>()
        where TSource : class, IDiagnosticFactSource;
}
