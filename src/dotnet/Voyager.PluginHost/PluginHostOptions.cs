namespace Voyager.PluginHost;

/// <summary>
/// The limits the host puts on what a plugin does while it answers a call (spec of the plugin proof).
/// </summary>
/// <param name="FactTimeout">How long a source has to provide its facts before the host stops waiting.</param>
/// <param name="MaximumFactsPerSource">How many facts of a source are kept; the others are left out.</param>
public sealed record PluginHostOptions(TimeSpan FactTimeout, int MaximumFactsPerSource)
{
    /// <summary>The limits of the application: 5 seconds and 50 facts.</summary>
    public static PluginHostOptions Default { get; } = new(TimeSpan.FromSeconds(5), 50);
}
