namespace Voyager.Sdk;

/// <summary>
/// Whether the application still accepts a version of a contract (ADR-0017, decision 5). A version that is
/// not listed at all is unknown to the application, which is the case of a plugin written for a newer one.
/// </summary>
public enum ContractVersionState
{
    /// <summary>The application accepts this version.</summary>
    Supported,

    /// <summary>The application still accepts this version, and warns that a later release will remove it.</summary>
    Deprecated,

    /// <summary>The application no longer accepts this version: a plugin that uses it is set aside.</summary>
    Obsolete,
}
