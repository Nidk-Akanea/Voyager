namespace Voyager.PluginHost;

/// <summary>
/// What a plugin does with a contract.
/// </summary>
public enum ContractRole
{
    /// <summary>The plugin provides an implementation of the contract.</summary>
    Provided,

    /// <summary>The plugin needs the contract to work, for example the base contract of every plugin.</summary>
    Required,
}
