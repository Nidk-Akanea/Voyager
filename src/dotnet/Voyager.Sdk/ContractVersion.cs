namespace Voyager.Sdk;

/// <summary>
/// One version of a contract and whether the application accepts it.
/// </summary>
/// <param name="Contract">The identifier of the contract, for example <c>diagnostic-fact-source</c>.</param>
/// <param name="Version">The version: a whole number, 1 for the first one.</param>
/// <param name="State">Whether the application accepts this version.</param>
public sealed record ContractVersion(string Contract, int Version, ContractVersionState State);
