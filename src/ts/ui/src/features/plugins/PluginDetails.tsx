// The detail of a plugin on the "Plugins" screen: what it brings, its contracts and their state, and, for an installed
// plugin, when it was installed and from which package. The texts from a package were cleaned by the service.
import { timestampDate } from '@bufbuild/protobuf/wkt';
import { Caption1, makeStyles, tokens } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';

import { ContractRole, ContractUseState, PluginOrigin, type Plugin } from '@voyager/grpc-client';

import { useAppContext } from '../../app/appContext';
import { contractArguments, contractKey } from '../../plugins/pluginTexts';

const useStyles = makeStyles({
  details: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXXS,
    color: tokens.colorNeutralForeground2,
  },
  fingerprint: {
    wordBreak: 'break-all',
  },
});

const CONTRACT_STATES: Readonly<Partial<Record<ContractUseState, string>>> = {
  [ContractUseState.SUPPORTED]: 'supported',
  [ContractUseState.DEPRECATED]: 'deprecated',
  [ContractUseState.OBSOLETE]: 'obsolete',
};

function parts(plugin: Pick<Plugin, 'hasDotnet' | 'hasUi'>): string {
  if (plugin.hasDotnet && plugin.hasUi) {
    return 'dotnet-and-ui';
  }
  if (plugin.hasDotnet) {
    return 'dotnet';
  }
  return plugin.hasUi ? 'ui' : 'unknown';
}

/** What a plugin brings, its contracts, and the package an installed plugin came from. */
export function PluginDetails({ plugin }: { readonly plugin: Plugin }) {
  const styles = useStyles();
  const { l10n } = useLocalization();
  const { locale } = useAppContext();
  const installed = plugin.origin === PluginOrigin.INSTALLED;
  const installedAt = plugin.installedAt === undefined ? undefined : timestampDate(plugin.installedAt);

  return (
    <div className={styles.details}>
      <Caption1>{l10n.getString('plugin-management-brings', { parts: parts(plugin) })}</Caption1>
      {plugin.contracts.map((contract) => (
        <Caption1 key={contractKey(contract)}>
          {l10n.getString('plugin-management-contract', {
            ...contractArguments(contract),
            role: contract.role === ContractRole.PROVIDED ? 'provides' : 'requires',
            // A state this version of the page does not know reads as "unknown to this application".
            state: CONTRACT_STATES[contract.state] ?? 'unknown',
          })}
        </Caption1>
      ))}
      {installed && installedAt !== undefined && (
        <Caption1>
          {l10n.getString('plugin-management-installed-at', {
            date: new Intl.DateTimeFormat(locale, { dateStyle: 'long', timeStyle: 'short' }).format(installedAt),
          })}
        </Caption1>
      )}
      {installed && plugin.packageName !== '' && (
        <Caption1>{l10n.getString('plugin-management-package', { name: plugin.packageName })}</Caption1>
      )}
      {installed && plugin.packageSha256 !== '' && (
        <Caption1 className={styles.fingerprint}>
          {l10n.getString('plugin-management-package-sha256', { sha256: plugin.packageSha256 })}
        </Caption1>
      )}
    </div>
  );
}
