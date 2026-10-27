namespace Voyager.Sdk.Diagnostics;

/// <summary>
/// The kind of value a <see cref="DiagnosticFactValue"/> holds.
/// </summary>
public enum DiagnosticFactKind
{
    /// <summary>A text, shown as is.</summary>
    Text,

    /// <summary>A number, formatted for the user's language.</summary>
    Number,

    /// <summary>An instant, shown in the user's language and time zone.</summary>
    Instant,
}
