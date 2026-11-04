using System.Net;

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
    [DataRow("10.0.0.7", DisplayName = "IPv4 of another machine")]
    [DataRow("2001:db8::7", DisplayName = "IPv6 of another machine")]
    public async Task Call_WithDevelopmentIdentityFromAnotherMachine_IsUnauthenticated(string address)
    {
        using var factory = new GroupServerFactory(remoteAddress: IPAddress.Parse(address));

        var call = async () => await new DiagnosticsService.DiagnosticsServiceClient(factory.CreateChannel())
            .GetServiceInfoAsync(new GetServiceInfoRequest());

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }

    [TestMethod]
    [DataRow("127.0.0.1")]
    [DataRow("::1")]
    public async Task Call_WithDevelopmentIdentityFromThisMachine_IsServed(string address)
    {
        using var factory = new GroupServerFactory(remoteAddress: IPAddress.Parse(address));

        var response = await new DiagnosticsService.DiagnosticsServiceClient(factory.CreateChannel())
            .GetServiceInfoAsync(new GetServiceInfoRequest());

        response.Environment.Should().Be("Development");
    }

    [TestMethod]
    [DataRow("0.4", DisplayName = "without a patch number")]
    [DataRow("v0.4.0", DisplayName = "with a v")]
    [DataRow("latest", DisplayName = "not a version")]
    public void Startup_UnreadableMinimumAppVersion_Fails(string minimum)
    {
        using var factory = new GroupServerFactory(minimumAppVersion: minimum);

        var start = () => factory.Server;

        start.Should().Throw<OptionsValidationException>().WithMessage("*MinimumAppVersion must be a version*");
    }

    [TestMethod]
    public void Startup_InvalidConnectionString_FailsWithoutQuotingIt()
    {
        using var factory = new GroupServerFactory(connectionString: "Host=localhost;Password=secret-of-test;NoSuchKeyword=1");

        var start = () => factory.Server;

        start.Should().Throw<ArgumentException>()
            .WithMessage("The connection string of the database is not valid.*")
            .Which.ToString().Should().NotContain("secret-of-test");
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
