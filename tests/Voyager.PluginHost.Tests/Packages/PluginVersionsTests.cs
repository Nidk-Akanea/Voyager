using AwesomeAssertions;

using Voyager.PluginHost.Packages;

namespace Voyager.PluginHost.Tests.Packages;

[TestClass]
public sealed class PluginVersionsTests
{
    #region Public Methods

    [TestMethod]
    [DataRow("1.0.0", "1.0.1", DisplayName = "patch")]
    [DataRow("1.2.0", "1.10.0", DisplayName = "minor compared as numbers")]
    [DataRow("1.9.9", "2.0.0", DisplayName = "major before minor and patch")]
    [DataRow("1.0.0-alpha", "1.0.0", DisplayName = "pre-release before its release")]
    [DataRow("1.0.0-alpha.1", "1.0.0-alpha.2", DisplayName = "numeric labels")]
    [DataRow("1.0.0-alpha.2", "1.0.0-alpha.10", DisplayName = "numeric labels compared as numbers")]
    [DataRow("1.0.0-alpha.1", "1.0.0-alpha.beta", DisplayName = "numeric label before alphanumeric")]
    [DataRow("1.0.0-alpha", "1.0.0-alpha.1", DisplayName = "fewer labels first")]
    [DataRow("1.0.0-alpha", "1.0.0-beta", DisplayName = "alphanumeric labels in ASCII order")]
    [DataRow("1.0.0-Beta", "1.0.0-alpha", DisplayName = "upper case before lower case in ASCII")]
    [DataRow("1.0.0-rc.1", "1.0.1-alpha", DisplayName = "pre-release of a later patch")]
    public void Compare_OlderThenNewer_IsNegativeAndPositiveTheOtherWay(string older, string newer)
    {
        var forward = PluginVersions.Compare(older, newer);
        var backward = PluginVersions.Compare(newer, older);

        forward.Should().BeNegative();
        backward.Should().BePositive();
    }

    [TestMethod]
    [DataRow("1.0.0", "1.0.0", DisplayName = "same version")]
    [DataRow("1.0.0+build.1", "1.0.0+build.2", DisplayName = "build metadata ignored")]
    [DataRow("1.0.0-rc.1+sha.5114f85", "1.0.0-rc.1", DisplayName = "build metadata of a pre-release ignored")]
    public void Compare_SameVersion_IsZero(string left, string right)
    {
        var comparison = PluginVersions.Compare(left, right);

        comparison.Should().Be(0);
    }

    [TestMethod]
    public void Compare_PrecedenceExampleOfSemanticVersioning_SortsInItsOrder()
    {
        string[] expected = ["1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2", "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0"];

        var sorted = expected.Reverse().Order(Comparer<string>.Create(PluginVersions.Compare)).ToList();

        sorted.Should().Equal(expected);
    }

    [TestMethod]
    [DataRow("2147483648.0.0", "2147483647.0.0", DisplayName = "major beyond a 32-bit number")]
    [DataRow("1.0.0-alpha.10000000000", "1.0.0-alpha.9999999999", DisplayName = "numeric labels beyond a 32-bit number")]
    public void Compare_NumbersBeyond32Bits_AreStillComparedAsNumbers(string newer, string older)
    {
        var comparison = PluginVersions.Compare(newer, older);

        comparison.Should().BePositive();
    }

    #endregion Public Methods
}
