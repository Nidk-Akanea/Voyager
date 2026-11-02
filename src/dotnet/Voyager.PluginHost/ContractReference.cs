namespace Voyager.PluginHost;

/// <summary>
/// A contract named in a manifest, with the version the plugin was written for.
/// </summary>
/// <param name="Contract">The identifier of the contract, for example <c>diagnostic-fact-source</c>.</param>
/// <param name="Version">The version of the contract: a whole number, 1 for the first one.</param>
public sealed record ContractReference(string Contract, int Version);
