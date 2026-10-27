namespace Voyager.PluginHost;

/// <summary>
/// How a source of diagnostic facts answered.
/// </summary>
public enum DiagnosticFactOutcome
{
    /// <summary>The source answered; its facts are available.</summary>
    Succeeded,

    /// <summary>The source threw an exception; its facts are left out.</summary>
    Failed,

    /// <summary>The source did not answer in time; its facts are left out.</summary>
    TimedOut,

    /// <summary>The source answered with facts the host refuses; they are left out.</summary>
    Invalid,
}
