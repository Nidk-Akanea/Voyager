// Shared with the plugins (ADR-0015): this module is an entry of the build next to the page itself, and the import map
// of the page points the specifier "react-dom" to it. The page and the plugins therefore use ONE copy of it.
export { default } from 'react-dom';
export {
  createPortal,
  flushSync,
  preconnect,
  prefetchDNS,
  preinit,
  preinitModule,
  preload,
  preloadModule,
  requestFormReset,
  unstable_batchedUpdates,
  useFormState,
  useFormStatus,
  version,
} from 'react-dom';
