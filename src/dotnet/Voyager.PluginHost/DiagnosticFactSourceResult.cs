using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginHost;

/// <summary>
/// What one source gave when the host asked for its facts.
/// </summary>
/// <param name="SourceId">The identifier of the plugin that provides the source, or <see cref="PluginCatalog.SERVICE_ID"/>.</param>
/// <param name="Outcome">How the source answered.</param>
/// <param name="Facts">The facts that were kept; empty unless the outcome is <see cref="DiagnosticFactOutcome.Succeeded"/>.</param>
/// <param name="Truncated">True when the source gave more facts than the limit and the last ones were left out.</param>
public sealed record DiagnosticFactSourceResult(
    string SourceId,
    DiagnosticFactOutcome Outcome,
    IReadOnlyList<DiagnosticFact> Facts,
    bool Truncated);
