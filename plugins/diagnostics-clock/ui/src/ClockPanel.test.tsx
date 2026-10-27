// @vitest-environment jsdom
import { FluentBundle, FluentResource } from '@fluent/bundle';
import { LocalizationProvider, ReactLocalization } from '@fluent/react';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { DiagnosticFactPanelProps, PluginRegistry } from '@voyager/plugin-sdk';

import english from '../../i18n/en/messages.ftl?raw';
import { ClockPanel } from './ClockPanel';
import { register } from './index';

function renderPanel(props: Partial<DiagnosticFactPanelProps> = {}) {
  const bundle = new FluentBundle('en');
  bundle.addResource(new FluentResource(english));
  const refresh = vi.fn();
  render(
    <LocalizationProvider l10n={new ReactLocalization([bundle])}>
      <ClockPanel
        pluginId="diagnostics-clock"
        locale="en"
        facts={[
          {
            labelId: 'diagnostics-clock-fact-now',
            value: { kind: 'instant', instant: new Date('2026-03-14T09:30:00Z') },
          },
          { labelId: 'diagnostics-clock-fact-uptime', value: { kind: 'number', number: 3725 } },
        ]}
        truncated={false}
        isRefreshing={false}
        refresh={refresh}
        {...props}
      />
    </LocalizationProvider>,
  );
  return refresh;
}

describe('ClockPanel', () => {
  it('shows the labels of the plugin and the values formatted for the user', () => {
    renderPanel();

    expect(screen.getByText('Time of the service')).toBeTruthy();
    expect(screen.getByText(/2026/)).toBeTruthy();
    expect(screen.getByText('Running for (seconds)')).toBeTruthy();
    expect(screen.getByText('3,725')).toBeTruthy();
  });

  it('asks for the facts again when the button is pressed', () => {
    const refresh = renderPanel();

    screen.getByRole('button', { name: 'Refresh' }).click();

    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it('disables the button while the facts are being read', () => {
    renderPanel({ isRefreshing: true });

    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Refresh' }).disabled).toBe(true);
  });
});

describe('register', () => {
  it('provides the panel, and nothing else', () => {
    const addDiagnosticFactPanel = vi.fn();

    register({ addDiagnosticFactPanel } satisfies PluginRegistry);

    expect(addDiagnosticFactPanel).toHaveBeenCalledExactlyOnceWith(ClockPanel);
  });
});
