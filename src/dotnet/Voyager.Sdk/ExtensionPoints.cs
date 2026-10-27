namespace Voyager.Sdk;

/// <summary>
/// The identifiers of the extension points a plugin can provide, as written in the <c>provides</c>
/// section of its manifest.
/// </summary>
public static class ExtensionPoints
{
    /// <summary>
    /// A source of diagnostic facts about the service, shown on the "About" page. Technical: it proves the
    /// plugin mechanism before any business extension point exists.
    /// </summary>
    public const string DIAGNOSTIC_FACT_SOURCE = "diagnostic-fact-source";
}
