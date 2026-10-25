using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenTelemetry.Instrumentation.Http;

using Serilog.Extensions.Logging;

namespace Voyager.LocalService.Tests;

/// <summary>
/// Logs written to Seq in development (Voyager.ServiceDefaults, ADR-0012), seen through the local service.
/// </summary>
[TestClass]
public sealed class SeqLoggingTests
{
    // Nothing listens on the discard port: sends fail at once, and nothing leaves the machine.
    private const string UNREACHABLE_SEQ_SERVER = "http://127.0.0.1:9";

    [TestMethod]
    public void Startup_WithoutSeqServer_WritesNoLogToSeq()
    {
        using var factory = new LocalServiceFactory();

        HasSeqProvider(factory).Should().BeFalse();
    }

    [TestMethod]
    public void Startup_WithSeqServer_WritesLogsToSeq()
    {
        using var factory = new LocalServiceFactory(seqServerUrl: UNREACHABLE_SEQ_SERVER);

        HasSeqProvider(factory).Should().BeTrue();
    }

    [TestMethod]
    public void Startup_WithSeqServerOutsideDevelopment_WritesNoLogToSeq()
    {
        using var factory = new LocalServiceFactory(seqServerUrl: UNREACHABLE_SEQ_SERVER, environment: "Production");

        HasSeqProvider(factory).Should().BeFalse();
    }

    [TestMethod]
    public void LogLevels_WithSeqServer_FollowLoggingSettings()
    {
        using var factory = new LocalServiceFactory(seqServerUrl: UNREACHABLE_SEQ_SERVER);
        var loggers = factory.Services.GetRequiredService<ILoggerFactory>();

        // appsettings.json: Information by default, Warning for Microsoft.AspNetCore. A logger is enabled
        // as soon as one provider is, so a Seq provider letting every level through would show here.
        loggers.CreateLogger("Microsoft.AspNetCore.Routing").IsEnabled(LogLevel.Information).Should().BeFalse();
        loggers.CreateLogger("Voyager.LocalService").IsEnabled(LogLevel.Debug).Should().BeFalse();
        loggers.CreateLogger("Voyager.LocalService").IsEnabled(LogLevel.Information).Should().BeTrue();
    }

    [TestMethod]
    public void HttpTraces_WithSeqServer_LeaveOutTheSendsToSeq()
    {
        using var factory = new LocalServiceFactory(seqServerUrl: UNREACHABLE_SEQ_SERVER);
        var filter = factory.Services.GetRequiredService<IOptionsMonitor<HttpClientTraceInstrumentationOptions>>()
            .Get(Options.DefaultName).FilterHttpRequestMessage;

        filter.Should().NotBeNull();
        filter!(new HttpRequestMessage(HttpMethod.Post, $"{UNREACHABLE_SEQ_SERVER}/ingest/clef")).Should().BeFalse();
        filter(new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:10/elsewhere")).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("localhost:5341", DisplayName = "No scheme")]
    [DataRow("http://192.0.2.10:5341", DisplayName = "Plain http to another machine")]
    public void Startup_WithInvalidSeqServer_Fails(string seqServerUrl)
    {
        using var factory = new LocalServiceFactory(seqServerUrl: seqServerUrl);

        var start = () => factory.Server;

        start.Should().Throw<InvalidOperationException>().WithMessage("*Seq:ServerUrl*");
    }

    private static bool HasSeqProvider(LocalServiceFactory factory) =>
        factory.Services.GetServices<ILoggerProvider>().Any(provider => provider is SerilogLoggerProvider);
}
