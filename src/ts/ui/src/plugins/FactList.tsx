// The standard presentation of the facts of a plugin: a labelled list. Plugins that bring their own panel replace it.
import { Body1, makeStyles, tokens } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';
import { Fragment } from 'react';

import { formatFactValue, type DiagnosticFact } from '@voyager/plugin-sdk';

import { useAppContext } from '../app/appContext';

const useStyles = makeStyles({
  facts: {
    display: 'grid',
    gridTemplateColumns: 'max-content 1fr',
    columnGap: tokens.spacingHorizontalXL,
    rowGap: tokens.spacingVerticalS,
    margin: 0,
  },
  label: {
    color: tokens.colorNeutralForeground3,
  },
  value: {
    margin: 0,
  },
});

/** The facts of a source as labelled values, written for the language of the user; a message when there is none. */
export function FactList({ facts }: { readonly facts: readonly DiagnosticFact[] }) {
  const styles = useStyles();
  const { locale } = useAppContext();
  const { l10n } = useLocalization();

  if (facts.length === 0) {
    return <Body1>{l10n.getString('plugins-facts-empty')}</Body1>;
  }

  return (
    <dl className={styles.facts}>
      {facts.map((fact, index) => (
        // A source may repeat a label: the position keeps the keys apart.
        <Fragment key={`${fact.labelId}-${String(index)}`}>
          <dt className={styles.label}>
            <Body1>{l10n.getString(fact.labelId)}</Body1>
          </dt>
          <dd className={styles.value}>
            <Body1>{formatFactValue(fact.value, locale)}</Body1>
          </dd>
        </Fragment>
      ))}
    </dl>
  );
}
