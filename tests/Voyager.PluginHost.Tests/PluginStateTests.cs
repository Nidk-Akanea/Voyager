using AwesomeAssertions;

namespace Voyager.PluginHost.Tests;

[TestClass]
public sealed class PluginStateTests
{
    #region Public Methods

    [TestMethod]
    [DataRow(PluginOrigin.Shipped, PluginVerification.GuaranteedByApplication)]
    [DataRow(PluginOrigin.Installed, PluginVerification.Unverified)]
    public void Verification_Origin_IsVouchedForOnlyWhenShipped(PluginOrigin origin, PluginVerification expected)
    {
        var state = new PluginState(
            "fixture-view",
            origin,
            "fixture-view",
            "1.0.0",
            PluginStatus.Loaded,
            RejectionReason: null,
            UiModule: "index.js",
            Provides: [],
            Contracts: [],
            HasDotnet: false,
            HasUi: true);

        var verification = state.Verification;

        verification.Should().Be(expected);
    }

    #endregion Public Methods
}
