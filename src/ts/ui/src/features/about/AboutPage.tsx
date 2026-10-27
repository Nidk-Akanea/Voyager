// "About" screen: what the local service reports about itself. Display only, no computation.
import { Button, makeStyles, MessageBar, MessageBarBody, Spinner, Title2, tokens } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';

import { PluginsSection } from '../../plugins/PluginsSection';
import { ServiceFacts } from './ServiceFacts';
import { useServiceInfo } from './useServiceInfo';

const useStyles = makeStyles({
  page: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalL,
    padding: tokens.spacingHorizontalXXL,
    maxWidth: '640px',
  },
});

export function AboutPage() {
  const styles = useStyles();
  const { l10n } = useLocalization();
  const serviceInfo = useServiceInfo();

  return (
    <main className={styles.page}>
      <Title2 as="h1">{l10n.getString('about-title')}</Title2>
      {serviceInfo.isPending && <Spinner label={l10n.getString('about-loading')} />}
      {serviceInfo.isError && (
        <MessageBar intent="error">
          <MessageBarBody>{l10n.getString('about-error')}</MessageBarBody>
          <Button onClick={() => void serviceInfo.refetch()}>{l10n.getString('about-retry')}</Button>
        </MessageBar>
      )}
      {serviceInfo.isSuccess && <ServiceFacts info={serviceInfo.data} />}
      <PluginsSection />
    </main>
  );
}
