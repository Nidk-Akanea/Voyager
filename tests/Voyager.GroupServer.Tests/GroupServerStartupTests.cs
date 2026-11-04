using AwesomeAssertions;

using Grpc.Core;

using Microsoft.Extensions.Options;

using Voyager.Contracts.Diagnostics.V1;

namespace Voyager.GroupServer.Tests;

/// <summary>
/// The protections the group server puts in place when it starts (Program.cs): the development settings and the
/// authentication (ADR-0019, decision 9).
/// </summary>
[TestClass]
public sealed class GroupServerStartupTests
{
    #region Public Methods

    [TestMethod]
    public async Task Call_WithDevelopmentIdentity_IsServed()
    {
        using var factory = new GroupServerFactory();

        var response = await new DiagnosticsService.DiagnosticsServiceClient(factory.CreateChannel())
            .GetServiceInfoAsync(new GetServiceInfoRequest());

        response.Environment.Should().Be("Development");
    }

    [TestMethod]
    public async Task Call_WithoutDevelopmentIdentity_IsUnauthenticated()
    {
        using var factory = new GroupServerFactory(developmentIdentity: false);

        var call = async () => await new DiagnosticsService.DiagnosticsServiceClient(factory.CreateChannel())
            .GetServiceInfoAsync(new GetServiceInfoRequest());

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }

    [TestMethod]
    [DataRow("Production")]
    [DataRow("Staging")]
    public async Task Call_DevelopmentIdentityOffOutsideDevelopment_IsUnauthenticated(string environment)
    {
        using var factory = new GroupServerFactory(environment: environment, developmentIdentity: false);

        var call = async () => await new DiagnosticsService.DiagnosticsServiceClient(factory.CreateChannel())
            .GetServiceInfoAsync(new GetServiceInfoRequest());

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }

    [TestMethod]
    [DataRow("Production")]
    [DataRow("Staging")]
    public void Startup_DevelopmentIdentityOutsideDevelopment_Fails(string environment)
    {
        using var factory = new GroupServerFactory(environment: environment, developmentIdentity: true);

        var start = () => factory.Server;

        start.Should().Throw<OptionsValidationException>()
            .WithMessage("*DevelopmentIdentity is only allowed in the Development environment*");
    }

    [TestMethod]
    public void Startup_PluginsFolderOutsideDevelopment_Fails()
    {
        using var plugins = new Voyager.PluginHost.Tests.PluginFolder();
        using var factory = new GroupServerFactory(environment: "Production", developmentIdentity: false, shippedPluginsDirectory: plugins.Path);

        var start = () => factory.Server;

        start.Should().Throw<OptionsValidationException>()
            .WithMessage("*ShippedPluginsDirectory is only allowed in the Development environment*");
    }

    [TestMethod]
    public void Startup_RelativePluginsFolder_Fails()
    {
        using var factory = new GroupServerFactory(shippedPluginsDirectory: "plugins");

        var start = () => factory.Server;

        start.Should().Throw<OptionsValidationException>().WithMessage("*must be a full path*");
    }

    #endregion Public Methods
}
