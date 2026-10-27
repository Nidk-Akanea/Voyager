// Shared with the plugins (ADR-0015): this module is an entry of the build next to the page itself, and the import map
// of the page points the specifier "@fluentui/react-components" to it. The page and the plugins therefore use ONE copy of it.
export * from '@fluentui/react-components';
