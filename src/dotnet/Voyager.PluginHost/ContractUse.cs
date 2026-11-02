namespace Voyager.PluginHost;

/// <summary>
/// A contract that a plugin provides or requires, and whether this application accepts its version.
/// </summary>
/// <param name="Contract">The identifier of the contract.</param>
/// <param name="Version">The version the plugin was written for.</param>
/// <param name="Role">Whether the plugin provides or requires the contract.</param>
/// <param name="State">Whether this application accepts that version.</param>
public sealed record ContractUse(string Contract, int Version, ContractRole Role, ContractUseState State);
