---
paths:
  - "plugins/**"
  - "src/dotnet/Voyager.Sdk/**"
  - "src/dotnet/Voyager.PluginHost/**"
  - "src/ts/plugin-sdk/**"
  - "src/ts/ui/src/plugins/**"
  - "proto/voyager/pluginhost/**"
  - "tests/plugin-fixtures/**"
  - "tests/Voyager.PluginHost.Tests/**"
  - ".claude/skills/new-plugin/**"
---

# Plugins et contrats

Références : [ADR-0015](../../docs/adr/0015-mecanisme-de-plugins.md) (mécanisme),
[ADR-0017](../../docs/adr/0017-contrats-versionnes-et-dependances-entre-plugins.md) (contrats versionnés),
[ADR-0020](../../docs/adr/0020-plugins-sur-le-serveur.md) (plugins sur le serveur, qui remplace en partie
l'[ADR-0016](../../docs/adr/0016-installation-et-gestion-des-plugins.md)),
[spec de la bascule](../../docs/specs/plateforme-bascule-client-serveur.md), section « Contrats du SDK » des
[conventions C#](../../docs/conventions/csharp.md). La [spec de la gestion des
plugins](../../docs/specs/plateforme-gestion-des-plugins.md) est à réécrire pour le serveur. L'essentiel :

## Manifeste (`plugin.json`)

- `id` : mots en minuscules séparés par des tirets, 64 caractères au plus. Jamais un nom que Windows donne à
  un périphérique (`con`, `prn`, `aux`, `nul`, `com0` à `com9`, `lpt0` à `lpt9`), ni `service` (réservé à
  l'hôte, `PluginCatalog.SERVICE_ID`). Le nom du dossier du plugin est son identifiant.
- **Jamais de `version` dans le dépôt** : `tools/build-plugins.ps1` l'injecte (ADR-0007).
- `dotnet.assembly` et/ou `ui.module` : au moins une partie.
- `provides` et `requires` listent des objets `{ "contract": "<identifiant>", "version": <entier ≥ 1> }`,
  32 au plus par liste, chaque contrat une seule fois par liste.
- `requires` contient **toujours** le contrat de base `{ "contract": "plugin", "version": 1 }` ; aucun plugin
  ne le **fournit**. L'ancien champ `sdk` n'existe plus.
- Une clé JSON écrite deux fois rend le manifeste invalide.

## Compatibilité par contrat

- Un plugin est jugé **contrat par contrat**, jamais sur la version du SDK. Chaque version de contrat est
  déclarée dans `ContractVersions` (`Voyager.Sdk`) avec son état : prise en charge, dépréciée ou obsolète.
- Une version obsolète ou inconnue d'un contrat fourni ou requis écarte le plugin avec **une seule raison**,
  « contrat non pris en charge ». L'état de chaque contrat (`ContractUse`) dit lequel et quoi faire :
  inconnu → mettre à jour Voyager ; obsolète → mettre à jour le plugin. Tous les contrats en cause sont
  listés, pas seulement le premier.
- Une version dépréciée charge le plugin, avec un avertissement (journal en `Warning`, carte du plugin).
- **Étape 1** (pas de dépendances entre plugins) : un plugin ne peut requérir que les contrats que
  l'application fournit (`ContractVersions.ProvidedByApplication`, aujourd'hui `plugin`). Tout autre contrat
  requis est « inconnu » : le plugin est écarté, jamais chargé sans son prérequis. L'étape 2 apportera la raison
  « prérequis non fourni ».
- Faire évoluer un contrat : dans une version, seulement des ajouts compatibles (nouveau type, membre non
  abstrait, méthode d'interface par défaut, valeur facultative, valeur d'énumération déjà gérée quand elle est
  inconnue). Tout le reste crée une **nouvelle version**, déclarée dans `ContractVersions` avec l'état de
  l'ancienne. Les deux moitiés d'un contrat (.NET et `@voyager/plugin-sdk`) ont le même numéro.
- Numéro du SDK : déclarer une version obsolète est `feat!:`, ajouter un contrat ou une version est `feat:`.

## Plugins sur le serveur du groupe (ADR-0020)

- Seul le **serveur du groupe** charge les plugins (`AddServerPlugins`, `Voyager.GroupServer/PluginStartup.cs`).
  Il ne lit pour l'instant que les **plugins livrés** : `/app/plugins` dans l'image, `artifacts/plugins/` en
  développement (`Voyager:ShippedPluginsDirectory`, refusé hors de l'environnement `Development`). Le dossier
  n'est jamais choisi par un utilisateur.
- `PluginLocations` garde un dossier utilisateur facultatif (`installed/`, `staging/`) pour la future gestion sur
  le serveur ; le serveur le laisse vide. Aucun plugin n'est donc désactivé ni suspendu aujourd'hui : désactiver,
  installer, suspendre et activer pour un groupe viendront avec la gestion sur le serveur, et sa spec.
- Racines lues dans l'ordre : livrés, puis installés. Un identifiant n'est actif qu'une fois : un installé qui
  porte celui d'un livré est écarté (`DuplicateOfShipped`), un autre doublon l'est avec `DuplicateId`. Les dossiers
  qui commencent par `.` ne sont jamais des plugins. L'hôte désigne un plugin par son origine et son **dossier**.
- Le serveur publie, pour chaque plugin chargé, ses **fichiers d'interface** avec leur empreinte SHA-256 et leur
  taille (`InterfaceFileCatalog`, `interface_files` de `ListPlugins`), et envoie un fichier à la demande
  (`GetInterfaceFile`).
- L'adresse d'un fichier de plugin dans l'application nomme son origine et sa version :
  `voyager://app/plugins/<origine>/<id>/<version>/ui/…` et `…/i18n/<langue>/messages.ftl`. Le processus principal
  ne les sert que depuis des modules **préparés** : livrés avec l'application (si la version et les empreintes sont
  celles du serveur), ou téléchargés et vérifiés par leur empreinte dans
  `%LOCALAPPDATA%\Voyager\servers\<serveur>\plugins\<id>\<version>` (`interfaceModules.ts`, `pluginFiles.ts`).
  Jamais le manifeste ni les assemblies ; un plugin écarté ou désactivé n'est pas servi ; le dossier d'un plugin ne
  peut pas être un lien, un fichier doit y rester une fois les liens suivis, et il est lu à son chemin réel.

## Paquets (`.voyagerplugin`)

`Voyager.PluginHost/Packages` garde l'**examen** (`PluginPackages.ExamineAsync`) et l'**extraction**
(`PluginPackageExtractor`) d'un paquet pour la future installation sur le serveur (ADR-0020) ; rien ne les appelle
encore, et le serveur ne sert pas `ExaminePackage`.

- Une archive zip : `plugin.json` et le reste du plugin à la racine. Contenu autorisé (`PackageEntryRules`, et la
  même liste dans `tools/pack-plugin.ps1`) : à la racine le manifeste, les assemblies .NET et leurs `.pdb`,
  `.xml`, `.deps.json`, une `signature.json` facultative (jamais extraite) ; `ui/` (`.js`, `.mjs`, `.css`,
  `.json`, `.svg`, `.png`) ; `i18n/` (`.ftl`) ; `data/` (tout type). Une DLL native est refusée.
- Refusés : chemin qui sort du dossier, chemin absolu ou avec lecteur, barre oblique inverse, `:` (flux), nom de
  périphérique (`con`, `nul.txt`, `com0`, `com¹`…), nom court 8.3 (`~1`), point ou espace final, doublon de casse,
  lien, fichier et dossier du même nom. **Aucune limite de taille** : seule la place libre du disque compte (tenue
  pendant toute l'extraction, sans débordement de calcul), et une entrée dont les octets ne sont pas ceux que
  l'archive annonce (longueur, CRC-32) est refusée. Une archive abîmée donne un refus, jamais une exception.
- Un paquet à examiner est une copie sous un **nom aléatoire** (`<32 hex>.voyagerplugin`, jamais le nom choisi
  par l'utilisateur) ; l'examen (un à la fois, annulable) n'écrit que les fichiers de la racine (manifeste,
  assemblies à vérifier) et lit le reste sans l'écrire ; rien n'est installé ni exécuté. L'empreinte est celle des
  octets lus.
- « Vérifié » n'est jamais « Garanti par l'application » : seul un paquet vérifié par une clé connue se passe de
  l'avertissement.
- Un nom choisi par l'utilisateur est réduit au nom seul, nettoyé (`DisplayText.Clean`) et **jamais journalisé**.
  Une erreur de fichier se journalise par son type et son code, jamais par son message, qui contient des chemins.

## Code et contrat gRPC

- Un plugin ne référence que `Voyager.Sdk` (et côté interface `@voyager/plugin-sdk`, React et Fluent UI
  fournis par la page).
- Une raison d'écartement est une **valeur**, jamais un texte : l'interface la traduit (FR et EN).
- `pluginhost.proto` : on ajoute, on ne renumérote ni ne supprime. `PLUGIN_REJECTION_REASON_SDK_INCOMPATIBLE`
  reste (déprécié, plus envoyé).
- Journaux de l'hôte : identifiants de plugin et de contrat, versions et états seulement. Jamais un chemin, un
  nom de dossier invalide, un nom de fichier ni le contenu d'un manifeste.

## Tests

- Les échecs de chargement se testent avec les plugins de `tests/plugin-fixtures/` et `PluginFolder`
  (`Manifest(id, assembly, provides, requires, uiPart)`), jamais avec un vrai plugin cassé.
- Les manifestes des plugins livrés sont chargés tels qu'ils sont dans le dépôt
  (`PluginFolder.AddShippedPlugin`) : un manifeste qui ne suit plus les contrats du SDK fait échouer un test.
- Un état ou une raison de contrat se teste avec une table `ContractSupport` déclarée dans le test (par
  exemple un contrat d'essai déprécié), tant qu'aucun vrai contrat n'a évolué.
