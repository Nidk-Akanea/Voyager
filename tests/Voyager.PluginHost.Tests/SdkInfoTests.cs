using System.Reflection;

using AwesomeAssertions;

using Voyager.Sdk;

namespace Voyager.PluginHost.Tests;

[TestClass]
public sealed class SdkInfoTests
{
    // A plugin is compiled against the identity of the SDK assembly and the host binds it to its own copy: when the
    // identity followed the version of the build, a plugin built at 0.3.0 against an SDK that is 0.1.0 would not load.
    [TestMethod]
    public void AssemblyVersion_Sdk_IsFixed()
    {
        typeof(SdkInfo).Assembly.GetName().Version.Should().Be(new Version(1, 0, 0, 0));
    }

    [TestMethod]
    public void Version_Sdk_IsTheFileVersionComputedForTheSdk()
    {
        // What the host compares with the "sdk" field of the manifests ("0.1"): the version computed for the SDK.
        var fileVersion = typeof(SdkInfo).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version;

        SdkInfo.Version.Should().Be(Version.Parse(fileVersion!));
    }
}
