// Goes from one group server to another when the app knows several (spec of the switch to client-server, "Les serveurs
// enregistrés"). Each server has its own view in the window: choosing one asks the main process to show it. A drop-down
// list changes server only once an option is chosen (a click or Enter), never while the arrows go through the list.
// Drawn only when there is a choice to make; the other servers are named by their identifier.
import { Dropdown, Field, makeStyles, Option, tokens } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';

import type { RegisteredServer, ServerChoice } from '@voyager/grpc-client';

import type { ServerBridge } from './bridge';

const useStyles = makeStyles({
  selector: {
    paddingInline: tokens.spacingHorizontalXL,
    paddingTop: tokens.spacingVerticalS,
    maxWidth: '480px',
  },
});

export interface ServerSelectorProps {
  readonly servers: readonly ServerChoice[];
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
      <Dropdown
        value={current.id}
        selectedOptions={[current.id]}
        onOptionSelect={(_event, data) => {
          if (data.optionValue === undefined || data.optionValue === current.id) {
            return;
          }
          bridge.selectServer(data.optionValue).catch((error: unknown) => {
            console.error('The server could not be shown:', error instanceof Error ? error.message : error);
          });
        }}
      >
        {servers.map((server) => (
          <Option key={server.id} value={server.id}>
            {server.id}
          </Option>
        ))}
      </Dropdown>
    </Field>
  );
}
