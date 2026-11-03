plugin-management-title = Plugins
plugin-management-loading = Recherche des plugins…
plugin-management-error = La liste des plugins n’a pas pu être lue.
plugin-management-retry = Réessayer
plugin-management-none = Aucun plugin n’est connu de Voyager.
plugin-management-requests-error = Les demandes en attente n’ont pas pu être lues.
plugin-management-version = Version { $version }
plugin-management-origin-shipped = Livré avec Voyager
plugin-management-origin-installed = Installé par vous
plugin-management-verification-guaranteed = Garanti par l’application
plugin-management-verification-unverified = Non vérifié
plugin-management-state-loaded = Chargé
plugin-management-state-rejected = Écarté
plugin-management-state-disabled = Désactivé
plugin-management-state-pending = En attente de redémarrage
plugin-management-state-failed = Demande en échec
plugin-management-pending-install = Sera installé au prochain redémarrage.
plugin-management-pending-uninstall = Sera désinstallé au prochain redémarrage.
plugin-management-pending-enable = Sera activé au prochain redémarrage.
plugin-management-pending-disable = Sera désactivé au prochain redémarrage. D’ici là, il continue de fonctionner.
plugin-management-pending-replace = Sera remplacé au prochain redémarrage par le paquet « { $name } ».
plugin-management-failed = { $action ->
    [install] L’installation
    [uninstall] La désinstallation
    [enable] L’activation
   *[disable] La désactivation
} n’a pas pu être appliquée : { $reason ->
    [not-found] le plugin n’est plus là.
    [not-allowed] ce geste n’est pas permis pour ce plugin.
    [file-system-error] ses fichiers n’ont pas pu être modifiés (droits, ou fichier ouvert par un autre programme).
    [not-supported] cette version de Voyager ne sait pas encore le faire.
    [package-missing] la copie du paquet n’est plus là.
    [package-changed] la copie du paquet a changé depuis votre confirmation.
    [package-refused] le paquet a été refusé, ou contient un autre plugin.
    [not-enough-space] espace disque insuffisant.
   *[other] la raison n’est pas connue.
}
plugin-management-brings = Apporte : { $parts ->
    [dotnet-and-ui] du code .NET et une interface
    [dotnet] du code .NET
    [ui] une interface
   *[unknown] inconnu (manifeste illisible)
}
plugin-management-contract = { $role ->
    [provides] Fournit
   *[requires] Requiert
} { $contract }, version { $version } : { $state ->
    [supported] pris en charge
    [deprecated] déprécié, une prochaine version de Voyager le retirera
    [obsolete] retiré de cette application
   *[unknown] inconnu de cette application
}
plugin-management-installed-at = Installé le { $date }
plugin-management-package = Paquet d’origine : { $name }
plugin-management-package-sha256 = Empreinte (SHA-256) : { $sha256 }
plugin-management-enable = Activer
plugin-management-disable = Désactiver
plugin-management-uninstall = Désinstaller
plugin-management-cancel-request = Annuler la demande
plugin-management-new-install = Nouveau plugin « { $id } »
plugin-management-new-install-package = Paquet : { $name }
plugin-management-missing = Absent de la liste des plugins.
plugin-management-pending-count = { $count ->
    [one] Une demande attend le redémarrage.
   *[other] { $count } demandes attendent le redémarrage.
}
plugin-management-restart = Redémarrer pour appliquer
plugin-management-restarting = Redémarrage du service…
plugin-management-inbox-title = Boîte de dépôt
plugin-management-inbox-help = Déposez un fichier .voyagerplugin dans la boîte de dépôt, ou ajoutez-en un. Installer un plugin ne fait rien avant votre confirmation.
plugin-management-inbox-empty = Aucun paquet dans la boîte de dépôt.
plugin-management-inbox-loading = Lecture de la boîte de dépôt…
plugin-management-inbox-error = La boîte de dépôt n’a pas pu être lue.
plugin-management-inbox-add = Ajouter un paquet…
plugin-management-inbox-open = Ouvrir la boîte de dépôt
plugin-management-inbox-refresh = Actualiser
plugin-management-inbox-install = Installer
plugin-management-inbox-package = { $size } Ko, modifié le { $date }
plugin-management-outcome-requested = C’est noté : le changement sera appliqué au prochain redémarrage.
plugin-management-outcome-nothing-to-apply = Rien à appliquer : la demande en attente est annulée.
plugin-management-outcome-not-found = Ce paquet ou ce plugin n’est plus là. La liste a été relue.
plugin-management-outcome-not-allowed = Ce geste n’est pas permis pour ce plugin.
plugin-management-outcome-service-not-ready = Le service de Voyager redémarre : réessayez dans un instant.
plugin-management-outcome-failed = Le changement n’a pas pu être enregistré. Le détail est dans le journal de Voyager.
plugin-management-refused = Ce paquet est refusé : { $reason ->
    [unreadable] ce n’est pas une archive lisible.
    [manifest-invalid] son manifeste est absent ou invalide, ou il n’apporte rien.
    [declared-file-missing] un fichier que son manifeste annonce est absent.
    [contract-not-supported] il utilise un contrat dans une version que cette application ne prend pas en charge.
    [forbidden-entry] il contient un fichier ou un nom qui n’est pas permis.
    [not-enough-space] espace disque insuffisant.
    [package-changed] il a changé pendant son examen.
    [not-a-package] ce n’est pas un fichier .voyagerplugin.
    [empty] le fichier est vide.
   *[other] la raison n’est pas connue.
}
plugin-management-outcome-added = Le paquet « { $name } » est dans la boîte de dépôt.
plugin-management-outcome-open-failed = La boîte de dépôt n’a pas pu être ouverte. Le détail est dans le journal de Voyager.
plugin-management-outcome-restart-elsewhere = Le service est lancé par un autre programme (Aspire, en développement) : redémarrez-le depuis celui-ci, puis rechargez la fenêtre.
plugin-management-outcome-restart-failed = Le service n’a pas pu redémarrer. Le détail est dans le journal de Voyager.
plugin-management-outcome-nothing-to-restart = Aucune demande n’attend : rien à redémarrer.
