# ADR-0015 — Mécanisme de plugins : découverte, chargement et preuve

- **Statut** : Proposée
- **Date** : 2026-10-26
- **Remplace** : — (complète [ADR-0002](0002-architecture-modulaire-par-contrats-et-plugins.md) et
  [ADR-0003](0003-choix-de-la-stack.md), qui renvoient à cet ADR le mécanisme concret ; précise la
  décision 7 de [ADR-0009](0009-installation-et-mises-a-jour-velopack.md) ; ajoute à la liste blanche de
  `Voyager.Sdk` d'[ADR-0006](0006-dependances-principales.md) deux paquets d'abstractions, et rend plus
  simple la « plage de versions » d'[ADR-0007](0007-git-flow-et-versions-gitversion.md))

## Contexte

Voyager est « entièrement modulaire » ([ADR-0002](0002-architecture-modulaire-par-contrats-et-plugins.md)) :
un cœur minimal, des contrats versionnés, et des plugins (aussi appelés *add-ins*), y compris pour les
fonctions de base. Le format du paquet est déjà fixé par [ADR-0003](0003-choix-de-la-stack.md) : un
dossier avec un manifeste `plugin.json`, une partie .NET chargée par le service local, et un module
d'interface React chargé par l'interface.

Il n'existe pourtant **aucun code** : `Voyager.Sdk`, `Voyager.PluginHost` et `@voyager/plugin-sdk` sont
des projets vides, et le dossier `plugins/` n'existe pas. Trois ADR renvoient à celui-ci ce qui reste à
décider : comment un plugin est **découvert et chargé**, **ce que le SDK expose** (question Q21),
**comment il arrive chez l'utilisateur** et **ce qui le protège** (question Q11). L'étape 3 du squelette
doit **éprouver** ces choix avec de vrais plugins avant d'écrire du métier.

Contraintes à respecter :

- un plugin ne dépend que du SDK .NET (`Voyager.Sdk`) et du SDK d'interface (`@voyager/plugin-sdk`) ;
  jamais des cas d'usage de `Voyager.Core` ni de l'infrastructure ;
- pas de point d'extension sans au moins **deux implémentations** connues ou prévues
  ([ADR-0002](0002-architecture-modulaire-par-contrats-et-plugins.md), règle 4) ;
- l'application est verrouillée (`contextIsolation`, `sandbox`, CSP stricte,
  [ADR-0013](0013-assemblage-du-paquet-de-l-application-de-bureau.md)) ; une interface qui charge du code
  dynamique ne doit pas l'affaiblir plus que nécessaire ;
- pas de métier avant la preuve : le point d'extension qui l'éprouve doit être technique.

## Options

### Chargement de la partie .NET

**Option A — Un `AssemblyLoadContext` par plugin, dans le processus du service local.**

- Avantages : intégré à .NET ([ADR-0006](0006-dependances-principales.md) le prévoyait déjà) ; aucune
  dépendance ; chaque plugin garde ses propres dépendances.
- Inconvénients : aucune isolation ; un plugin qui plante ou boucle emporte le service local ; il faut
  écrire à la main le partage des types avec l'hôte (voir la décision 2) ; un contexte peut être rendu
  déchargeable, mais l'hôte garde des références (conteneur d'injection, services gRPC) : on redémarre
  pour recharger.

**Option B — Un processus par plugin**, qui parle gRPC au service local.

- Avantages : isolation réelle (plantage, mémoire, droits) ; permet des plugins de tiers.
- Inconvénients : démarrage et mémoire multipliés par le nombre de plugins ; un contrat doit être
  entièrement exprimé en gRPC ; paquet, supervision et mise à jour de N processus. Démesuré tant que les
  plugins sont écrits par l'équipe.

**Option C — La bibliothèque `McMaster.NETCore.Plugins`**, qui enveloppe `AssemblyLoadContext`.

- Avantages : elle règle le partage de types (`PreferSharedTypes`) et la résolution des dépendances.
- Inconvénients : une dépendance de plus pour ce qui reste, une fois le partage décidé, un petit module
  d'environ deux cents lignes avec ses tests ; [ADR-0006](0006-dependances-principales.md) préfère le
  mécanisme intégré. Choix serré : à reprendre si ce module grossit.

### Chargement du module d'interface

**Option A — `import()` dynamique de modules ES, avec React et Fluent UI partagés par une *import map*.**
L'hôte sert les modules des plugins par son protocole `voyager://app`, et publie React, React DOM,
Fluent UI, `@fluent/react` et `@voyager/plugin-sdk` comme modules ES partagés que les plugins déclarent
« externes ».

- Avantages : standard du navigateur, aucune dépendance ; **une seule instance de React**, donc les
  hooks, le thème Fluent et le contexte de l'application fonctionnent dans les plugins ; même origine.
- Inconvénients : l'*import map* est un script de la page, donc la CSP change (décision 7) ; l'hôte doit
  lui aussi importer React par cette carte (sinon deux instances) ; Fluent UI exposé comme un seul
  module fait perdre à l'hôte son élagage (*tree-shaking*) ; chaque plugin doit être compilé avec les
  bonnes « externes ».

**Option B — *Module Federation*** (plugin de Vite ou de webpack), ou *Native Federation* (même idée,
fondée sur les *import maps*).

- Avantages : le partage de dépendances est géré par l'outil.
- Inconvénients : une dépendance de construction de plus et une configuration propre à l'outil, pour un
  résultat que l'option A obtient avec le standard ; à reconsidérer si la carte devient pénible à tenir.

**Option C — Une `iframe` par plugin.**

- Avantages : isolation des styles et du code.
- Inconvénients : pas de React, de thème Fluent ni de contexte partagés ; chaque plugin recharge ses
  bibliothèques ; communication par messages. Va contre « les plugins utilisent la copie de React de
  l'hôte » ([ADR-0003](0003-choix-de-la-stack.md)).

### Services gRPC d'un plugin

**Option A — C'est l'hôte qui possède les services gRPC.** Un point d'extension a un contrat .NET dans le
SDK ; l'hôte expose un service gRPC qui agrège les implémentations des plugins.

- Avantages : aucun enregistrement dynamique de services ; les `.proto` restent dans `proto/` ; suffit à
  éprouver le mécanisme.
- Inconvénients : un plugin ne peut pas, pour l'instant, ajouter un service gRPC à lui.

**Option B — Un plugin déclare ses propres `.proto` et services**, mappés dynamiquement.

- Avantages : un plugin vraiment autonome ; c'est la direction de `docs/conventions/grpc.md`.
- Inconvénients : génération de code par plugin (Buf, C#, TypeScript), `MapGrpcService` dynamique,
  versions de contrat par plugin. À faire quand deux plugins en auront un besoin réel.

## Décision

**Option A** pour le .NET, pour l'interface et pour les services gRPC (avec B reportée), dans le cadre
suivant. L'utilisateur a choisi le périmètre de la preuve, la distribution et le niveau de confiance.

1. **Manifeste `plugin.json`**, seule source de vérité : `id` unique en kebab-case ; `version` (injectée
   par GitVersion, jamais écrite à la main) ; `sdk` (voir la décision 8) ; `dotnet.assembly` (le fichier
   à charger, facultatif) ; `ui.module` (chemin du module d'interface sous le dossier `ui/`,
   facultatif) ; `provides` (les points d'extension fournis). Un plugin a au moins une des deux parties :
   un plugin purement d'interface (une vue) est possible. L'hôte **valide** contre `provides` ce que le
   plugin enregistre, côté .NET et côté interface : une inscription non déclarée est refusée et
   journalisée.
2. **Chargement .NET** : `Voyager.PluginHost` lit les sous-dossiers du dossier des plugins, valide chaque
   manifeste et charge chaque partie .NET dans **son** `AssemblyLoadContext`. Les assemblies **partagées
   avec l'hôte** sont `Voyager.Sdk`, `Voyager.Core`, NodaTime et les deux abstractions Microsoft du SDK :
   le contexte du plugin les laisse résoudre par le contexte par défaut (sinon un même type aurait deux
   identités et un transtypage échouerait), et un plugin se compile en les excluant de sa sortie. Un test
   d'intégration vérifie ce partage.
3. **Le plugin enregistre, il ne remplace pas.** La classe d'entrée implémente `IPlugin` et reçoit un
   `IPluginBuilder` du SDK. Ses propres services vont dans un conteneur **privé du plugin**, construit
   (`ValidateOnBuild`, `ValidateScopes`) séparément de celui de l'hôte et de celui des autres plugins ; le
   journal lui est fourni. Ses implémentations de points d'extension passent par des méthodes typées
   (`builder.AddDiagnosticFactSource<T>()`) : l'hôte les range lui-même dans son registre d'extensions.
   Un plugin ne peut donc ni remplacer une inscription de l'hôte, ni faire échouer la construction du
   conteneur de l'hôte.
4. **Un plugin en échec ne coûte rien aux autres** : manifeste invalide, SDK incompatible, conteneur
   invalide ou exception au chargement, le plugin est écarté, la raison est journalisée et visible, et
   l'application démarre. À l'appel, l'hôte entoure chaque appel à un plugin d'un `try/catch` avec une
   limite de durée : une exception ou un plugin trop lent est journalisé, marqué en erreur, et sa
   contribution est omise. L'interface en fait autant d'un module qui ne se charge pas ou plante au rendu
   (frontière d'erreur).
5. **SDK .NET (Q21)** : `Voyager.Sdk` référence `Voyager.Core`, plus **deux paquets uniquement** :
   `Microsoft.Extensions.DependencyInjection.Abstractions` et
   `Microsoft.Extensions.Logging.Abstractions`. Il réexpose de `Voyager.Core` **ses objets-valeurs et ses
   types partagés** (`Voyager.Core.Shared` : argent, identifiants…), jamais ses cas d'usage. Les tests
   d'architecture sont modifiés ainsi : la liste blanche de paquets de `Voyager.Sdk`
   (`PackageReferenceTests`) reçoit ces deux paquets, et l'interdiction de `Microsoft.Extensions`
   (`TypeDependencyTests`) devient une liste d'espaces de noms précis, ces deux-là exceptés ; un
   **nouveau test sur les assemblies des plugins** vérifie qu'ils ne dépendent que de `Voyager.Sdk`, de
   `Voyager.Core.Shared`, de NodaTime, des deux abstractions et de la bibliothèque standard. Il
   remplace le contrôle des seules références de projet, que la transitivité rend insuffisant.
6. **Interface** : un plugin d'interface importe uniquement `@voyager/plugin-sdk`, `react`,
   `react/jsx-runtime`, `react-dom`, `react-dom/client`, `@fluentui/react-components` et `@fluent/react`
   (une **liste blanche** : la règle de lint la fait respecter, et la convention TypeScript est mise à
   jour). Le SDK d'interface fournit le registre d'extensions, les services de l'hôte et le client du
   service local ; le plugin déclare ses inscriptions dans `register(registry)`, que l'hôte valide contre
   `provides`.
7. **Chargement de l'interface et CSP** : le service local liste les plugins (`voyager.pluginhost.v1`,
   nouveau paquet : `voyager.plugins.<id>.v1` est réservé aux plugins). L'interface importe le module
   déclaré par chaque manifeste, servi par le protocole `voyager://app` sous `plugins/<id>/ui/` depuis une
   seconde racine (identifiant validé, seul `ui/` est lisible : ni `plugin.json` ni les assemblies), avec
   la même résolution sûre de chemins. L'*import map* est un script en ligne de la page, et Chromium
   n'accepte pas d'*import map* externe : **la CSP devient `script-src 'self' 'sha256-<empreinte>'`**.
   Le processus principal calcule l'empreinte au lancement, en lisant l'`index.html` livré. Seule la
   page de l'interface peut le faire. En développement, la page est servie par Vite : le chemin des
   plugins d'interface y reste à régler dans la spec (question Q25).
8. **Version et compatibilité** : le SDK et les plugins suivent SemVer et démarrent en `0.1.0`. Le
   manifeste dit `"sdk": "0.1"` : « écrit pour le SDK 0.1 ». Un SDK d'**une même majeure et d'une mineure
   supérieure ou égale** l'accepte ; tant que la majeure est `0`, il faut la **même mineure** (en `0.x`,
   toute mineure peut rompre). C'est une règle de quelques lignes, sans bibliothèque de plages de
   versions. Le calcul de version par composant (`sdk/v*`, `plugins/<id>/v*`,
   [ADR-0007](0007-git-flow-et-versions-gitversion.md)) est mis en œuvre à l'étape 3.
9. **Distribution (étape 3)** : les plugins sont **livrés avec l'application**, dans
   `resources/plugins/<id>/` ([ADR-0009](0009-installation-et-mises-a-jour-velopack.md), décision 7) ; un
   plugin se met donc à jour **avec** l'application. Un script assemble chaque plugin (assembly .NET,
   module d'interface construit, manifeste, textes) dans `artifacts/plugins/<id>/` ; `tools/release.ps1`
   le copie dans le paquet (`extraResource`), et Aspire le donne en développement. Le dossier des plugins
   est posé par le processus principal dans l'environnement du service (comme `Voyager__LogDirectory`),
   jamais lu de l'environnement de la machine : charger des assemblies d'un dossier configurable par
   l'utilisateur est le risque que l'[ADR-0014](0014-ecran-de-demarrage-et-mise-a-jour-au-demarrage.md)
   écarte pour la source des mises à jour. Il n'y a pas de dossier de plugins de l'utilisateur.
10. **Confiance (Q11)** : **aucune isolation** et pas de signature à cette étape. Les plugins sont écrits
    par l'équipe et partent dans le paquet. C'est la limite déjà acceptée par
    [ADR-0003](0003-choix-de-la-stack.md) ; elle sera revue **avant** d'accepter un plugin de tiers
    (option B du .NET, signature, installation séparée), dans un ADR de cette étape-là.
11. **Textes** : le cœur ne traduit pas ([ADR-0008](0008-globalisation-fluent-et-linguini.md)). Un plugin
    livre ses `.ftl` français et anglais (`i18n/<langue>/*.ftl`, identifiants préfixés par son `id`) ; ses
    contrats renvoient des identifiants et des valeurs, pas des phrases ; l'interface charge ses messages
    dans la localisation de l'hôte. Le test de parité français / anglais s'étend aux plugins.
12. **La preuve (étape 3)** : un point d'extension **technique**, la *source de faits de diagnostic*
    (un plugin fournit des faits sur le service et, s'il le souhaite, un panneau pour la page
    « À propos »), avec **deux plugins** de démonstration, dont un apporte son propre panneau React. Le
    service lui-même est une source de plus (les faits de « À propos » d'aujourd'hui), pour que le point
    d'extension serve vraiment après la preuve ; les deux démonstrations sont ensuite remplacées par les
    premiers vrais plugins. Un plugin **cassé** existe seulement dans les tests, jamais dans le paquet.
    La preuve est réussie quand : les deux plugins se chargent depuis le paquet installé ; le plugin
    cassé est écarté sans gêner les autres ; React n'est présent qu'une fois dans la page ; le test sur
    les assemblies des plugins est vert. Les noms (source de faits, registre d'extensions) entrent au
    glossaire avec la spec, avec un nom de code qui évite `System.Diagnostics.DiagnosticSource`.

## Conséquences

- Positif : le mécanisme est essayé de bout en bout (manifeste, .NET, interface, paquet, versions) avant
  que le métier en dépende ; un plugin ne peut pas casser l'hôte au chargement ni remplacer ses
  services ; aucune bibliothèque de plus hors les deux paquets d'abstractions ; les plugins s'écrivent
  comme du .NET et du React ordinaires.
- Négatif : un plugin qui plante ou boucle **après** son chargement emporte quand même le service local ;
  le module d'un plugin s'exécute avec les droits de l'interface (il parle au service local avec son
  jeton et aux canaux IPC de la fenêtre) ; un plugin ajouté n'apparaît qu'au lancement suivant (choix de l'utilisateur : redémarrer suffit), sans
  rechargement à chaud ; la CSP s'ouvre à un
  script en ligne connu par son empreinte ; l'hôte perd l'élagage de Fluent UI ; chaque plugin doit être
  construit avec les bonnes « externes » et sans les assemblies partagées.
- **Hors périmètre, décidé plus tard** : installation par l'utilisateur et plugins de tiers ; signature ;
  déchargement et rechargement à chaud (les registres de l'hôte, côté .NET et côté interface, acceptent
  pourtant des inscriptions à tout moment, pour qu'un ajout à chaud puisse venir sans les refaire) ; services gRPC propres à un plugin (option B) ; données d'un
  plugin en base ([ADR-0004](0004-stockage-des-donnees.md)) ; chargement sur le serveur du groupe (le même
  `Voyager.PluginHost` y servira) ; désactivation d'un plugin qui fait planter le service en boucle (le
  superviseur le relance, question Q26).
- À faire ensuite (étape 3, détaillé au backlog) : une spec de la preuve, puis le SDK .NET et d'interface,
  `Voyager.PluginHost`, le chargement côté interface et côté protocole, les deux plugins, l'assemblage
  dans le paquet, le calcul de version par composant, la règle de lint, les tests d'architecture et le
  skill `/new-plugin`. La convention TypeScript (liste blanche des imports d'un plugin) et les schémas
  (installation des plugins avec l'application) sont mis à jour avec l'implémentation.
