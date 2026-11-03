// The screens of the main window once the local service is ready, and the tabs that go from one to the other. A plain
// state for now, not a router (ADR-0006 names TanStack Router): the navigation comes with the design. Both screens stay
// mounted, so that an operation going on in one is not lost when the user looks at the other, and the chosen tab
// survives the reload that follows a restart of the service.
import { Tab, TabList, makeStyles, tokens } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';
import { useState } from 'react';

import type { PluginManagementBridge } from '@voyager/grpc-client';

import { AboutPage } from '../features/about/AboutPage';
import { PluginsPage } from '../features/plugins/PluginsPage';

const useStyles = makeStyles({
  tabs: {
    paddingInline: tokens.spacingHorizontalXL,
    paddingTop: tokens.spacingVerticalS,
  },
});

type Screen = 'about' | 'plugins';

// Kept for the session of the window only: a reload keeps it, closing Voyager forgets it.
const SCREEN_STORAGE_KEY = 'voyager.screen';

/** The screen a tab or the storage names; anything else, a value from an older page included, opens "About". */
function toScreen(value: unknown): Screen {
  return value === 'plugins' ? 'plugins' : 'about';
}

function savedScreen(): Screen {
  try {
    return toScreen(sessionStorage.getItem(SCREEN_STORAGE_KEY));
  } catch {
    // Storage can be refused; the app then opens on "About".
    return 'about';
  }
}

function saveScreen(screen: Screen): void {
  try {
    sessionStorage.setItem(SCREEN_STORAGE_KEY, screen);
  } catch {
    // Nothing to keep: the next reload opens on "About".
  }
}

export interface AppShellProps {
  /** What the desktop app lets the "Plugins" screen do. */
  readonly pluginManagement: PluginManagementBridge;
}

/** The tabs of the main window, and the screen of the chosen one; "About" first. */
export function AppShell({ pluginManagement }: AppShellProps) {
  const styles = useStyles();
  const { l10n } = useLocalization();
  const [screen, setScreen] = useState<Screen>(savedScreen);

  return (
    <>
      <TabList
        className={styles.tabs}
        selectedValue={screen}
        onTabSelect={(_event, data) => {
          const chosen = toScreen(data.value);
          saveScreen(chosen);
          setScreen(chosen);
        }}
      >
        <Tab value="about">{l10n.getString('app-nav-about')}</Tab>
        <Tab value="plugins">{l10n.getString('app-nav-plugins')}</Tab>
      </TabList>
      <div hidden={screen !== 'about'}>
        <AboutPage />
      </div>
      <div hidden={screen !== 'plugins'}>
        <PluginsPage bridge={pluginManagement} />
      </div>
    </>
  );
}
