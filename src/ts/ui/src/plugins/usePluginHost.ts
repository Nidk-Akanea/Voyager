// What the interface reads about plugins from the local service (proto/voyager/pluginhost/v1/pluginhost.proto), and
// the loading of their interface parts and translations.
import { timestampDate } from '@bufbuild/protobuf/wkt';
import { useQuery } from '@tanstack/react-query';

import type { DiagnosticFact, FactValue } from '@voyager/plugin-sdk';
import type { Fact, Plugin, SourceFacts } from '@voyager/grpc-client';

import { useAppContext } from '../app/appContext';
import type { Locale } from '../i18n/localization';
import { loadPluginUi } from './loadPluginUi';
import { loadPluginMessages } from './pluginMessages';

// What plugins say does not change while the page is open, and asking a plugin runs its code: nothing here is read
// again when the window gets the focus back. The refresh button of a panel asks again when the user wants.

/** The plugins found at startup, in use or set aside. */
export function usePluginList() {
  const { clients } = useAppContext();
  return useQuery({
    queryKey: ['plugins', 'list'],
    queryFn: ({ signal }) => clients.pluginHost.listPlugins({}, { signal }),
    refetchOnWindowFocus: false,
  });
}

/** The facts of one source. Asking again is `refetch`; the source answers in a few seconds at most. */
export function usePluginFacts(pluginId: string, enabled: boolean) {
  const { clients } = useAppContext();
  return useQuery({
    queryKey: ['plugins', 'facts', pluginId],
    queryFn: ({ signal }) => clients.pluginHost.getDiagnosticFacts({ sourceId: pluginId }, { signal }),
    enabled,
    refetchOnWindowFocus: false,
    // A plugin that fails is told so by its answer, not by a failed call: no need to ask again at once.
    retry: false,
  });
}

/** The interface part of a plugin, loaded once: a module is not imported again. */
export function usePluginUi(plugin: Plugin, enabled: boolean) {
  const { pluginRuntime } = useAppContext();
  return useQuery({
    queryKey: ['plugins', 'ui', plugin.id],
    queryFn: () => loadPluginUi(plugin, pluginRuntime),
    enabled,
    staleTime: Infinity,
    retry: false,
  });
}

/** The translations of a plugin for a language; empty when they could not be read. */
export function usePluginMessages(pluginId: string, locale: Locale, enabled: boolean) {
  const { pluginRuntime } = useAppContext();
  return useQuery({
    queryKey: ['plugins', 'messages', pluginId, locale],
    queryFn: () => loadPluginMessages(pluginId, locale, pluginRuntime),
    enabled,
    staleTime: Infinity,
    retry: false,
  });
}

/** The value of a fact of the service, in the shape the plugins' panels receive it in. */
function toFactValue(fact: Fact): FactValue {
  switch (fact.value.case) {
    case 'number':
      return { kind: 'number', number: fact.value.value };
    case 'instant':
      return { kind: 'instant', instant: timestampDate(fact.value.value) };
    case 'text':
      return { kind: 'text', text: fact.value.value };
    default:
      // A fact without a value, or of a kind this version of the page does not know, is shown as an empty text.
      return { kind: 'text', text: '' };
  }
}

/** The facts of a source in the shape the plugins' panels receive them in. */
export function toDiagnosticFacts(source: SourceFacts): DiagnosticFact[] {
  return source.facts.map((fact) => ({ labelId: fact.labelId, value: toFactValue(fact) }));
}
