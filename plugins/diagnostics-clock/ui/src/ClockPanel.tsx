// The panel of the diagnostics-clock plugin: the time of the service, its uptime, and a button to read them again.
import { Button, Caption1, Title3, makeStyles, tokens } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';

import { formatFactValue, type DiagnosticFactPanelProps } from '@voyager/plugin-sdk';

const useStyles = makeStyles({
  panel: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    alignItems: 'flex-start',
  },
  fact: {
    display: 'flex',
    flexDirection: 'column',
  },
  label: {
    color: tokens.colorNeutralForeground3,
  },
});

/** Shows each fact as a label over a large value, and a button that asks the service for the facts again. */
export function ClockPanel({ facts, locale, isRefreshing, refresh }: DiagnosticFactPanelProps) {
  const styles = useStyles();
  const { l10n } = useLocalization();

  return (
    <div className={styles.panel}>
      {facts.map((fact, index) => (
        <div key={`${fact.labelId}-${String(index)}`} className={styles.fact}>
          <Caption1 className={styles.label}>{l10n.getString(fact.labelId)}</Caption1>
          <Title3>{formatFactValue(fact.value, locale)}</Title3>
        </div>
      ))}
      <Button appearance="primary" disabled={isRefreshing} onClick={refresh}>
        {l10n.getString('diagnostics-clock-refresh')}
      </Button>
    </div>
  );
}
