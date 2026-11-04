using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NodaTime;

using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginHost.Tests;

[TestClass]
public sealed class DiagnosticFactCollectorTests
{
    #region Fields

    private static readonly ContractSupport _contracts = ContractSupport.Application;

    // Short enough for the test of a source that never answers, long enough for a source that answers at once on a busy machine.
    private static readonly PluginHostOptions _shortTimeout = new(TimeSpan.FromSeconds(1), 50);

    // For a source that the test holds past its time limit; long enough for a test to give up on another call first.
    private static readonly PluginHostOptions _shortWait = new(TimeSpan.FromSeconds(1), 50);

    private const string GATED_ID = "fixture-gated";

    private static readonly PluginKey _gated = new(PluginOrigin.Installed, GATED_ID);

    #endregion Fields

    #region Private Methods

    private static DiagnosticFactCollector CreateCollector(PluginCatalog catalog, PluginHostOptions? options = null) =>
        new(catalog.Registry, options ?? PluginHostOptions.Default, NullLogger<DiagnosticFactCollector>.Instance);

    private static PluginCatalog LoadFixtures(PluginFolder folder, params string[] fixtures)
    {
        foreach (var fixture in fixtures)
        {
            var id = ToPluginId(fixture);
            folder.AddPlugin(id, fixture, PluginFolder.Manifest(id, $"Voyager.PluginFixtures.{fixture}.dll"));
        }

        return PluginCatalog.Load(folder.Path, _contracts, NullLoggerFactory.Instance);
    }

    // A collector that traces its calls, with these plugins loaded, and nothing registered yet.
    private static (DiagnosticFactCollector Collector, ExtensionRegistry Registry, PluginActivity Activity, string File) TracingCollector(
        UserPluginFolder user,
        PluginHostOptions? options = null,
        params PluginKey[] loaded)
    {
        var file = new PluginLocations(null, user.Path).ActivityFile!;
        var activity = new PluginActivity(file, NullLogger<PluginActivity>.Instance);
        foreach (var plugin in loaded)
        {
            activity.BeginLoad(plugin);
            activity.EndLoad(plugin, loaded: true);
        }

        var registry = new ExtensionRegistry();
        var collector = new DiagnosticFactCollector(registry, options ?? PluginHostOptions.Default, NullLogger<DiagnosticFactCollector>.Instance, activity);
        return (collector, registry, activity, file);
    }

    // The calls the trace names once the service lets go of it, as after a crash; null when a write took the file back
    // in the meantime.
    private static IReadOnlyList<PluginKey>? TracedCalls(PluginActivity activity, string file)
    {
        activity.Dispose();
        try
        {
            return PluginStoreFiles.ReadActivity(file).Calls;
        }
        catch (IOException)
        {
            return null;
        }
    }

    // The end of a call is noted by a continuation of the call itself, which may run just after the caller resumes.
    private static async Task<bool> EventuallyAsync(Func<bool> condition)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed > TimeSpan.FromSeconds(5))
            {
                return false;
            }

            await Task.Delay(10);
        }

        return true;
    }

    private static Task<bool> NoLongerTracedAsync(PluginActivity activity, string file) =>
        EventuallyAsync(() => TracedCalls(activity, file) is { Count: 0 });

    // Starts a call to a source and gives up waiting for it at once: the call goes on, traced, and never reaches its time
    // limit, which only a caller that still waits can see.
    private static async Task StartThenGiveUpAsync(DiagnosticFactCollector collector, string sourceId, GatedSource source)
    {
        using var cancellation = new CancellationTokenSource();
        var collect = collector.CollectAsync(sourceId, cancellation.Token);
        await source.Started.WaitAsync(TimeSpan.FromSeconds(10));
        await cancellation.CancelAsync();
        var wait = async () => await collect;
        await wait.Should().ThrowAsync<OperationCanceledException>();
    }

    // "ThrowingSource" becomes "fixture-throwing-source".
    private static string ToPluginId(string fixture) =>
        "fixture-" + string.Concat(fixture.Select((letter, index) =>
            index > 0 && char.IsUpper(letter) ? "-" + char.ToLowerInvariant(letter) : char.ToLowerInvariant(letter).ToString()));

    #endregion Private Methods

    #region Public Methods

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
    public async Task CollectAsync_FactWhoseLabelDoesNotStartWithThePluginId_IsInvalid()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder, "BadLabel");

        var result = (await CreateCollector(catalog).CollectAsync(null, CancellationToken.None)).Single();

        result.Outcome.Should().Be(DiagnosticFactOutcome.Invalid);
        result.Facts.Should().BeEmpty("a plugin must not shadow a message of the host");
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
    public async Task CollectAsync_SourceAddedAfterLoading_IsAskedToo()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder, "Good");
        catalog.Registry.AddDiagnosticFactSource("fixture-late", new LateSource());

        var results = await CreateCollector(catalog).CollectAsync(null, CancellationToken.None);

        results.Select(result => result.SourceId).Should().Equal("fixture-good", "fixture-late");
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
    public async Task CollectAsync_CallToAPluginInProgress_IsTraced()
    {
        using var user = new UserPluginFolder();
        var (collector, registry, activity, file) = TracingCollector(user, loaded: _gated);
        using var _ = activity;
        var source = new GatedSource();
        registry.AddDiagnosticFactSource(GATED_ID, source);

        var collect = collector.CollectAsync(null, CancellationToken.None);
        await source.Started.WaitAsync(TimeSpan.FromSeconds(10));

        TracedCalls(activity, file).Should().Equal(_gated);
        source.Release();
        await collect;
    }

    [TestMethod]
    public async Task CollectAsync_CallThatSucceeded_IsNoLongerTraced()
    {
        using var user = new UserPluginFolder();
        var (collector, registry, activity, file) = TracingCollector(user, loaded: _gated);
        using var _ = activity;
        var source = new GatedSource();
        registry.AddDiagnosticFactSource(GATED_ID, source);
        var collect = collector.CollectAsync(null, CancellationToken.None);
        await source.Started.WaitAsync(TimeSpan.FromSeconds(10));

        source.Release();
        var results = await collect;

        results.Single().Outcome.Should().Be(DiagnosticFactOutcome.Succeeded);
        (await NoLongerTracedAsync(activity, file)).Should().BeTrue("the call ended");
    }

    [TestMethod]
    public async Task CollectAsync_CallThatThrew_IsNoLongerTraced()
    {
        using var user = new UserPluginFolder();
        var plugin = new PluginKey(PluginOrigin.Installed, "fixture-own-timeout");
        var (collector, registry, activity, file) = TracingCollector(user, loaded: plugin);
        using var _ = activity;
        registry.AddDiagnosticFactSource(plugin.Id, new TimeoutThrowingSource());

        var results = await collector.CollectAsync(null, CancellationToken.None);

        results.Single().Outcome.Should().Be(DiagnosticFactOutcome.Failed);
        (await NoLongerTracedAsync(activity, file)).Should().BeTrue("the call ended");
    }

    [TestMethod]
    public async Task CollectAsync_CallPastItsTimeLimit_IsNoLongerTracedThoughItStillRuns()
    {
        using var user = new UserPluginFolder();
        var (collector, registry, activity, file) = TracingCollector(user, _shortWait, _gated);
        using var _ = activity;
        var source = new GatedSource();
        registry.AddDiagnosticFactSource(GATED_ID, source);

        var results = await collector.CollectAsync(null, CancellationToken.None);

        results.Single().Outcome.Should().Be(DiagnosticFactOutcome.TimedOut);
        TracedCalls(activity, file).Should().BeEmpty("a call past its time limit is reported as such, not as a crash");
        source.Release();
    }

    [TestMethod]
    public async Task CollectAsync_CallPastItsTimeLimitThenACallToAnotherPlugin_OnlyTheOtherIsDesignated()
    {
        using var user = new UserPluginFolder();
        var map = new PluginKey(PluginOrigin.Shipped, "fixture-map");
        var (collector, registry, activity, file) = TracingCollector(user, _shortWait, _gated, map);
        using var _ = activity;
        var stuck = new GatedSource();
        registry.AddDiagnosticFactSource(GATED_ID, stuck);
        await collector.CollectAsync(GATED_ID, CancellationToken.None);
        var other = new GatedSource();
        registry.AddDiagnosticFactSource(map.Id, other);
        await StartThenGiveUpAsync(collector, map.Id, other);

        activity.Dispose();
        var suspect = PluginActivity.FindSuspect(file, NullLogger.Instance);

        suspect.Should().Be(map);
        other.Release();
        stuck.Release();
    }

    [TestMethod]
    public async Task CollectAsync_CallPastItsTimeLimitThatEndsLater_DoesNotEndTheTraceOfAnotherCallToTheSamePlugin()
    {
        using var user = new UserPluginFolder();
        var (collector, registry, activity, file) = TracingCollector(user, _shortWait, _gated);
        using var _ = activity;
        var stuck = new GatedSource();
        var second = new GatedSource();
        registry.AddDiagnosticFactSource(GATED_ID, stuck);
        await collector.CollectAsync(null, CancellationToken.None);
        registry.AddDiagnosticFactSource(GATED_ID, second);
        await StartThenGiveUpAsync(collector, GATED_ID, second);

        stuck.Release();
        await EventuallyAsync(() => stuck.Finished);
        await Task.Delay(200);

        TracedCalls(activity, file).Should().Equal(_gated);
        second.Release();
    }

    [TestMethod]
    public async Task CollectAsync_CallerThatGivesUp_LeavesTheCallTracedWhileItRuns()
    {
        using var user = new UserPluginFolder();
        var (collector, registry, activity, file) = TracingCollector(user, loaded: _gated);
        using var _ = activity;
        var source = new GatedSource();
        registry.AddDiagnosticFactSource(GATED_ID, source);
        using var cancellation = new CancellationTokenSource();
        var collect = collector.CollectAsync(null, cancellation.Token);
        await source.Started.WaitAsync(TimeSpan.FromSeconds(10));

        await cancellation.CancelAsync();

        var wait = async () => await collect;
        await wait.Should().ThrowAsync<OperationCanceledException>();
        TracedCalls(activity, file).Should().Equal(_gated);
        source.Release();
        (await NoLongerTracedAsync(activity, file)).Should().BeTrue("the call ended");
    }

    [TestMethod]
    public async Task CollectAsync_SourceOfTheService_IsNotTraced()
    {
        using var user = new UserPluginFolder();
        var (collector, registry, activity, file) = TracingCollector(user, loaded: _gated);
        using var _ = activity;
        var source = new GatedSource();
        registry.AddDiagnosticFactSource(PluginCatalog.SERVICE_ID, source);

        var collect = collector.CollectAsync(null, CancellationToken.None);
        await source.Started.WaitAsync(TimeSpan.FromSeconds(10));

        TracedCalls(activity, file).Should().BeEmpty("a crash during a call to the service designates no plugin");
        source.Release();
        await collect;
    }

    [TestMethod]
    public async Task CollectAsync_ListThatRunsCodeOfThePluginAsItIsRead_IsReadWhileTheCallIsTraced()
    {
        using var user = new UserPluginFolder();
        var (collector, registry, activity, file) = TracingCollector(user, loaded: _gated);
        using var _ = activity;
        var source = new ListSource(() => TracedCalls(activity, file));
        registry.AddDiagnosticFactSource(GATED_ID, source);

        var results = await collector.CollectAsync(null, CancellationToken.None);

        results.Single().Outcome.Should().Be(DiagnosticFactOutcome.Succeeded);
        source.TracedWhileRead.Should().Equal([_gated], "the plugin's list runs its code as the host reads it");
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
    public async Task CollectAsync_WithASourceId_AsksOnlyThatSource()
    {
        using var folder = new PluginFolder();
        using var catalog = LoadFixtures(folder, "Good", "ThrowingSource");

        var results = await CreateCollector(catalog).CollectAsync("fixture-good", CancellationToken.None);

        results.Should().ContainSingle().Which.SourceId.Should().Be("fixture-good");
    }

    #endregion Public Methods

    #region Classes

    private sealed class CountingStuckSource : IDiagnosticFactSource
    {
        #region Fields

        private int _calls;

        #endregion Fields

        #region Properties

        public int Calls => Volatile.Read(ref _calls);

        #endregion Properties

        #region Public Methods

        public async Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            await Task.Delay(Timeout.Infinite, CancellationToken.None);
            return [];
        }

        #endregion Public Methods
    }

    private sealed class FixedSource(string label, DiagnosticFactValue value) : IDiagnosticFactSource
    {
        #region Public Methods

        public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DiagnosticFact>>([new(label, value)]);

        #endregion Public Methods
    }

    // Answers only once released, and counts how many times it was asked.
    private sealed class GatedSource : IDiagnosticFactSource
    {
        #region Fields

        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _calls;
        private bool _finished;

        #endregion Fields

        #region Properties

        public int Calls => Volatile.Read(ref _calls);

        // Completes once the source is asked: the host has started the call by then.
        public Task Started => _started.Task;

        // Whether the source returned its facts.
        public bool Finished => Volatile.Read(ref _finished);

        #endregion Properties

        #region Public Methods

        public async Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            _started.TrySetResult();
            await _gate.Task;
            Volatile.Write(ref _finished, true);
            return [new("fixture-gated-fact", DiagnosticFactValue.FromText("done"))];
        }

        public void Release() => _gate.SetResult();

        #endregion Public Methods
    }

    // Returns a list of its own type, which looks at the trace when the host reads it.
    private sealed class ListSource(Func<IReadOnlyList<PluginKey>?> readTrace) : IDiagnosticFactSource
    {
        #region Fields

        private readonly Func<IReadOnlyList<PluginKey>?> _readTrace = readTrace;

        #endregion Fields

        #region Properties

        public IReadOnlyList<PluginKey>? TracedWhileRead { get; private set; }

        #endregion Properties

        #region Public Methods

        public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DiagnosticFact>>(new WatchingList(this));

        #endregion Public Methods

        #region Classes

        private sealed class WatchingList(ListSource owner) : IReadOnlyList<DiagnosticFact>
        {
            private readonly DiagnosticFact _fact = new("fixture-gated-fact", DiagnosticFactValue.FromText("read"));

            public int Count => 1;

            public DiagnosticFact this[int index]
            {
                get
                {
                    owner.TracedWhileRead ??= owner._readTrace();
                    return index == 0 ? _fact : throw new ArgumentOutOfRangeException(nameof(index));
                }
            }

            public IEnumerator<DiagnosticFact> GetEnumerator()
            {
                owner.TracedWhileRead ??= owner._readTrace();
                yield return _fact;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        #endregion Classes
    }

    private sealed class LateSource : IDiagnosticFactSource
    {
        #region Public Methods

        public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DiagnosticFact>>([new("fixture-late-fact", DiagnosticFactValue.FromText("late"))]);

        #endregion Public Methods
    }

    // Registers a callback on its token that throws when the host cancels the call.
    private sealed class ThrowingCallbackSource : IDiagnosticFactSource
    {
        #region Public Methods

        public async Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(() => throw new InvalidOperationException("The callback is broken."));
            await Task.Delay(Timeout.Infinite, CancellationToken.None);
            return [];
        }

        #endregion Public Methods
    }

    private sealed class TimeoutThrowingSource : IDiagnosticFactSource
    {
        #region Public Methods

        public Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken) =>
            throw new TimeoutException("The web call of the plugin timed out.");

        #endregion Public Methods
    }

    #endregion Classes
}
