// Entry point of the user interface, loaded by the desktop app (src/ts/desktop).
import { createLocalServiceClients, type LocalServiceClients } from '@voyager/grpc-client';
import { createRoot } from 'react-dom/client';

import './app/bridge';
import { AppProviders } from './app/AppProviders';
import { AboutPage } from './features/about/AboutPage';
import { createLocalization, pickLocale } from './i18n/localization';

const ROOT_ELEMENT_ID = 'root';

async function start(): Promise<void> {
  const container = document.getElementById(ROOT_ELEMENT_ID);
  if (container === null) {
    throw new Error(`The page has no #${ROOT_ELEMENT_ID} element.`);
  }

  const root = createRoot(container);
  const locale = pickLocale(navigator.languages);
  const prefersDarkTheme = window.matchMedia('(prefers-color-scheme: dark)').matches;

  const localization = createLocalization(locale);
  // Screen readers pick their voice from the page language.
  document.documentElement.lang = locale;
  document.title = localization.getString('app-title');

  // Outside the desktop app (a plain browser), there is no local service to talk to.
  if (window.voyager === undefined) {
    root.render(<p>{localization.getString('app-not-in-desktop')}</p>);
    return;
  }

  // The desktop app may still be starting the local service.
  root.render(<p>{localization.getString('app-starting')}</p>);

  let clients: LocalServiceClients;
  try {
    clients = createLocalServiceClients(await window.voyager.getLocalService());
  } catch (error: unknown) {
    // Shown in the developer tools; the user sees a message telling them what to do.
    console.error('The local service is not available:', error instanceof Error ? error.message : error);
    root.render(<p>{localization.getString('app-local-service-failed')}</p>);
    return;
  }

  root.render(
    <AppProviders locale={locale} clients={clients} prefersDarkTheme={prefersDarkTheme}>
      <AboutPage />
    </AppProviders>,
  );
}

void start();
