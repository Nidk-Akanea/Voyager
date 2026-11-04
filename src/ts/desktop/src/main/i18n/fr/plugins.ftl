plugins-dialog-title = Plugins de Voyager
plugins-dialog-cancel = Annuler
plugins-dialog-understand = Je comprends
plugins-dialog-understand-reminder = Cochez « Je comprends » pour confirmer.
plugins-dialog-warning = Un plugin .NET s’exécute avec vos droits sur cet ordinateur. Un plugin d’interface peut parler au service de Voyager et lire vos voyages. L’auteur de ce plugin n’est pas vérifié.
plugins-dialog-author-not-proven = Rien ne prouve que l’auteur de la nouvelle version soit celui de l’ancienne.
plugins-dialog-install-question = Installer le plugin « { $id } » ?
plugins-dialog-upgrade-question = Remplacer la version { $from } du plugin « { $id } » par la version { $to } ?
plugins-dialog-downgrade-question = Installer une version plus ancienne du plugin « { $id } » ? La version { $to } remplacera la version { $from }.
plugins-dialog-reinstall-question = Réinstaller la version { $version } du plugin « { $id } » ?
plugins-dialog-install = Installer
plugins-dialog-replace = Remplacer
plugins-dialog-reinstall = Réinstaller
plugins-dialog-file = Fichier : { $name }
plugins-dialog-fingerprint = Empreinte (SHA-256) : { $sha256 }
plugins-dialog-identifier = Identifiant : { $id }
plugins-dialog-version = Version : { $version }
plugins-dialog-provides = Contrats fournis : { $contracts }
plugins-dialog-requires = Contrats requis : { $contracts }
plugins-dialog-none = aucun
plugins-dialog-brings = Apporte : { $parts ->
    [dotnet-and-ui] du code .NET et une interface
    [dotnet] du code .NET
   *[ui] une interface
}
plugins-dialog-verification = Vérification : { $verification ->
    [guaranteed] Garanti par l’application
    [verified] Vérifié
   *[unverified] Non vérifié
}
plugins-dialog-duplicate-of-shipped = Un plugin livré avec Voyager porte déjà cet identifiant : celui-ci restera écarté tant que vous ne désactivez pas le plugin livré.
plugins-dialog-deprecated = Le contrat « { $contract } » en version { $version } est déprécié : une prochaine version de Voyager ne le prendra plus en charge.
plugins-dialog-applied-at-restart = Le changement sera appliqué au prochain redémarrage.
plugins-dialog-uninstall-question = Désinstaller le plugin « { $id } » (version { $version }) ?
plugins-dialog-uninstall = Désinstaller
plugins-dialog-uninstall-detail = Ses fichiers seront supprimés. Vos données de voyage ne sont pas touchées.
plugins-dialog-enable-question = Activer le plugin « { $id } » (version { $version }) ?
plugins-dialog-enable = Activer
plugins-dialog-enable-detail = Il sera chargé au démarrage et pourra de nouveau agir dans Voyager.
plugins-dialog-choose-package = Ajouter un paquet de plugin
plugins-dialog-package-files = Paquets de plugin Voyager
plugins-dialog-reenable-question = Réactiver le plugin « { $id } » (version { $version }) ?
plugins-dialog-suspicion = Ce plugin est soupçonné d’avoir fait planter Voyager. C’est un soupçon, pas une certitude : un autre plugin, ou une coupure de courant, a pu en être la cause.
plugins-dialog-reenable-detail = Il sera chargé au prochain démarrage. S’il fait de nouveau planter Voyager, il sera suspendu de nouveau.
plugins-dialog-reenable = Réactiver
