// What the desktop app and the user interface exchange to manage plugins (ADR-0016): the page names a plugin or a
// package of the inbox, never a path; the main process asks the user, writes the requests and restarts the service.

/** Where a plugin comes from, as the requests name it. */
export type PluginOriginName = 'shipped' | 'installed';

/** A plugin a request concerns: its origin and its identifier, which is also the name of its folder. */
export interface PluginTarget {
  readonly origin: PluginOriginName;
  readonly pluginId: string;
}

/** What a request asks for a plugin. */
export type PluginRequestActionName = 'install' | 'uninstall' | 'enable' | 'disable';

/** A request confirmed by the user, waiting for the next start of the local service. */
export interface PendingPluginRequest extends PluginTarget {
  readonly action: PluginRequestActionName;
  /** The cleaned name of the package file, for an installation. */
  readonly packageName?: string;
}

/** A package waiting in the inbox. */
export interface InboxPackage {
  /** The name of the file in the inbox, which the page gives back to install it. */
  readonly name: string;
  /** The same name, cleaned to be shown. */
  readonly displayName: string;
  /** Size of the file, in bytes. */
  readonly size: number;
  /** When the file was last changed, in milliseconds since 1970 (UTC). */
  readonly modifiedAt: number;
}

/** Why a package could not be added to the inbox. */
export type AddPackageRefusal = 'not-a-package' | 'empty' | 'not-enough-space';

/** What became of a package the user chose to add. */
export type AddPackageResult =
  /** `name` designates the package in the inbox and is never shown; `displayName` is the same name, cleaned. */
  | { readonly outcome: 'added'; readonly name: string; readonly displayName: string }
  | { readonly outcome: 'cancelled' }
  | { readonly outcome: 'refused'; readonly reason: AddPackageRefusal }
  /** The file could not be copied: the details are in the app's log. */
  | { readonly outcome: 'failed' };

/** Why the examination refused a package; the names of the values of PackageRefusal, and a copy that changed. */
export type InstallRefusal =
  | 'unreadable'
  | 'manifest-invalid'
  | 'declared-file-missing'
  | 'contract-not-supported'
  | 'forbidden-entry'
  | 'not-enough-space'
  | 'package-changed'
  /** A refusal this version of the app does not know: a newer service gave it. */
  | 'unknown';

/** What became of a request of the user. */
export type PluginActionResult =
  /** The request is written: it applies at the next restart. */
  | { readonly outcome: 'requested' }
  /** The request cancels the one waiting, or asks for what is already so: nothing waits any more. */
  | { readonly outcome: 'nothing-to-apply' }
  /** The user declined the confirmation: nothing is written. */
  | { readonly outcome: 'cancelled' }
  /** The package is not in the inbox any more, or the plugin is not in the list of the service. */
  | { readonly outcome: 'not-found' }
  /** The request does not apply to this plugin, for example uninstalling a shipped plugin. */
  | { readonly outcome: 'not-allowed' }
  /** The examination refused the package. */
  | { readonly outcome: 'refused'; readonly reason: InstallRefusal }
  /** The local service is starting, and may be reading the requests: none is changed until it is ready. */
  | { readonly outcome: 'service-not-ready' }
  /** The local service could not be asked, or a file could not be written: the details are in the app's log. */
  | { readonly outcome: 'failed' };

/** What became of a request to restart the local service. */
export type RestartResult =
  /** The service restarted: the page reloads. */
  | { readonly outcome: 'restarting' }
  /** No request waits: there is nothing to apply, and the service keeps running. */
  | { readonly outcome: 'nothing-to-apply' }
  /** The service is not the app's own (Aspire runs it in development): it must be restarted where it runs. */
  | { readonly outcome: 'restart-elsewhere' }
  /** The requests could not be read, or the service could not be restarted: the details are in the app's log. */
  | { readonly outcome: 'failed' };

/** What the desktop app lets the user interface do with plugins. Every change waits for the user's confirmation. */
export interface PluginManagementBridge {
  /** The packages in the inbox; the other files are left out. */
  readonly listInbox: () => Promise<readonly InboxPackage[]>;
  /** Lets the user choose a package file, and copies it into the inbox. */
  readonly addPackage: () => Promise<AddPackageResult>;
  /** Opens the inbox in the file explorer, for packages dropped there by hand. */
  readonly openInbox: () => Promise<void>;
  /** Examines a package of the inbox, asks the user, and writes the request to install it. */
  readonly install: (name: string) => Promise<PluginActionResult>;
  /** Asks the user, and writes the request to uninstall an installed plugin. */
  readonly uninstall: (target: PluginTarget) => Promise<PluginActionResult>;
  /** Asks the user, and writes the request to enable a plugin. */
  readonly enable: (target: PluginTarget) => Promise<PluginActionResult>;
  /** Writes the request to disable a plugin: it reduces what runs, and needs no confirmation. */
  readonly disable: (target: PluginTarget) => Promise<PluginActionResult>;
  /** Cancels the request waiting for a plugin. */
  readonly cancelRequest: (target: PluginTarget) => Promise<PluginActionResult>;
  /** The requests waiting for the next restart. */
  readonly listRequests: () => Promise<readonly PendingPluginRequest[]>;
  /** Restarts the local service when a request waits, so that it applies them; the page reloads once it is ready. */
  readonly restart: () => Promise<RestartResult>;
}

/** What became of a request to start without the installed plugins, from the screen of a service that failed. */
export type RecoveryResult =
  /** The request is written and the service starts again: the installed plugins come back disabled. */
  | { readonly outcome: 'restarting' }
  /** The service has not failed: nothing is written (the "Plugins" screen disables plugins one by one). */
  | { readonly outcome: 'not-failed' }
  /** The request could not be written: the details are in the app's log. */
  | { readonly outcome: 'failed' };
