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
- L'adresse d'un fichier de plugin nomme son origine : `plugins/<shipped|installed>/<id>/ui/…` et
  `…/i18n/<langue>/messages.ftl`. Le protocole `voyager://app` ne sert que les plugins **chargés**, depuis le
  dossier que donne la liste du service ; le dossier d'un plugin ne peut pas être un lien, un fichier doit y rester
  une fois les liens suivis, et il est lu à son chemin réel. Après un redémarrage du service, la page est
  rechargée.

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
