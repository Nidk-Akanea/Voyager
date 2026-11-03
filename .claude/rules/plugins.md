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
[ADR-0016](../../docs/adr/0016-installation-et-gestion-des-plugins.md) (installation et gestion),
[ADR-0017](../../docs/adr/0017-contrats-versionnes-et-dependances-entre-plugins.md) (contrats versionnés),
[spec de la gestion des plugins](../../docs/specs/plateforme-gestion-des-plugins.md), section « Contrats du
SDK » des [conventions C#](../../docs/conventions/csharp.md). L'essentiel :

## Manifeste (`plugin.json`)

- `id` : mots en minuscules séparés par des tirets, 64 caractères au plus. Jamais un nom que Windows donne à
  un périphérique (`con`, `prn`, `aux`, `nul`, `com0` à `com9`, `lpt0` à `lpt9`), ni `service` (réservé au
  service). Le nom du dossier du plugin est son identifiant.
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

## Gestion des plugins (ADR-0016)

- Deux **racines**, lues dans cet ordre : les plugins livrés (dossier de l'application, `ShippedPluginsDirectory`),
  puis les installés (`<UserPluginsDirectory>/installed/<id>`). Les dossiers sont donnés par le processus principal,
  jamais par un réglage propre à Voyager dans l'application installée (seul le dossier standard de Windows,
  `%LOCALAPPDATA%`, comme pour les journaux). En développement, les données de l'utilisateur sont sous
  `artifacts/user-plugins`, jamais celles de l'application installée.
- Un identifiant n'est actif qu'une fois : un installé qui porte celui d'un livré est écarté
  (`DuplicateOfShipped`), un autre doublon l'est avec `DuplicateId`. Un plugin désactivé est listé sans être chargé
  et laisse son identifiant libre. Le service désigne un plugin par son origine et son **dossier**.
- Le **processus principal écrit seul** `requests.json` ; le **service écrit seul** `state.json`. Le service
  applique les demandes au démarrage, **avant tout chargement** (`PluginStore.ApplyRequests`), une seule fois
  chacune (identifiants des demandes appliquées gardés dans l'état). Aucune opération de gestion n'est un appel
  gRPC : le service ne fait que lire.
- Une demande qui échoue ne bloque jamais le démarrage : elle reste sur le plugin comme dernière erreur
  (`PluginRequestFailure`). Les fichiers illisibles sont journalisés et ignorés.
- Supprimer un plugin installé le renomme d'abord en `.removing-<id>-…` (jamais à moitié sous son nom) ; les
  dossiers qui commencent par `.` ne sont jamais des plugins.
- **Plantages** : `PluginActivity` écrit `activity.json` (plugin en chargement, plugins en cours d'appel) et
  l'efface à l'arrêt normal (`ApplicationStopping`). Au démarrage, `PluginActivity.FindSuspect` désigne le plugin en
  chargement ou le seul en cours d'appel, jamais plusieurs ; `PluginStore.ApplyRequests(suspect)` le suspend
  (`PluginStatus.Suspended`, non chargé, identifiant laissé libre) avant d'appliquer les demandes. `Enable`,
  `Disable` et un remplacement lèvent la suspension ; réactiver un plugin suspendu demande la confirmation renforcée
  (rappel du soupçon, « Je comprends »). La demande `recovery` (propriété de premier niveau de `requests.json`, écrite
  seulement quand le service a échoué) désactive une fois tous les plugins installés (`DisabledForRecovery`). La vue
  parle toujours d'un **soupçon**, jamais d'une certitude. **Tout appel de l'hôte dans le code d'un plugin** (un
  nouveau point d'extension compris) passe par `PluginActivity.BeginCall`/`EndCall`, comme les sources de faits.
- L'adresse d'un fichier de plugin nomme son origine : `plugins/<shipped|installed>/<id>/ui/…` et
  `…/i18n/<langue>/messages.ftl`. Le protocole `voyager://app` ne sert que les plugins **chargés**, depuis le
  dossier que donne la liste du service ; le dossier d'un plugin ne peut pas être un lien, un fichier doit y rester
  une fois les liens suivis, et il est lu à son chemin réel. Après un redémarrage du service, la page est
  rechargée.

## Paquets (`.voyagerplugin`)

- Une archive zip : `plugin.json` et le reste du plugin à la racine. Contenu autorisé (`PackageEntryRules`, et la
  même liste dans `tools/pack-plugin.ps1`) : à la racine le manifeste, les assemblies .NET et leurs `.pdb`,
  `.xml`, `.deps.json`, une `signature.json` facultative (jamais extraite) ; `ui/` (`.js`, `.mjs`, `.css`,
  `.json`, `.svg`, `.png`) ; `i18n/` (`.ftl`) ; `data/` (tout type). Une DLL native est refusée.
- Refusés : chemin qui sort du dossier, chemin absolu ou avec lecteur, barre oblique inverse, `:` (flux), nom de
  périphérique (`con`, `nul.txt`, `com0`, `com¹`…), nom court 8.3 (`~1`), point ou espace final, doublon de casse,
  lien, fichier et dossier du même nom. **Aucune limite de taille** : seule la place libre du disque compte (tenue
  pendant toute l'extraction, sans débordement de calcul), et une entrée dont les octets ne sont pas ceux que
  l'archive annonce (longueur, CRC-32) est refusée. Une archive abîmée donne un refus, jamais une exception.
- Le processus principal copie le paquet dans `staging/` sous un **nom aléatoire** (`<32 hex>.voyagerplugin`,
  jamais le nom choisi par l'utilisateur) et en calcule l'empreinte ; le service l'**examine** (`ExaminePackage`,
  un examen à la fois, annulable) : il n'écrit que les fichiers de la racine (manifeste, assemblies à vérifier)
  et lit le reste sans l'écrire ; rien n'est installé ni exécuté.
- Installer (au démarrage) : le paquet est lu une seule fois, l'empreinte des octets lus doit être celle
  confirmée, tout est revérifié, son identifiant doit être celui de la demande, puis il prend la place de
  l'ancien (renommé en `.replacing-…`, remis en place si un démarrage s'arrête au milieu). Un remplacement garde
  l'état activé ou désactivé. La copie n'est supprimée qu'une fois `state.json` écrit.
- Le **processus principal** (`src/ts/desktop/src/main/plugins/`) : la page ne lui donne qu'un nom de la boîte de
  dépôt (`isInboxPackageName`) ou une cible `{ origin, pluginId }` (`isPluginTarget`), jamais un chemin. Installer,
  remplacer, désinstaller et activer passent par une **boîte de dialogue du système** (`askConfirmation`) :
  « Annuler » par défaut, « Je comprends » pour un plugin non vérifié (la boîte se rouvre avec un rappel, une boîte
  native ne sait pas griser un bouton). Désactiver et annuler n'en demandent pas. Les textes des boîtes sont des
  messages Fluent (`src/main/i18n/<langue>/plugins.ftl`), en français et en anglais. « Vérifié » n'est jamais
  « Garanti par l'application » : seul un paquet vérifié par une clé connue se passe de l'avertissement. Une
  opération à la fois ; aucune demande ne change tant que le service n'est pas prêt (il peut être en train de les
  lire) ; une erreur est journalisée par son code (`describeFailure`) et la page ne reçoit qu'une issue
  (`PluginActionResult`), jamais un message d'erreur. Le format de `requests.json` est fixé par
  `tests/fixtures/plugin-requests.json`, lu par les tests des deux langages.
- Le nom du fichier choisi par l'utilisateur est réduit au nom seul, nettoyé (`DisplayText.Clean`) et **jamais
  journalisé**. Une erreur de fichier se journalise par son type et son code (`FileError.Describe`), jamais par
  son message, qui contient des chemins.

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
