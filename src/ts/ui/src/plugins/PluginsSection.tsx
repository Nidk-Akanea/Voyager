// The plugins section of the "About" page: which plugins are in use or set aside, and what each one provides.
import {
  Body1,
  Button,
  MessageBar,
  MessageBarBody,
  Spinner,
  Title3,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';

import { PluginCard } from './PluginCard';
import { usePluginList } from './usePluginHost';

const useStyles = makeStyles({
  section: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
  },
});

/** The plugins found at startup, one card each, with the loading and error states of the list itself. */
export function PluginsSection() {
  const styles = useStyles();
  const { l10n } = useLocalization();
  const list = usePluginList();
  const plugins = list.data?.plugins ?? [];

  return (
    <section className={styles.section}>
      <Title3 as="h2">{l10n.getString('plugins-title')}</Title3>
      {list.isPending && <Spinner label={l10n.getString('plugins-loading')} />}
      {list.isError && (
        <MessageBar intent="error">
          <MessageBarBody>{l10n.getString('plugins-error')}</MessageBarBody>
          <Button onClick={() => void list.refetch()}>{l10n.getString('about-retry')}</Button>
        </MessageBar>
      )}
      {list.isSuccess && plugins.length === 0 && <Body1>{l10n.getString('plugins-none')}</Body1>}
      {plugins.map((plugin) => (
        <PluginCard key={plugin.folder} plugin={plugin} />
      ))}
    </section>
  );
}
