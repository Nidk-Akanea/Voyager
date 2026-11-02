namespace Voyager.Sdk;

/// <summary>
/// The contracts of this SDK, each version with its state (ADR-0017). A plugin declares in its manifest the
/// contracts it provides and those it requires, each with the version it was written for; the application
/// accepts it when every one of them is in a version listed here as supported or deprecated.
/// </summary>
/// <remarks>
/// A contract changes version when a change would break a plugin written for the previous one; within a version,
/// only compatible additions are allowed (a new type, a non-abstract member, a default interface method, an
/// optional value). The two halves of a contract (the .NET types here and the interface types of
/// <c>@voyager/plugin-sdk</c>) share the same version number.
/// </remarks>
public static class ContractVersions
{
    #region Fields

    /// <summary>
    /// The base contract of every plugin: <see cref="IPlugin"/> and <see cref="IPluginBuilder"/>. Every plugin
    /// requires it; no plugin provides it.
    /// </summary>
    public const string PLUGIN = "plugin";

    #endregion Fields

    #region Properties

    /// <summary>Every version of every contract that this application knows, with its state.</summary>
    public static IReadOnlyList<ContractVersion> All { get; } =
    [
        new(PLUGIN, 1, ContractVersionState.Supported),
        new(ExtensionPoints.DIAGNOSTIC_FACT_SOURCE, 1, ContractVersionState.Supported),
    ];

    /// <summary>
    /// The contracts that the application itself provides to every plugin. A plugin may only require these
    /// until plugins can depend on one another (ADR-0017, decision 8): any other required contract is not
    /// supported by this application.
    /// </summary>
    public static IReadOnlyList<string> ProvidedByApplication { get; } = [PLUGIN];

    #endregion Properties
}
