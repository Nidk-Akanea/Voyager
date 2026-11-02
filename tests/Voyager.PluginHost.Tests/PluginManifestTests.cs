using AwesomeAssertions;

namespace Voyager.PluginHost.Tests;

[TestClass]
public sealed class PluginManifestTests
{
    #region Private Methods

    private static PluginManifest Manifest(params ContractReference[] provides) =>
        new("fixture-good", "1.0.0", "Voyager.PluginFixtures.Good.dll", UiModule: null, provides, [new ContractReference("plugin", 1)]);

    #endregion Private Methods

    #region Public Methods

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void ProvidesContract_ContractInAnyVersion_IsTrue(int version)
    {
        var manifest = Manifest(new ContractReference("diagnostic-fact-source", version));

        var provides = manifest.ProvidesContract("diagnostic-fact-source");

        provides.Should().BeTrue();
    }

    [TestMethod]
    public void ProvidesContract_ContractThatIsOnlyRequired_IsFalse()
    {
        var manifest = Manifest(new ContractReference("diagnostic-fact-source", 1));

        var provides = manifest.ProvidesContract("plugin");

        provides.Should().BeFalse();
    }

    [TestMethod]
    public void ProvidesContract_IdentifierThatDiffersOnlyByCase_IsFalse()
    {
        var manifest = Manifest(new ContractReference("diagnostic-fact-source", 1));

        var provides = manifest.ProvidesContract("Diagnostic-Fact-Source");

        provides.Should().BeFalse();
    }

    #endregion Public Methods
}
