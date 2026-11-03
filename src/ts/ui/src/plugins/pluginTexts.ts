// What the "About" page and the "Plugins" screen say alike about a plugin: why it was set aside, and the contracts it
// uses. The service sends values; the page only picks the messages of plugins.ftl that say them.
import { ContractUseState, PluginRejectionReason, type ContractUse, type Plugin } from '@voyager/grpc-client';

// Said of a reason or a state this version of the page does not know (a newer service).
const MESSAGE_REASON_UNKNOWN = 'plugins-reason-unknown';

const REJECTION_MESSAGES: Readonly<Partial<Record<PluginRejectionReason, string>>> = {
  [PluginRejectionReason.MANIFEST_INVALID]: 'plugins-reason-manifest-invalid',
  [PluginRejectionReason.DUPLICATE_ID]: 'plugins-reason-duplicate-id',
  [PluginRejectionReason.DUPLICATE_OF_SHIPPED]: 'plugins-reason-duplicate-of-shipped',
  [PluginRejectionReason.CONTRACT_NOT_SUPPORTED]: 'plugins-reason-contract-not-supported',
  [PluginRejectionReason.LOAD_FAILED]: 'plugins-reason-load-failed',
  [PluginRejectionReason.UNDECLARED_REGISTRATION]: 'plugins-reason-undeclared-registration',
};

// What the user can do about a contract the application does not accept: a plugin that is too old, or too recent.
const UNSUPPORTED_CONTRACT_MESSAGES: Readonly<Partial<Record<ContractUseState, string>>> = {
  [ContractUseState.OBSOLETE]: 'plugins-contract-obsolete',
  [ContractUseState.UNKNOWN]: 'plugins-contract-unknown',
};

/** The message that says why a plugin was set aside. */
export function rejectionMessageId(reason: PluginRejectionReason): string {
  return REJECTION_MESSAGES[reason] ?? MESSAGE_REASON_UNKNOWN;
}

/** The message that says what to do about a contract the application does not accept. */
export function unsupportedContractMessageId(state: ContractUseState): string {
  return UNSUPPORTED_CONTRACT_MESSAGES[state] ?? MESSAGE_REASON_UNKNOWN;
}

/** The contracts of a plugin that are in one of the given states, in the order the service sent them. */
export function contractsIn(
  plugin: Pick<Plugin, 'contracts'>,
  states: readonly ContractUseState[],
): readonly ContractUse[] {
  return plugin.contracts.filter((contract) => states.includes(contract.state));
}

/** The contracts a plugin cannot use, when that is why it was set aside. */
export function unsupportedContracts(plugin: Pick<Plugin, 'contracts' | 'rejectionReason'>): readonly ContractUse[] {
  return plugin.rejectionReason === PluginRejectionReason.CONTRACT_NOT_SUPPORTED
    ? contractsIn(plugin, [ContractUseState.OBSOLETE, ContractUseState.UNKNOWN])
    : [];
}

/** A React key for a contract: a manifest names a contract at most once in each role. */
export function contractKey(contract: ContractUse): string {
  return `${String(contract.role)}-${contract.contract}`;
}

/** The arguments of the messages that name a contract. */
export function contractArguments(contract: Pick<ContractUse, 'contract' | 'version'>) {
  // A string, not a number: a version is a name, never formatted with the separators of a language.
  return { contract: contract.contract, version: String(contract.version) };
}
