// What the server reports about itself, as a list of labelled values, with its address. Formats, never computes.
import { Body1, Card, makeStyles, tokens } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';
import { timestampDate } from '@bufbuild/protobuf/wkt';
import { Fragment, type ReactNode } from 'react';

import type { GetServiceInfoResponse } from '@voyager/grpc-client';

import { useAppContext } from '../../app/appContext';
import { DatabaseBadge } from './DatabaseBadge';

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

export function ServiceFacts({ info }: { readonly info: GetServiceInfoResponse }) {
  const styles = useStyles();
  const { locale, server, appVersion } = useAppContext();
  const { l10n } = useLocalization();
  const startedAt = info.startedAt
    ? new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'medium' }).format(
        timestampDate(info.startedAt),
      )
    : '—';

  const facts: readonly (readonly [string, ReactNode])[] = [
    ...(server === undefined ? [] : [['about-server', server.address] as const]),
    ['about-version', info.version],
    ...(appVersion === undefined ? [] : [['about-app-version', appVersion] as const]),
    ['about-runtime', info.runtime],
    ['about-environment', info.environment],
    ['about-started-at', startedAt],
    ['about-database', <DatabaseBadge status={info.databaseStatus} />],
  ];

  return (
    <Card>
      <dl className={styles.facts}>
        {facts.map(([messageId, value]) => (
          <Fragment key={messageId}>
            <dt className={styles.label}>
              <Body1>{l10n.getString(messageId)}</Body1>
            </dt>
            <dd className={styles.value}>
              <Body1>{value}</Body1>
            </dd>
          </Fragment>
        ))}
      </dl>
    </Card>
  );
}
