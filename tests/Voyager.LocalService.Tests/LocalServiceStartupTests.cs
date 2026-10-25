using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Voyager.LocalService.Api;

namespace Voyager.LocalService.Tests;

/// <summary>
/// Settings and protections the local service puts in place when it starts (Program.cs).
/// </summary>
[TestClass]
public sealed class LocalServiceStartupTests
{
    private const string GET_SERVICE_INFO_PATH = "/voyager.diagnostics.v1.DiagnosticsService/GetServiceInfo";

    [TestMethod]
    public void Startup_WithoutLocalToken_Fails()
    {
        using var factory = new LocalServiceFactory(localToken: null);

        var start = () => factory.Server;

        start.Should().Throw<OptionsValidationException>();
    }

    [TestMethod]
    public void Startup_WithTooShortLocalToken_Fails()
    {
        using var factory = new LocalServiceFactory(localToken: "too-short");

        var start = () => factory.Server;

        start.Should().Throw<OptionsValidationException>();
    }

    [TestMethod]
    public async Task Cors_FromPackagedUserInterface_IsAllowed()
    {
        using var factory = new LocalServiceFactory();
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(Preflight(LocalServiceOptions.PACKAGED_UI_ORIGIN));

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal(LocalServiceOptions.PACKAGED_UI_ORIGIN);
    }

    [TestMethod]
    public async Task Cors_FromAnotherOrigin_IsNotAllowed()
    {
        using var factory = new LocalServiceFactory();
        using var client = factory.CreateClient();

        using var response = await client.SendAsync(Preflight("https://example.com"));

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    /// <summary>What a browser sends before a gRPC-Web call from another origin.</summary>
    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, GET_SERVICE_INFO_PATH);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", $"content-type,x-grpc-web,{LocalTokenInterceptor.TOKEN_HEADER}");
        return request;
    }
}
