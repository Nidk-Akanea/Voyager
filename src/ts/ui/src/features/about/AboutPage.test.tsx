// @vitest-environment jsdom
import { create } from '@bufbuild/protobuf';
import { timestampFromDate } from '@bufbuild/protobuf/wkt';
import { QueryClient } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { userEvent } from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { DatabaseStatus, GetServiceInfoResponseSchema, type LocalServiceClients } from '@voyager/grpc-client';

import { AppProviders } from '../../app/AppProviders';
import type { Locale } from '../../i18n/localization';
import { AboutPage } from './AboutPage';

const SERVICE_INFO = create(GetServiceInfoResponseSchema, {
  version: '0.2.0-alpha.3',
  runtime: '.NET 10.0.1',
  environment: 'Development',
  startedAt: timestampFromDate(new Date('2026-10-25T08:30:00Z')),
  databaseStatus: DatabaseStatus.AVAILABLE,
});

function renderAboutPage(getServiceInfo: () => Promise<unknown>, locale: Locale = 'fr') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const clients = { diagnostics: { getServiceInfo: vi.fn(getServiceInfo) } } as unknown as LocalServiceClients;
  render(
    <AppProviders locale={locale} clients={clients} queryClient={queryClient}>
      <AboutPage />
    </AppProviders>,
  );
  return clients;
}

describe('AboutPage', () => {
  it('shows what the local service reports', async () => {
    renderAboutPage(() => Promise.resolve(SERVICE_INFO));

    expect(await screen.findByText('0.2.0-alpha.3')).toBeTruthy();
    expect(screen.getByText('.NET 10.0.1')).toBeTruthy();
    expect(screen.getByText('Development')).toBeTruthy();
    expect(screen.getByText('Disponible')).toBeTruthy();
  });

  it('speaks English when English is chosen', async () => {
    renderAboutPage(() => Promise.resolve(SERVICE_INFO), 'en');

    expect(await screen.findByText('About Voyager')).toBeTruthy();
    expect(await screen.findByText('Available')).toBeTruthy();
  });

  it('shows the database as unavailable', async () => {
    renderAboutPage(() => Promise.resolve({ ...SERVICE_INFO, databaseStatus: DatabaseStatus.UNAVAILABLE }));

    expect(await screen.findByText('Indisponible')).toBeTruthy();
  });

  it('offers to try again when the local service does not answer', async () => {
    const clients = renderAboutPage(() => Promise.reject(new Error('connection refused')));

    const retry = await screen.findByRole('button', { name: 'Réessayer' });
    expect(screen.getByText('Le service local ne répond pas.')).toBeTruthy();

    await userEvent.click(retry);
    expect(vi.mocked(clients.diagnostics.getServiceInfo).mock.calls.length).toBeGreaterThan(1);
  });
});
