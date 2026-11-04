plugins-dialog-title = Voyager plugins
plugins-dialog-cancel = Cancel
plugins-dialog-understand = I understand
plugins-dialog-understand-reminder = Tick “I understand” to confirm.
plugins-dialog-warning = A .NET plugin runs with your rights on this computer. An interface plugin can talk to the Voyager service and read your trips. The author of this plugin is not verified.
plugins-dialog-author-not-proven = Nothing proves that the author of the new version is the author of the old one.
plugins-dialog-install-question = Install the plugin “{ $id }”?
plugins-dialog-upgrade-question = Replace version { $from } of the plugin “{ $id }” with version { $to }?
plugins-dialog-downgrade-question = Install an older version of the plugin “{ $id }”? Version { $to } will replace version { $from }.
plugins-dialog-reinstall-question = Reinstall version { $version } of the plugin “{ $id }”?
plugins-dialog-install = Install
plugins-dialog-replace = Replace
plugins-dialog-reinstall = Reinstall
plugins-dialog-file = File: { $name }
plugins-dialog-fingerprint = Fingerprint (SHA-256): { $sha256 }
plugins-dialog-identifier = Identifier: { $id }
plugins-dialog-version = Version: { $version }
plugins-dialog-provides = Provided contracts: { $contracts }
plugins-dialog-requires = Required contracts: { $contracts }
plugins-dialog-none = none
plugins-dialog-brings = Brings: { $parts ->
    [dotnet-and-ui] .NET code and an interface
    [dotnet] .NET code
   *[ui] an interface
}
plugins-dialog-verification = Verification: { $verification ->
    [guaranteed] Guaranteed by the application
    [verified] Verified
   *[unverified] Not verified
}
plugins-dialog-duplicate-of-shipped = A plugin shipped with Voyager already has this identifier: this one will stay set aside until you disable the shipped plugin.
plugins-dialog-deprecated = Version { $version } of the contract “{ $contract }” is deprecated: a later version of Voyager will no longer support it.
plugins-dialog-applied-at-restart = The change applies at the next restart.
plugins-dialog-uninstall-question = Uninstall the plugin “{ $id }” (version { $version })?
plugins-dialog-uninstall = Uninstall
plugins-dialog-uninstall-detail = Its files will be deleted. Your trip data is not touched.
plugins-dialog-enable-question = Enable the plugin “{ $id }” (version { $version })?
plugins-dialog-enable = Enable
plugins-dialog-enable-detail = It will load at startup and act in Voyager again.
plugins-dialog-choose-package = Add a plugin package
plugins-dialog-package-files = Voyager plugin packages
plugins-dialog-reenable-question = Enable the plugin “{ $id }” (version { $version }) again?
plugins-dialog-suspicion = This plugin is suspected of having crashed Voyager. It is a suspicion, not a certainty: another plugin, or a power cut, may have caused it.
plugins-dialog-reenable-detail = It will load at the next start. If it crashes Voyager again, it will be suspended again.
plugins-dialog-reenable = Enable again
