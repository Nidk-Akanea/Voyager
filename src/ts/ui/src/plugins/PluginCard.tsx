// One plugin of the "About" page: its state, and what it provides (its own panel, or the standard list of facts).
import {
  Badge,
  type BadgeProps,
  Body1,
  Caption1,
  Card,
  CardHeader,
  Spinner,
  Subtitle2,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { LocalizationProvider, useLocalization } from '@fluent/react';
import { useMemo, useState } from 'react';

import { FactsOutcome, PluginRejectionReason, PluginStatus, type Plugin } from '@voyager/grpc-client';

import { useAppContext } from '../app/appContext';
import { createLocalization } from '../i18n/localization';
import { FactList } from './FactList';
import { PluginErrorBoundary } from './PluginErrorBoundary';
import { toDiagnosticFacts, usePluginFacts, usePluginMessages, usePluginUi } from './usePluginHost';

const useStyles = makeStyles({
  body: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalS,
  },
  version: {
    color: tokens.colorNeutralForeground3,
  },
});

type PluginState = 'rejected' | 'loading' | 'error' | 'loaded';

const STATE_BADGES: Readonly<Record<PluginState, { readonly color: BadgeProps['color']; readonly messageId: string }>> =
  {
    rejected: { color: 'warning', messageId: 'plugins-state-rejected' },
    loading: { color: 'informative', messageId: 'plugins-state-loading' },
    error: { color: 'danger', messageId: 'plugins-state-error' },
    loaded: { color: 'success', messageId: 'plugins-state-loaded' },
  };

const MESSAGE_FACTS_FAILED = 'plugins-error-failed';
const MESSAGE_INTERFACE_FAILED = 'plugins-error-ui';
const MESSAGE_PANEL_FAILED = 'plugins-error-panel';
const MESSAGE_REASON_UNKNOWN = 'plugins-reason-unknown';

const REJECTION_MESSAGES: Readonly<Partial<Record<PluginRejectionReason, string>>> = {
  [PluginRejectionReason.MANIFEST_INVALID]: 'plugins-reason-manifest-invalid',
  [PluginRejectionReason.DUPLICATE_ID]: 'plugins-reason-duplicate-id',
  [PluginRejectionReason.SDK_INCOMPATIBLE]: 'plugins-reason-sdk-incompatible',
  [PluginRejectionReason.LOAD_FAILED]: 'plugins-reason-load-failed',
  [PluginRejectionReason.UNDECLARED_REGISTRATION]: 'plugins-reason-undeclared-registration',
};

const OUTCOME_MESSAGES: Readonly<Partial<Record<FactsOutcome, string>>> = {
  [FactsOutcome.FAILED]: MESSAGE_FACTS_FAILED,
  [FactsOutcome.TIMED_OUT]: 'plugins-error-timed-out',
  [FactsOutcome.INVALID]: 'plugins-error-invalid',
};

function findState(rejected: boolean, isLoading: boolean, problem: string | undefined): PluginState {
  if (rejected) {
    return 'rejected';
  }
  if (isLoading) {
    return 'loading';
  }
  return problem === undefined ? 'loaded' : 'error';
}

/** What is wrong with a plugin that is in use, as the id of the message that says it; undefined when nothing is. */
function findProblem(
  factsFailed: boolean,
  outcome: FactsOutcome | undefined,
  interfaceFailed: boolean,
  panelFailed: boolean,
): string | undefined {
  if (factsFailed) {
    return MESSAGE_FACTS_FAILED;
  }
  // An outcome this version of the page does not know (a newer service) is a failure, never a success.
  if (outcome !== undefined && outcome !== FactsOutcome.SUCCEEDED) {
    return OUTCOME_MESSAGES[outcome] ?? MESSAGE_FACTS_FAILED;
  }
  if (interfaceFailed) {
    return MESSAGE_INTERFACE_FAILED;
  }
  return panelFailed ? MESSAGE_PANEL_FAILED : undefined;
}

/**
 * One plugin of the "About" page: its identifier, version and state, then the reason it was set aside, a spinner
 * while it loads, or its facts (in its own panel when it has one, in the standard list otherwise).
 */
export function PluginCard({ plugin }: { readonly plugin: Plugin }) {
  const styles = useStyles();
  const { l10n } = useLocalization();
  const { locale } = useAppContext();
  const [panelFailed, setPanelFailed] = useState(false);
  const rejected = plugin.status !== PluginStatus.LOADED;
  const facts = usePluginFacts(plugin.id, !rejected);
  const ui = usePluginUi(plugin, !rejected);
  const messages = usePluginMessages(plugin.id, locale, !rejected);
  // The messages of a plugin are for that plugin alone: no plugin can see, hide or borrow those of another one.
  const localization = useMemo(
    () => createLocalization(locale, messages.data === undefined ? [] : [messages.data]),
    [locale, messages.data],
  );

  const source = facts.data?.sources[0];
  const problem = rejected
    ? undefined
    : findProblem(facts.isError, source?.outcome, ui.data?.status === 'failed', panelFailed);
  const isLoading = !rejected && (facts.isPending || ui.isPending || messages.isPending);
  const truncated = source?.truncated === true;
  const badge = STATE_BADGES[findState(rejected, isLoading, problem)];

  const header = (
    <CardHeader
      header={<Subtitle2>{plugin.id}</Subtitle2>}
      description={
        plugin.version === '' ? undefined : (
          <Caption1 className={styles.version}>
            {l10n.getString('plugins-version', { version: plugin.version })}
          </Caption1>
        )
      }
      action={<Badge color={badge.color}>{l10n.getString(badge.messageId)}</Badge>}
    />
  );

  if (rejected) {
    return (
      <Card>
        {header}
        <Body1>{l10n.getString(REJECTION_MESSAGES[plugin.rejectionReason] ?? MESSAGE_REASON_UNKNOWN)}</Body1>
      </Card>
    );
  }

  if (isLoading) {
    return (
      <Card>
        {header}
        <Spinner size="tiny" />
      </Card>
    );
  }

  if (problem !== undefined && !panelFailed) {
    return (
      <Card>
        {header}
        <Body1>{l10n.getString(problem)}</Body1>
      </Card>
    );
  }

  const diagnosticFacts = source === undefined ? [] : toDiagnosticFacts(source);
  const Panel = ui.data?.status === 'ready' ? ui.data.panel : undefined;
  const standardList = (
    <div className={styles.body}>
      <FactList facts={diagnosticFacts} />
      {truncated && <Caption1>{l10n.getString('plugins-facts-truncated')}</Caption1>}
    </div>
  );

  return (
    <Card>
      {header}
      {problem !== undefined && <Body1>{l10n.getString(problem)}</Body1>}
      <LocalizationProvider l10n={localization}>
        {Panel === undefined ? (
          standardList
        ) : (
          <PluginErrorBoundary
            fallback={standardList}
            onError={() => {
              setPanelFailed(true);
            }}
          >
            <Panel
              pluginId={plugin.id}
              locale={locale}
              facts={diagnosticFacts}
              truncated={truncated}
              isRefreshing={facts.isFetching}
              refresh={() => void facts.refetch()}
            />
          </PluginErrorBoundary>
        )}
      </LocalizationProvider>
    </Card>
  );
}
