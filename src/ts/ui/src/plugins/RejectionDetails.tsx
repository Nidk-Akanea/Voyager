// Why a plugin was set aside, as the "About" page says it: the reason, then each contract the application does not
// accept when that is the reason.
import { Body1, Caption1 } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';

import type { Plugin } from '@voyager/grpc-client';

import {
  contractArguments,
  contractKey,
  rejectionMessageId,
  unsupportedContractMessageId,
  unsupportedContracts,
} from './pluginTexts';

/** The reason a plugin was set aside, and what to do about each contract it cannot use. */
export function RejectionDetails({ plugin }: { readonly plugin: Pick<Plugin, 'contracts' | 'rejectionReason'> }) {
  const { l10n } = useLocalization();
  return (
    <>
      <Body1>{l10n.getString(rejectionMessageId(plugin.rejectionReason))}</Body1>
      {unsupportedContracts(plugin).map((contract) => (
        <Caption1 key={contractKey(contract)}>
          {l10n.getString(unsupportedContractMessageId(contract.state), contractArguments(contract))}
        </Caption1>
      ))}
    </>
  );
}
