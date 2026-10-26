// What every page of the app is drawn in, whether or not the local service is ready: Fluent UI theme and translations.
import { FluentProvider, webDarkTheme, webLightTheme } from '@fluentui/react-components';
import { LocalizationProvider } from '@fluent/react';
import { useMemo, type ReactNode } from 'react';

import { createLocalization, type Locale } from '../i18n/localization';

export interface ShellProvidersProps {
  readonly locale: Locale;
  /** Whether the system asks for a dark theme; light by default. */
  readonly prefersDarkTheme?: boolean;
  readonly children: ReactNode;
}

/** Gives its children the Fluent UI theme and the translations of the chosen locale. */
export function ShellProviders({ locale, prefersDarkTheme = false, children }: ShellProvidersProps) {
  const localization = useMemo(() => createLocalization(locale), [locale]);

  return (
    <FluentProvider theme={prefersDarkTheme ? webDarkTheme : webLightTheme}>
      <LocalizationProvider l10n={localization}>{children}</LocalizationProvider>
    </FluentProvider>
  );
}
