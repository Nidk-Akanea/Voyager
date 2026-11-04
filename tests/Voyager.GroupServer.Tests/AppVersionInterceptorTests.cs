using AwesomeAssertions;

using Grpc.Core;

using Voyager.Contracts.Diagnostics.V1;
using Voyager.GroupServer.Api;

namespace Voyager.GroupServer.Tests;

/// <summary>
/// The server refuses a desktop app older than the minimum version it serves (ADR-0018, decision 6).
/// </summary>
[TestClass]
public sealed class AppVersionInterceptorTests
{
    #region Private Methods

    private static async Task<GetServiceInfoResponse> CallAsync(GroupServerFactory factory, string? appVersion) =>
        await new DiagnosticsService.DiagnosticsServiceClient(factory.CreateChannel()).GetServiceInfoAsync(
            new GetServiceInfoRequest(),
            appVersion is null ? null : GroupServerFactory.AppVersionHeader(appVersion));

    #endregion Private Methods

    #region Public Methods

    [TestMethod]
    [DataRow(null, DisplayName = "no version given")]
    [DataRow("0.0.1", DisplayName = "any version")]
    public async Task Call_WithoutMinimum_IsServed(string? appVersion)
    {
        using var factory = new GroupServerFactory();

        var response = await CallAsync(factory, appVersion);

        response.Version.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    [DataRow("0.4.0", DisplayName = "the minimum")]
    [DataRow("0.4.0-beta.2", DisplayName = "a prerelease of the minimum")]
    [DataRow("0.10.0", DisplayName = "a later minor, compared as a number")]
    [DataRow("1.0.0+abc123", DisplayName = "a later version with build metadata")]
    public async Task Call_RecentEnoughApp_IsServed(string appVersion)
    {
        using var factory = new GroupServerFactory(minimumAppVersion: "0.4.0");

        var response = await CallAsync(factory, appVersion);

        response.Version.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    [DataRow("0.3.9", DisplayName = "older")]
    [DataRow(null, DisplayName = "no version given")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("not a version", DisplayName = "unreadable")]
    [DataRow("0.4", DisplayName = "without a patch number")]
    public async Task Call_TooOldOrUnknownApp_IsRefusedWithAClearMessage(string? appVersion)
    {
        using var factory = new GroupServerFactory(minimumAppVersion: "0.4.0");

        var call = async () => await CallAsync(factory, appVersion);

        var exception = (await call.Should().ThrowAsync<RpcException>()).Which;
        exception.StatusCode.Should().Be(StatusCode.FailedPrecondition);
        exception.Status.Detail.Should().Be(AppVersionInterceptor.TOO_OLD_MESSAGE);
    }

    #endregion Public Methods
}
