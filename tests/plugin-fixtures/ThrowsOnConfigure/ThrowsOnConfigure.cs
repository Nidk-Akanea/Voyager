using Voyager.Sdk;

namespace Voyager.PluginFixtures.ThrowsOnConfigure;

public sealed class ThrowsOnConfigurePlugin : IPlugin
{
    public void Configure(IPluginBuilder builder) => throw new InvalidOperationException("The plugin cannot configure itself.");
}
