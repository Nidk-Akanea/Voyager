namespace Voyager.Sdk.Diagnostics;

/// <summary>
/// Provides facts about the running service. The host asks every source when the "About" page opens.
/// </summary>
/// <remarks>
/// <para>
/// A source is isolated from the others: one that throws, answers too slowly or answers facts the host
/// refuses is shown as being in error, and the other sources are unaffected.
/// </para>
/// <para>
/// The host never calls a source again while its previous call has not finished, and it shares that
/// call between callers that ask at the same time. So <see cref="GetFactsAsync"/> is never running
/// twice at once on the same instance, and a source that is slow or stuck costs the host one call, not
/// one per request. A call that outlived the time limit is reported as timed out until it ends.
/// </para>
/// <para>
/// The host also checks what a source answers against the rules of <see cref="DiagnosticFact"/> and
/// <see cref="DiagnosticFactValue"/>: a source that breaks one is reported as invalid and none of its
/// facts are shown. The time limit is 5 seconds and the number of facts kept is 50.
/// </para>
/// </remarks>
public interface IDiagnosticFactSource
{
    /// <summary>
    /// Returns the current facts. Fact labels must start with the identifier of the plugin followed by
    /// a hyphen, as the plugin's own translations do; at most 50 facts are kept and the answer is
    /// expected within a few seconds.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancelled when the host stops waiting for the answer. Honour it: a call that ignores it keeps
    /// running, and the source is not asked again until it ends.
    /// </param>
    /// <returns>The facts, in the order they should be shown; never <see langword="null"/>.</returns>
    Task<IReadOnlyList<DiagnosticFact>> GetFactsAsync(CancellationToken cancellationToken);
}
