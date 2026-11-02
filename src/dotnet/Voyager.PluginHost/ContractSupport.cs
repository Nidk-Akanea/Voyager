using Voyager.Sdk;

namespace Voyager.PluginHost;

/// <summary>
/// The versions of contracts that this application accepts (ADR-0017): the rule that replaces the version of
/// the SDK. A plugin is accepted when every contract it provides or requires is in a supported or deprecated
/// version; one obsolete or unknown version is enough to set it aside. Until plugins can depend on one another,
/// a plugin may only require the contracts that the application itself provides: any other one is unknown here.
/// </summary>
public sealed class ContractSupport
{
    #region Fields

    private readonly HashSet<string> _providedByApplication;
    private readonly Dictionary<(string Contract, int Version), ContractVersionState> _states;

    #endregion Fields

    #region Public Constructors

    /// <summary>
    /// Creates the rule from a list of contract versions.
    /// </summary>
    /// <param name="versions">Every known version of every contract, with its state; each one listed once.</param>
    /// <param name="providedByApplication">The contracts a plugin may require: those the application itself provides.</param>
    public ContractSupport(IEnumerable<ContractVersion> versions, IEnumerable<string> providedByApplication)
    {
        _states = versions.ToDictionary(version => (version.Contract, version.Version), version => version.State);
        _providedByApplication = new HashSet<string>(providedByApplication, StringComparer.Ordinal);
    }

    #endregion Public Constructors

    #region Properties

    /// <summary>
    /// The rule of this application: the contract versions declared by the SDK it runs with
    /// (<see cref="ContractVersions"/>).
    /// </summary>
    public static ContractSupport Application { get; } = new(ContractVersions.All, ContractVersions.ProvidedByApplication);

    #endregion Properties

    #region Private Methods

    // A required contract that the application does not provide has nobody to provide it yet: the plugin needs a
    // later application, one where plugins can depend on one another.
    private ContractUse Use(ContractReference reference, ContractRole role) =>
        new(
            reference.Contract,
            reference.Version,
            role,
            role == ContractRole.Required && !_providedByApplication.Contains(reference.Contract)
                ? ContractUseState.Unknown
                : StateOf(reference.Contract, reference.Version));

    #endregion Private Methods

    #region Public Methods

    /// <summary>
    /// Whether a plugin that uses a contract in this state can be loaded.
    /// </summary>
    /// <param name="state">The state of the version the plugin uses.</param>
    public static bool IsAccepted(ContractUseState state) =>
        state is ContractUseState.Supported or ContractUseState.Deprecated;

    /// <summary>
    /// Every contract a plugin provides or requires, with its state in this application.
    /// </summary>
    /// <param name="manifest">The validated manifest of the plugin.</param>
    /// <returns>The provided contracts first, then the required ones, each in the order of the manifest.</returns>
    public IReadOnlyList<ContractUse> Describe(PluginManifest manifest) =>
    [
        .. manifest.Provides.Select(reference => Use(reference, ContractRole.Provided)),
        .. manifest.Requires.Select(reference => Use(reference, ContractRole.Required)),
    ];

    /// <summary>
    /// The state of one version of a contract in this application.
    /// </summary>
    /// <param name="contract">The identifier of the contract.</param>
    /// <param name="version">The version a plugin was written for.</param>
    /// <returns><see cref="ContractUseState.Unknown"/> when the application does not list this version.</returns>
    public ContractUseState StateOf(string contract, int version) =>
        _states.TryGetValue((contract, version), out var state)
            ? state switch
            {
                ContractVersionState.Supported => ContractUseState.Supported,
                ContractVersionState.Deprecated => ContractUseState.Deprecated,
                ContractVersionState.Obsolete => ContractUseState.Obsolete,
                _ => ContractUseState.Unknown,
            }
            : ContractUseState.Unknown;

    #endregion Public Methods
}
