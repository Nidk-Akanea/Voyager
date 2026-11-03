plugin-management-title = Plugins
plugin-management-loading = Looking for plugins…
plugin-management-error = The list of plugins could not be read.
plugin-management-retry = Try again
plugin-management-none = Voyager knows no plugin.
plugin-management-requests-error = The waiting requests could not be read.
plugin-management-version = Version { $version }
plugin-management-origin-shipped = Shipped with Voyager
plugin-management-origin-installed = Installed by you
plugin-management-verification-guaranteed = Guaranteed by the application
plugin-management-verification-unverified = Not verified
plugin-management-state-loaded = Loaded
plugin-management-state-rejected = Set aside
plugin-management-state-disabled = Disabled
plugin-management-state-pending = Waiting for a restart
plugin-management-state-failed = Request failed
plugin-management-pending-install = Will be installed at the next restart.
plugin-management-pending-uninstall = Will be uninstalled at the next restart.
plugin-management-pending-enable = Will be enabled at the next restart.
plugin-management-pending-disable = Will be disabled at the next restart. Until then, it keeps working.
plugin-management-pending-replace = Will be replaced at the next restart by the package “{ $name }”.
plugin-management-failed = { $action ->
    [install] The installation
    [uninstall] The uninstallation
    [enable] Enabling
   *[disable] Disabling
} could not be applied: { $reason ->
    [not-found] the plugin is no longer there.
    [not-allowed] this is not allowed for this plugin.
    [file-system-error] its files could not be changed (rights, or a file open in another program).
    [not-supported] this version of Voyager cannot do it yet.
    [package-missing] the copy of the package is no longer there.
    [package-changed] the copy of the package changed since you confirmed.
    [package-refused] the package was refused, or holds another plugin.
    [not-enough-space] not enough disk space.
   *[other] the reason is not known.
}
plugin-management-brings = Brings: { $parts ->
    [dotnet-and-ui] .NET code and an interface
    [dotnet] .NET code
    [ui] an interface
   *[unknown] unknown (unreadable manifest)
}
plugin-management-contract = { $role ->
    [provides] Provides
   *[requires] Requires
} { $contract }, version { $version }: { $state ->
    [supported] supported
    [deprecated] deprecated, a later version of Voyager will remove it
    [obsolete] removed from this application
   *[unknown] unknown to this application
}
plugin-management-installed-at = Installed on { $date }
plugin-management-package = Original package: { $name }
plugin-management-package-sha256 = Fingerprint (SHA-256): { $sha256 }
plugin-management-enable = Enable
plugin-management-disable = Disable
plugin-management-uninstall = Uninstall
plugin-management-cancel-request = Cancel the request
plugin-management-new-install = New plugin “{ $id }”
plugin-management-new-install-package = Package: { $name }
plugin-management-missing = Not in the list of plugins.
plugin-management-pending-count = { $count ->
    [one] One request waits for a restart.
   *[other] { $count } requests wait for a restart.
}
plugin-management-restart = Restart to apply
plugin-management-restarting = Restarting the service…
plugin-management-inbox-title = Inbox
plugin-management-inbox-help = Drop a .voyagerplugin file in the inbox, or add one. Installing a plugin does nothing before you confirm.
plugin-management-inbox-empty = No package in the inbox.
plugin-management-inbox-loading = Reading the inbox…
plugin-management-inbox-error = The inbox could not be read.
plugin-management-inbox-add = Add a package…
plugin-management-inbox-open = Open the inbox
plugin-management-inbox-refresh = Refresh
plugin-management-inbox-install = Install
plugin-management-inbox-package = { $size } KB, changed on { $date }
plugin-management-outcome-requested = Noted: the change applies at the next restart.
plugin-management-outcome-nothing-to-apply = Nothing to apply: the waiting request is cancelled.
plugin-management-outcome-not-found = This package or plugin is no longer there. The list was read again.
plugin-management-outcome-not-allowed = This is not allowed for this plugin.
plugin-management-outcome-service-not-ready = The Voyager service is restarting: try again in a moment.
plugin-management-outcome-failed = The change could not be saved. The details are in the Voyager log.
plugin-management-refused = This package is refused: { $reason ->
    [unreadable] it is not a readable archive.
    [manifest-invalid] its manifest is missing or invalid, or it brings nothing.
    [declared-file-missing] a file its manifest declares is missing.
    [contract-not-supported] it uses a contract in a version this application does not support.
    [forbidden-entry] it holds a file or a name that is not allowed.
    [not-enough-space] not enough disk space.
    [package-changed] it changed while it was examined.
    [not-a-package] it is not a .voyagerplugin file.
    [empty] the file is empty.
   *[other] the reason is not known.
}
plugin-management-outcome-added = The package “{ $name }” is in the inbox.
plugin-management-outcome-open-failed = The inbox could not be opened. The details are in the Voyager log.
plugin-management-outcome-restart-elsewhere = Another program runs the service (Aspire, in development): restart it from there, then reload the window.
plugin-management-outcome-restart-failed = The service could not restart. The details are in the Voyager log.
plugin-management-outcome-nothing-to-restart = No request waits: nothing to restart.
plugin-management-state-suspended = Suspended
plugin-management-suspended = This plugin is suspected of having crashed Voyager: it is not loaded, and what it brings is no longer available. It is a suspicion, not a certainty: disable it for good, or enable it again.
plugin-management-disabled = It is not loaded: what it brings is no longer available.
plugin-management-disabled-for-recovery = Disabled for recovery: enable the installed plugins again one by one, restarting each time, to find the one that crashes Voyager.
plugin-management-reenable = Enable again
plugin-management-disable-for-good = Disable for good
