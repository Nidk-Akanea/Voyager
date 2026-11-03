using NodaTime;

namespace Voyager.PluginHost;

/// <summary>
/// What the service remembers of one plugin between two starts, in <c>state.json</c>: the choices of the user and
/// where an installed plugin came from (ADR-0016, decision 4: source and date are kept).
/// </summary>
/// <param name="Plugin">The plugin.</param>
/// <param name="Enabled">Whether the plugin is loaded at startup.</param>
/// <param name="InstalledAt">When an installed plugin was installed; null for a shipped one.</param>
/// <param name="PackageName">The file name of the package it was installed from; null for a shipped one.</param>
/// <param name="PackageSha256">The SHA-256 fingerprint of that package, in lower-case hexadecimal; null for a shipped one.</param>
/// <param name="LastError">The last request for this plugin that could not be applied; null when the last one was.</param>
/// <param name="Suspended">
/// Whether the plugin is suspected of having crashed the service: it is not loaded until the user decides.
/// </param>
/// <param name="DisabledForRecovery">
/// Whether the plugin was disabled by a start without the installed plugins, rather than by the user one by one.
/// </param>
public sealed record PluginRecord(
    PluginKey Plugin,
    bool Enabled,
    Instant? InstalledAt,
    string? PackageName,
    string? PackageSha256,
    PluginRequestError? LastError,
    bool Suspended = false,
    bool DisabledForRecovery = false);
