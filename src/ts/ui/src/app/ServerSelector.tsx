// Goes from one group server to another when the app knows several (spec of the switch to client-server, "Les serveurs
// enregistrés"). Each server has its own view in the window: choosing one asks the main process to show it. Drawn only
// when there is a choice to make.
import { Field, makeStyles, Select, tokens } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';

import type { RegisteredServer } from '@voyager/grpc-client';

import type { ServerBridge } from './bridge';

const useStyles = makeStyles({
  selector: {
    paddingInline: tokens.spacingHorizontalXL,
    paddingTop: tokens.spacingVerticalS,
    maxWidth: '480px',
  },
});

export interface ServerSelectorProps {
  readonly servers: readonly RegisteredServer[];
  /** The server of this view. */
  readonly current: RegisteredServer;
  readonly bridge: Pick<ServerBridge, 'selectServer'>;
}

export function ServerSelector({ servers, current, bridge }: ServerSelectorProps) {
  const styles = useStyles();
  const { l10n } = useLocalization();

  if (servers.length < 2) {
    return null;
  }

  return (
    <Field className={styles.selector} label={l10n.getString('app-server-selector')}>
      <Select
        value={current.id}
        onChange={(_event, data) => {
          bridge.selectServer(data.value).catch((error: unknown) => {
            console.error('The server could not be shown:', error instanceof Error ? error.message : error);
          });
        }}
      >
        {servers.map((server) => (
          <option key={server.id} value={server.id}>
            {server.address}
          </option>
        ))}
      </Select>
    </Field>
  );
}
