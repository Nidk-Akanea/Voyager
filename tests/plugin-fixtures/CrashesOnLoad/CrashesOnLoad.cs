using System.Diagnostics;

using Voyager.Sdk;

namespace Voyager.PluginFixtures.CrashesOnLoad;

// Ends the whole process as it loads, as a plugin that crashes the service would: no exception for the host to catch,
// and no error report dialog on Windows. Only ever loaded in a process of its own (tests/Voyager.LocalService.Tests).
public sealed class CrashesOnLoadPlugin : IPlugin
{
    public void Configure(IPluginBuilder builder) => Process.GetCurrentProcess().Kill();
}
