using AwesomeAssertions;

namespace Voyager.PluginHost.Tests;

[TestClass]
public sealed class SdkCompatibilityTests
{
    [TestMethod]
    // While the major version is 0, every minor version may break the contract: the minors must be equal.
    [DataRow("0.1", "0.1.5", true)]
    [DataRow("0.1", "0.2.0", false)]
    [DataRow("0.2", "0.1.9", false)]
    // From major 1, a newer minor accepts plugins written for an older one, never the opposite.
    [DataRow("1.2", "1.2.0", true)]
    [DataRow("1.2", "1.5.3", true)]
    [DataRow("1.5", "1.2.0", false)]
    // A different major version is never accepted.
    [DataRow("1.0", "2.0.0", false)]
    [DataRow("2.0", "1.9.0", false)]
    [DataRow("0.1", "1.1.0", false)]
    // A value that is not major.minor is refused.
    [DataRow("0", "0.1.0", false)]
    [DataRow("0.1.0", "0.1.0", false)]
    [DataRow("a.b", "0.1.0", false)]
    [DataRow("-1.0", "0.1.0", false)]
    [DataRow("", "0.1.0", false)]
    public void IsAccepted_RequestedAndRunningVersions_FollowsTheRule(string requested, string running, bool expected)
    {
        SdkCompatibility.IsAccepted(requested, Version.Parse(running)).Should().Be(expected);
    }
}
