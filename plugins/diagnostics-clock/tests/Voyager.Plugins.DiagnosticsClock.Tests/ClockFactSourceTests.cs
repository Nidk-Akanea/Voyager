using AwesomeAssertions;

using NodaTime;
using NodaTime.Testing;

using Voyager.Sdk.Diagnostics;

namespace Voyager.Plugins.DiagnosticsClock.Tests;

[TestClass]
public sealed class ClockFactSourceTests
{
    private static readonly Instant _start = Instant.FromUtc(2026, 3, 14, 9, 30);

    private sealed class FixedServiceStart(Instant startedAt) : ServiceStart
    {
        public override Instant StartedAt { get; } = startedAt;
    }

    [TestMethod]
    public async Task GetFactsAsync_AfterTwoMinutes_ReturnsTheCurrentInstantAndTheUptimeInSeconds()
    {
        var now = _start.Plus(Duration.FromSeconds(125.7));
        var source = new ClockFactSource(new FakeClock(now), new FixedServiceStart(_start));

        var facts = await source.GetFactsAsync(CancellationToken.None);

        facts.Select(fact => fact.LabelId).Should().Equal("diagnostics-clock-fact-now", "diagnostics-clock-fact-uptime");
        facts[0].Value.Kind.Should().Be(DiagnosticFactKind.Instant);
        facts[0].Value.Instant.Should().Be(now);
        facts[1].Value.Kind.Should().Be(DiagnosticFactKind.Number);
        facts[1].Value.Number.Should().Be(125);
    }

    [TestMethod]
    public async Task GetFactsAsync_WhenTheClockIsBeforeTheStart_ReturnsAnUptimeOfZero()
    {
        var source = new ClockFactSource(new FakeClock(_start.Minus(Duration.FromMinutes(1))), new FixedServiceStart(_start));

        var facts = await source.GetFactsAsync(CancellationToken.None);

        facts[1].Value.Number.Should().Be(0);
    }

    [TestMethod]
    public void StartedAt_ForTheRealProcess_IsBeforeNow()
    {
        new ServiceStart().StartedAt.Should().BeLessThan(SystemClock.Instance.GetCurrentInstant());
    }
}
