using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using NodaTime;

using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginHost;

/// <summary>
/// Asks the registered sources for their facts. Each source runs on its own, under a time limit and a
/// <c>try/catch</c>: a source that fails, is too slow or answers nonsense is reported as such, and the
/// others are unaffected (ADR-0015, decision 4). A source is never asked again while its previous call
/// is still running: callers share that call, and a call that outlived the time limit is reported as
/// timed out without starting another one, so a stuck source costs one thread, not one per request.
/// </summary>
public sealed class DiagnosticFactCollector
{
    #region Fields

    private const int MAXIMUM_LABEL_LENGTH = 200;
    private const int MAXIMUM_TEXT_LENGTH = 1000;

    // The range of System.DateTimeOffset, and so of the instants the contract can carry.
    private static readonly Instant _earliestInstant = Instant.FromUtc(1, 1, 1, 0, 0);
    private static readonly Instant _latestInstant = Instant.FromDateTimeOffset(DateTimeOffset.MaxValue);

    private readonly ExtensionRegistry _registry;
    private readonly PluginHostOptions _options;
    private readonly ILogger<DiagnosticFactCollector> _logger;
    private readonly ConcurrentDictionary<IDiagnosticFactSource, Lazy<FactCall>> _inFlight = new();

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Creates the collector.
    /// </summary>
    /// <param name="registry">Where the sources are registered.</param>
    /// <param name="options">The time and size limits.</param>
    /// <param name="logger">Where failures are written.</param>
    public DiagnosticFactCollector(
        ExtensionRegistry registry,
        PluginHostOptions options,
        ILogger<DiagnosticFactCollector> logger)
    {
        _registry = registry;
        _options = options;
        _logger = logger;
    }

    #endregion Constructors

    #region Private Methods

    // What the interface gets from a source it cannot show: the reason, and none of its facts.
    private static DiagnosticFactSourceResult WithoutFacts(string sourceId, DiagnosticFactOutcome outcome) =>
        new(sourceId, outcome, [], Truncated: false);

    // The label prefix keeps a plugin out of the host's messages: its translations are loaded under its own identifier.
    private static string? FindProblem(string ownerId, IReadOnlyList<DiagnosticFact> facts)
    {
        var prefix = ownerId + "-";
        foreach (var fact in facts)
        {
            if (fact is null || fact.Value is null)
            {
                return "a fact is missing";
            }

            if (fact.LabelId is not { Length: > 0 } label || label.Length > MAXIMUM_LABEL_LENGTH
                || !label.StartsWith(prefix, StringComparison.Ordinal))
            {
                return "a label does not start with the plugin identifier";
            }

            var problem = fact.Value.Kind switch
            {
                DiagnosticFactKind.Text when fact.Value.Text.Length > MAXIMUM_TEXT_LENGTH => "a text is too long",
                DiagnosticFactKind.Number when !double.IsFinite(fact.Value.Number) => "a number is not finite",
                DiagnosticFactKind.Instant when fact.Value.Instant < _earliestInstant || fact.Value.Instant > _latestInstant =>
                    "an instant is outside the supported dates",
                _ => null,
            };

            if (problem is not null)
            {
                return problem;
            }
        }

        return null;
    }

    private FactCall StartCall(IDiagnosticFactSource source)
    {
        var cancellation = new CancellationTokenSource();

        // On the thread pool, so that a source that blocks before returning a task cannot hold the caller.
        var task = Task.Run(() => source.GetFactsAsync(cancellation.Token), CancellationToken.None);
        var call = new FactCall(task, cancellation);

        // Once the call ends, whatever the way, the source can be asked again. The failure of a call nobody
        // waits for any more is observed here, so that it never goes unobserved.
        _ = task.ContinueWith(
            finished =>
            {
                _ = finished.Exception;
                _inFlight.TryRemove(source, out _);
                cancellation.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return call;
    }

    // A callback a plugin registered on its token may throw or block: it must cost neither the caller nor the host.
    private static void SignalTimeout(CancellationTokenSource cancellation) =>
        _ = Task.Run(() =>
        {
            try
            {
                cancellation.Cancel();
            }
            catch (Exception exception) when (exception is AggregateException or ObjectDisposedException)
            {
                // The call is being given up on: a callback that fails to be notified changes nothing.
            }
        });

    private async Task<DiagnosticFactSourceResult> CollectOneAsync(
        DiagnosticFactSourceRegistration registration,
        CancellationToken cancellationToken)
    {
        var id = registration.OwnerId;
        var call = _inFlight.GetOrAdd(registration.Source, source => new Lazy<FactCall>(() => StartCall(source))).Value;

        // A call that already outlived its time limit is still running: do not wait for it again.
        if (call.IsOverdue)
        {
            PluginHostLog.FactSourceStillRunning(_logger, id);
            return WithoutFacts(id, DiagnosticFactOutcome.TimedOut);
        }

        try
        {
            var facts = await call.Task.WaitAsync(_options.FactTimeout, cancellationToken);
            return Validate(id, facts);
        }
        // Only the wait that ran out is a time-out: a source may throw a TimeoutException of its own.
        catch (TimeoutException) when (!call.Task.IsCompleted)
        {
            call.MarkOverdue();
            SignalTimeout(call.Cancellation);
            PluginHostLog.FactSourceTimedOut(_logger, id, _options.FactTimeout.TotalMilliseconds);
            return WithoutFacts(id, DiagnosticFactOutcome.TimedOut);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller gave up: not a failure of the source.
            throw;
        }
        // A plugin is a boundary: whatever it throws only costs its own facts.
        catch (Exception exception)
        {
            PluginHostLog.FactSourceFailed(_logger, id, exception);
            return WithoutFacts(id, DiagnosticFactOutcome.Failed);
        }
    }

    private DiagnosticFactSourceResult Validate(string id, IReadOnlyList<DiagnosticFact>? facts)
    {
        if (facts is null)
        {
            PluginHostLog.FactSourceInvalid(_logger, id, "no list of facts");
            return WithoutFacts(id, DiagnosticFactOutcome.Invalid);
        }

        var truncated = facts.Count > _options.MaximumFactsPerSource;
        if (truncated)
        {
            PluginHostLog.FactSourceTruncated(_logger, id, facts.Count, _options.MaximumFactsPerSource);
        }

        // Always a copy: the list belongs to the plugin, which may change it while the host reads it twice.
        IReadOnlyList<DiagnosticFact> kept = [.. facts.Take(_options.MaximumFactsPerSource)];
        if (FindProblem(id, kept) is { } problem)
        {
            PluginHostLog.FactSourceInvalid(_logger, id, problem);
            return WithoutFacts(id, DiagnosticFactOutcome.Invalid);
        }

        PluginHostLog.FactSourceCollected(_logger, id, kept.Count);
        return new DiagnosticFactSourceResult(id, DiagnosticFactOutcome.Succeeded, kept, truncated);
    }

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Asks the sources for their facts, all at the same time: the slowest one decides how long this takes,
    /// never more than the time limit.
    /// </summary>
    /// <param name="sourceId">The source to ask, or null to ask every one.</param>
    /// <param name="cancellationToken">Cancelled when the caller no longer waits.</param>
    /// <returns>One result per source asked, in registration order.</returns>
    public async Task<IReadOnlyList<DiagnosticFactSourceResult>> CollectAsync(
        string? sourceId,
        CancellationToken cancellationToken)
    {
        var registrations = _registry.DiagnosticFactSources
            .Where(registration => sourceId is null || registration.OwnerId == sourceId);

        return await Task.WhenAll(registrations.Select(registration => CollectOneAsync(registration, cancellationToken)));
    }

    #endregion Public Methods

    #region Nested Types

    // One running call to a source, shared by everyone who asks while it runs.
    private sealed class FactCall(Task<IReadOnlyList<DiagnosticFact>> task, CancellationTokenSource cancellation)
    {
        private volatile bool _overdue;

        public Task<IReadOnlyList<DiagnosticFact>> Task { get; } = task;

        public CancellationTokenSource Cancellation { get; } = cancellation;

        // True once a caller gave up waiting for this call and it has not ended since.
        public bool IsOverdue => _overdue && !Task.IsCompleted;

        public void MarkOverdue() => _overdue = true;
    }

    #endregion Nested Types
}
