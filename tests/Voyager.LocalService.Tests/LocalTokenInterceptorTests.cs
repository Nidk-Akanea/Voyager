using AwesomeAssertions;

using Grpc.Core;

using Voyager.Contracts.Diagnostics.V1;

namespace Voyager.LocalService.Tests;

[TestClass]
public sealed class LocalTokenInterceptorTests
{
    [TestMethod]
    public async Task Call_WithoutLocalToken_IsRefused()
    {
        using var factory = new LocalServiceFactory();
        using var channel = factory.CreateChannel();
        var client = new DiagnosticsService.DiagnosticsServiceClient(channel);

        var call = async () => await client.GetServiceInfoAsync(new GetServiceInfoRequest());

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }

    [TestMethod]
    public async Task Call_WithWrongLocalToken_IsRefused()
    {
        using var factory = new LocalServiceFactory();
        using var channel = factory.CreateChannel();
        var client = new DiagnosticsService.DiagnosticsServiceClient(channel);

        var call = async () => await client.GetServiceInfoAsync(
            new GetServiceInfoRequest(),
            LocalServiceFactory.TokenHeader("not-the-right-token"));

        (await call.Should().ThrowAsync<RpcException>()).Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
    }
}
