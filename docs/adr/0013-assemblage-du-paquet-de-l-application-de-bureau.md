# ADR-0013 — Assemblage du paquet de l'application de bureau

- **Statut** : Acceptée
- **Date** : 2026-10-26
- **Remplace** : — (complète [ADR-0009](0009-installation-et-mises-a-jour-velopack.md), qui laisse ouvert
  l'outil assemblant le dossier de l'application, et précise la ligne « Electron + Electron Forge » de
  [ADR-0006](0006-dependances-principales.md))

## Contexte

[ADR-0009](0009-installation-et-mises-a-jour-velopack.md) a choisi Velopack pour l'installateur et les
mises à jour, et dit qu'Electron Forge ne fait plus le packaging. Elle ne dit pas qui prépare, avant
Velopack, le **dossier de l'application** : l'exécutable d'Electron, le code dans une archive `asar`, le
service local publié et l'interface construite à côté. Velopack prend ce dossier tel quel (`vpk pack`).

Trois autres choix se sont imposés en construisant `tools/release.ps1` :

- **Verrouiller l'exécutable.** Electron embarque des *fuses*, des interrupteurs écrits dans le binaire
  qu'aucune variable d'environnement ni option de la machine de l'utilisateur ne peut rallumer.
  Laissées par défaut, elles permettent de lancer n'importe quel code avec `Voyager.exe`
  (`ELECTRON_RUN_AS_NODE`, `NODE_OPTIONS`, `--inspect`). Le dépôt est public et l'application manipule
  des itinéraires : le backlog demandait de les verrouiller.
- **Où vivent les données.** Velopack installe l'application dans `%LOCALAPPDATA%\<identifiant du
  paquet>` et vide ce dossier à la désinstallation ou à la réinstallation. La base SQLite et les journaux
  sont dans `%LOCALAPPDATA%\Voyager`. Avec l'identifiant `Voyager`, les deux seraient le même dossier.
- **Journaux sur le poste.** [ADR-0006](0006-dependances-principales.md) prévoyait des fichiers
  tournants (Serilog côté service, `electron-log` côté application) ;
  [ADR-0012](0012-journaux-de-developpement-dans-seq-avec-serilog.md) les annonçait comme une sortie de
  plus de la même configuration Serilog.

## Options

### Option A — `@electron/packager` + `@electron/fuses`, puis Velopack

- Avantages : `@electron/packager` est la brique de bas niveau d'Electron Forge, maintenue par
  l'équipe d'Electron ; elle fait exactement le travail voulu (asar, ressources supplémentaires,
  modules natifs dépaquetés) sans imposer d'installateur. `@electron/fuses` est l'outil officiel pour
  les fuses et sait relire ceux d'un binaire, ce qui permet de **vérifier** le résultat à chaque
  paquet. Deux dépendances de développement, aucune dans l'application.
- Inconvénients : un script à nous (`packageApp.ts`, environ 150 lignes) prépare le dossier et copie
  les modules qui ne sont pas inclus dans le code compilé (`velopack`, `electron-log`). À maintenir
  quand Electron ou ces outils changent.

### Option B — `electron-builder`

- Avantages : fait tout (asar, fuses, installateur, mises à jour) avec un fichier de configuration ;
  très répandu.
- Inconvénients : il apporte son propre installateur et son propre système de mise à jour, que Velopack
  remplace : on en désactiverait la moitié. Configuration déclarative plus difficile à vérifier que le
  résultat lui-même. Une dépendance plus lourde (et plus de code de tiers dans la chaîne de
  livraison) pour une fraction de ce qu'elle sait faire.

### Option C — Electron Forge

- Avantages : outil officiel, plugins pour les fuses et le packaging.
- Inconvénients : [ADR-0009](0009-installation-et-mises-a-jour-velopack.md) l'écarte du packaging ; son
  intérêt (makers d'installateurs, publication) ferait doublon avec Velopack. Il lui faut sa propre
  configuration et ses plugins pour un résultat qu'Option A obtient avec deux bibliothèques.

### Identifiant du paquet Velopack

- **`Voyager`** : identique au dossier des données. Simple, mais la désinstallation et la
  réinstallation peuvent effacer la base de l'utilisateur.
- **`VoyagerApp`** : l'application s'installe dans `%LOCALAPPDATA%\VoyagerApp`, les données restent dans
  `%LOCALAPPDATA%\Voyager`. Le nom affiché (raccourcis, installateur) reste « Voyager ».
- **Déplacer les données** (`%LOCALAPPDATA%\VoyagerData`) : même effet, mais change des chemins déjà
  documentés et utilisés par le service local, Aspire et les tests, pour un gain nul.

L'identifiant du paquet est l'identité de l'application pour Velopack : le changer après la première
publication oblige à réinstaller. C'est maintenant, avant toute release, qu'il coûte le moins.

## Décision

**Option A**, avec l'identifiant de paquet **`VoyagerApp`** :

1. `@electron/packager` assemble le dossier de l'application ; Velopack (`vpk`, épinglé dans
   `dotnet-tools.json` à la version de la bibliothèque npm `velopack`) en fait l'installateur et les
   paquets de mise à jour.
2. Les fuses sont posées sur `Voyager.exe` : `RunAsNode`, `NODE_OPTIONS` et `--inspect` désactivés ;
   code chargé seulement depuis `app.asar`, avec contrôle de son empreinte ; `file://` sans privilèges
   supplémentaires. La liste est dans `src/ts/desktop/src/packaging/fuses.ts`, qui exige une décision
   pour toute fuse ajoutée par une version future d'Electron, et le script **relit** le binaire produit
   et échoue s'il diffère.
3. Les journaux de l'application installée vont dans des fichiers de `%LOCALAPPDATA%\Voyager\logs` :
   Serilog côté service (un fichier par jour, 10 Mo au plus, 14 conservés), `electron-log` côté
   application. Le dossier personnel y est remplacé par `~` des deux côtés.
4. Le service local est lancé depuis le dossier de son exécutable : ASP.NET Core y cherche ses fichiers
   de réglages, pas à côté de l'exécutable.

## Conséquences

- Positif : un paquet reproductible d'une seule commande, dont les verrous sont vérifiés à chaque
  construction ; aucune dépendance supplémentaire dans l'application elle-même ; les données de
  l'utilisateur ne dépendent plus du cycle de vie de l'installation.
- Négatif : un script de packaging à entretenir ; trois dépendances de développement de plus
  (`@electron/packager`, `@electron/fuses`, `vpk`), choisies parmi des versions publiées depuis plus de
  14 jours ; pas de signature de code (SmartScreen avertira) ni d'icône pour l'instant.
- **À vérifier avant la première release**, car cela touche aux données de l'utilisateur : installer
  avec `Setup.exe`, créer des données, désinstaller et réinstaller, et constater que
  `%LOCALAPPDATA%\Voyager\voyager.db` est intact. Le comportement exact de la désinstallation de Velopack
  n'a pas été confirmé ici, d'où la précaution de l'identifiant distinct.
- À faire ensuite (backlog) : workflow de release en CI, publication sur GitHub, politique de canaux
  (un utilisateur de la bêta reçoit-il la version stable ?), icône, signature de code.
