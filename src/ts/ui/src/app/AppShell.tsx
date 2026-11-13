// The screens of the main window once a server answers, and the tabs that go from one to the other. A plain state for
// now, not a router (ADR-0006 names TanStack Router): the navigation comes with the design. The plugins of the server
// show on "About"; managing them comes with the server (ADR-0020).
import { Tab, TabList, makeStyles, tokens } from '@fluentui/react-components';
import { useLocalization } from '@fluent/react';

import { AboutPage } from '../features/about/AboutPage';

const useStyles = makeStyles({
  tabs: {
    paddingInline: tokens.spacingHorizontalXL,
    paddingTop: tokens.spacingVerticalS,
  },
});

/** The tabs of the main window, and the screen of the chosen one: "About" only for now. */
export function AppShell() {
  const styles = useStyles();
  const { l10n } = useLocalization();

  return (
    <>
      <TabList className={styles.tabs} selectedValue="about">
        <Tab value="about">{l10n.getString('app-nav-about')}</Tab>
      </TabList>
      <AboutPage />
    </>
  );
}
