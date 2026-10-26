// Entry point of the user interface, loaded by the desktop app (src/ts/desktop).
import { createRoot } from 'react-dom/client';

import './app/bridge';
import { LocalServiceGate } from './app/LocalServiceGate';
import { ShellProviders } from './app/ShellProviders';
import { AboutPage } from './features/about/AboutPage';
import { createLocalization, pickLocale } from './i18n/localization';

const ROOT_ELEMENT_ID = 'root';

function start(): void {
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

  root.render(
    <ShellProviders locale={locale} prefersDarkTheme={prefersDarkTheme}>
      <LocalServiceGate bridge={window.voyager} locale={locale}>
        <AboutPage />
      </LocalServiceGate>
    </ShellProviders>,
  );
}

start();
