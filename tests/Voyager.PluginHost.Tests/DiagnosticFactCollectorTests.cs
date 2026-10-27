using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NodaTime;

using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginHost.Tests;

[TestClass]
public sealed class DiagnosticFactCollectorTests
{
    private static readonly Version _sdkVersion = new(0, 1, 0);

    // Short enough for the test of a source that never answers, long enough for a source that answers at once on a busy machine.
    private static readonly PluginHostOptions _shortTimeout = new(TimeSpan.FromSeconds(1), 50);

    private static DiagnosticFactCollector CreateCollector(PluginCatalog catalog, PluginHostOptions? options = null) =>
        new(catalog.Registry, options ?? PluginHostOptions.Default, NullLogger<DiagnosticFactCollector>.Instance);

    // "ThrowingSource" becomes "fixture-throwing-source".
    private static string ToPluginId(string fixture) =>
        "fixture-" + string.Concat(fixture.Select((letter, index) =>
            index > 0 && char.IsUpper(letter) ? "-" + char.ToLowerInvariant(letter) : char.ToLowerInvariant(letter).ToString()));

    private static PluginCatalog LoadFixtures(PluginFolder folder, params string[] fixtures)
    {
        foreach (var fixture in fixtures)
        {
            var id = ToPluginId(fixture);
            folder.AddPlugin(id, fixture, PluginFolder.Manifest(id, $"Voyager.PluginFixtures.{fixture}.dll"));
        }

        return PluginCatalog.Load(folder.Path, _sdkVersion, NullLoggerFactory.Instance);
    }

    [TestMethod]
    public async Task CollectAsync_ValidSource_ReturnsItsFactsOfEveryKind()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder, "Good");

        var results = await CreateCollector(catalog).CollectAsync(null, CancellationToken.None);

        var result = results.Should().ContainSingle().Subject;
        result.SourceId.Should().Be("fixture-good");
        result.Outcome.Should().Be(DiagnosticFactOutcome.Succeeded);
        result.Truncated.Should().BeFalse();
        result.Facts.Select(fact => (fact.LabelId, fact.Value.Kind)).Should().Equal(
            ("fixture-good-text", DiagnosticFactKind.Text),
            ("fixture-good-number", DiagnosticFactKind.Number),
            ("fixture-good-instant", DiagnosticFactKind.Instant));
        result.Facts[2].Value.Instant.Should().Be(Instant.FromUtc(2026, 3, 14, 9, 30));
    }

    [TestMethod]
    public async Task CollectAsync_SourceThatThrows_IsReportedAsFailedAndOthersAreUnaffected()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder, "Good", "ThrowingSource");

        var results = await CreateCollector(catalog).CollectAsync(null, CancellationToken.None);

        results.Select(result => (result.SourceId, result.Outcome)).Should().Equal(
            ("fixture-good", DiagnosticFactOutcome.Succeeded),
            ("fixture-throwing-source", DiagnosticFactOutcome.Failed));
        results.Single(result => result.Outcome == DiagnosticFactOutcome.Failed).Facts.Should().BeEmpty();
    }

    [TestMethod]
    public async Task CollectAsync_SourceThatNeverAnswers_IsReportedAsTimedOutWithoutBlockingTheOthers()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder, "Good", "SlowSource");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var results = await CreateCollector(catalog, _shortTimeout).CollectAsync(null, CancellationToken.None);

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        results.Select(result => (result.SourceId, result.Outcome)).Should().Equal(
            ("fixture-good", DiagnosticFactOutcome.Succeeded),
            ("fixture-slow-source", DiagnosticFactOutcome.TimedOut));
    }

    [TestMethod]
    public async Task CollectAsync_SourceWithTooManyFacts_KeepsTheFirstFiftyAndSaysSo()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder, "ManyFacts");

        var result = (await CreateCollector(catalog).CollectAsync(null, CancellationToken.None)).Single();

        result.Outcome.Should().Be(DiagnosticFactOutcome.Succeeded);
        result.Truncated.Should().BeTrue();
        result.Facts.Should().HaveCount(50);
        result.Facts[0].LabelId.Should().Be("fixture-many-facts-1");
        result.Facts[^1].LabelId.Should().Be("fixture-many-facts-50");
    }

    [TestMethod]
    public async Task CollectAsync_FactWhoseLabelDoesNotStartWithThePluginId_IsInvalid()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder, "BadLabel");

        var result = (await CreateCollector(catalog).CollectAsync(null, CancellationToken.None)).Single();

        result.Outcome.Should().Be(DiagnosticFactOutcome.Invalid);
        result.Facts.Should().BeEmpty("a plugin must not shadow a message of the host");
    }

    [TestMethod]
    public async Task CollectAsync_WithASourceId_AsksOnlyThatSource()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder, "Good", "ThrowingSource");

        var results = await CreateCollector(catalog).CollectAsync("fixture-good", CancellationToken.None);

        results.Should().ContainSingle().Which.SourceId.Should().Be("fixture-good");
    }

    [TestMethod]
    public async Task CollectAsync_UnknownSourceId_ReturnsNothing()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder, "Good");

        var results = await CreateCollector(catalog).CollectAsync("nobody", CancellationToken.None);

        results.Should().BeEmpty();
    }

    [TestMethod]
    public async Task CollectAsync_CallerThatGivesUp_GetsACancellationInsteadOfAFailureOfTheSource()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder, "SlowSource");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var collect = () => CreateCollector(catalog).CollectAsync(null, cancellation.Token);

        await collect.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    public async Task CollectAsync_SourceAddedAfterLoading_IsAskedToo()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder, "Good");
        catalog.Registry.AddDiagnosticFactSource("fixture-late", new LateSource());

        var results = await CreateCollector(catalog).CollectAsync(null, CancellationToken.None);

        results.Select(result => result.SourceId).Should().Equal("fixture-good", "fixture-late");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task CollectAsync_InstantOutsideTheSupportedDates_IsInvalidInsteadOfBreakingTheCall(bool tooEarly)
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder, "Good");
        var extreme = tooEarly ? Instant.MinValue : Instant.MaxValue;
        catalog.Registry.AddDiagnosticFactSource("fixture-extreme", new FixedSource("fixture-extreme-fact", DiagnosticFactValue.FromInstant(extreme)));

        var results = await CreateCollector(catalog).CollectAsync(null, CancellationToken.None);

        results.Select(result => (result.SourceId, result.Outcome)).Should().Equal(
            ("fixture-good", DiagnosticFactOutcome.Succeeded),
            ("fixture-extreme", DiagnosticFactOutcome.Invalid));
    }

    [TestMethod]
    public async Task CollectAsync_SourceStillRunningFromAnEarlierCall_IsNotAskedAgain()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder);
        var stuck = new CountingStuckSource();
        catalog.Registry.AddDiagnosticFactSource("fixture-stuck", stuck);
        var collector = CreateCollector(catalog, new PluginHostOptions(TimeSpan.FromMilliseconds(200), 50));

        var first = await collector.CollectAsync(null, CancellationToken.None);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var second = await collector.CollectAsync(null, CancellationToken.None);

        first.Single().Outcome.Should().Be(DiagnosticFactOutcome.TimedOut);
        second.Single().Outcome.Should().Be(DiagnosticFactOutcome.TimedOut);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(150), "a source that is still stuck is not waited for again");
        stuck.Calls.Should().Be(1, "a stuck source must not get a new thread at every request");
    }

    [TestMethod]
    public async Task CollectAsync_TwoCallersAtTheSameTime_ShareOneCallToTheSource()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder);
        var source = new GatedSource();
        catalog.Registry.AddDiagnosticFactSource("fixture-gated", source);
        var collector = CreateCollector(catalog);

        var first = collector.CollectAsync(null, CancellationToken.None);
        var second = collector.CollectAsync(null, CancellationToken.None);
        source.Release();
        var results = await Task.WhenAll(first, second);

        results.Should().AllSatisfy(result => result.Single().Outcome.Should().Be(DiagnosticFactOutcome.Succeeded));
        source.Calls.Should().Be(1, "simultaneous callers share the running call");
    }

    [TestMethod]
    public async Task CollectAsync_SourceThatThrowsATimeoutException_IsFailedNotTimedOut()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder);
        catalog.Registry.AddDiagnosticFactSource("fixture-own-timeout", new TimeoutThrowingSource());

        var result = (await CreateCollector(catalog).CollectAsync(null, CancellationToken.None)).Single();

        result.Outcome.Should().Be(DiagnosticFactOutcome.Failed, "the time-out was the plugin's own, not the host's");
    }

    [TestMethod]
    public async Task CollectAsync_SourceWhoseCancellationCallbackThrows_StillTimesOutCleanly()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder, "Good");
        catalog.Registry.AddDiagnosticFactSource("fixture-bad-callback", new ThrowingCallbackSource());
        var collector = CreateCollector(catalog, new PluginHostOptions(TimeSpan.FromMilliseconds(200), 50));

        var results = await collector.CollectAsync(null, CancellationToken.None);

        results.Select(result => (result.SourceId, result.Outcome)).Should().Equal(
            ("fixture-good", DiagnosticFactOutcome.Succeeded),
            ("fixture-bad-callback", DiagnosticFactOutcome.TimedOut));
    }

    private sealed class LateSource : IDiagnosticFactSource
    {
        public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DiagnosticFact>>([new("fixture-late-fact", DiagnosticFactValue.FromText("late"))]);
    }

    private sealed class FixedSource(string label, DiagnosticFactValue value) : IDiagnosticFactSource
    {
        public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DiagnosticFact>>([new(label, value)]);
    }

    private sealed class CountingStuckSource : IDiagnosticFactSource
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public async Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            await Task.Delay(Timeout.Infinite, CancellationToken.None);
            return [];
        }
    }

    private sealed class TimeoutThrowingSource : IDiagnosticFactSource
    {
        public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken) =>
            throw new TimeoutException("The web call of the plugin timed out.");
    }

    // Registers a callback on its token that throws when the host cancels the call.
    private sealed class ThrowingCallbackSource : IDiagnosticFactSource
    {
        public async Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(() => throw new InvalidOperationException("The callback is broken."));
            await Task.Delay(Timeout.Infinite, CancellationToken.None);
            return [];
        }
    }

    // Answers only once released, and counts how many times it was asked.
    private sealed class GatedSource : IDiagnosticFactSource
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public void Release() => _gate.SetResult();

        public async Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            await _gate.Task;
            return [new("fixture-gated-fact", DiagnosticFactValue.FromText("done"))];
        }
    }
}
