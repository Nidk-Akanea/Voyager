using Voyager.Sdk.Diagnostics;

namespace Voyager.PluginHost;

/// <summary>
/// Where the plugins' contributions are kept, service side: the application only offers what the
/// registry contains. Registrations are accepted at any time and reads never block (a snapshot is
/// swapped on each write), so that adding a plugin while the application runs can come later without
/// redoing this (ADR-0015).
/// </summary>
public sealed class ExtensionRegistry
{
    #region Fields

    private readonly Lock _writeLock = new();
    private IReadOnlyList<DiagnosticFactSourceRegistration> _diagnosticFactSources = [];

    #endregion Fields

    #region Properties

    /// <summary>The registered sources of diagnostic facts, in registration order.</summary>
    public IReadOnlyList<DiagnosticFactSourceRegistration> DiagnosticFactSources => _diagnosticFactSources;

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Registers a source of diagnostic facts.
    /// </summary>
    /// <param name="ownerId">The plugin that provides it, or the reserved identifier of the service itself.</param>
    /// <param name="source">The source.</param>
    public void AddDiagnosticFactSource(string ownerId, IDiagnosticFactSource source)
    {
        lock (_writeLock)
        {
            _diagnosticFactSources = [.. _diagnosticFactSources, new DiagnosticFactSourceRegistration(ownerId, source)];
        }
    }

    #endregion Public Methods
}
