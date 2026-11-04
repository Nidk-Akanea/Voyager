# ADR-0017 — Contrats versionnés, dépendances entre plugins et compatibilité

- **Statut** : Acceptée
- **Date** : 2026-11-02
- **Remplace** : — (complète [ADR-0002](0002-architecture-modulaire-par-contrats-et-plugins.md), règle 3, et
  **remplace la règle de compatibilité** de la décision 8 de [ADR-0015](0015-mecanisme-de-plugins.md) : la
  version du SDK entier cesse d'être l'unique critère, les contrats versionnés le deviennent ; précise le
  sens du numéro de version du SDK d'[ADR-0007](0007-git-flow-et-versions-gitversion.md) ; répond à la
  question Q27 ; **touche les décisions 6 et 9** de [ADR-0016](0016-installation-et-gestion-des-plugins.md)
  et **la décision 3** de [ADR-0014](0014-ecran-de-demarrage-et-mise-a-jour-au-demarrage.md))
- **Remplacée en partie par** : [ADR-0018](0018-client-serveur-et-copie-de-voyage.md) (décisions 2, 6 et 7, les deux moitiés d'un contrat ne sont plus mises à jour ensemble)
- **Complétée par** : [ADR-0020](0020-plugins-sur-le-serveur.md) (décisions 3 et 7, précisées pour les groupes)

## Contexte

Un plugin est écrit pour **une version du SDK** (`"sdk": "0.1"`), et l'application n'accepte un plugin
que si cette version est celle qu'elle porte : même majeure et, tant que la majeure est `0`, **même
mineure** ([ADR-0015](0015-mecanisme-de-plugins.md), décision 8). C'est simple, mais **grossier** :

- le moindre changement du SDK qui passe à une autre mineure en `0.x` **écarte tous les plugins**, même
  ceux dont le contrat n'a pas bougé ;
- une mise à jour de l'application peut donc rendre tous les plugins installés inutilisables d'un coup ;
- un plugin ne peut dépendre que du SDK : **un plugin ne peut pas en consommer un autre**, ce qui devient
  un vrai besoin, la plupart des plugins devant apporter des données que d'autres utiliseront
  (question Q27). Aujourd'hui le seul point d'extension, la source de faits de diagnostic, n'est consommé que
  par l'hôte : **aucun plugin n'en consomme un autre**, et le cas visé (un plugin de tarifs utilisé par un
  plugin de budget, par exemple) **n'existe pas encore**.

Ce qui existe déjà et va dans le bon sens :

- [ADR-0002](0002-architecture-modulaire-par-contrats-et-plugins.md), règle 3 : « chaque contrat est
  versionné ; une évolution incompatible crée une nouvelle version » ;
- les [conventions gRPC](../conventions/grpc.md) : ajouter est compatible, le reste crée une **nouvelle
  version du paquet** (`v2`) et **l'ancienne reste servie le temps de la transition** ;
- un plugin n'enregistre que ce que son manifeste annonce (`provides`).

L'utilisateur a précisé le fonctionnement voulu :

- **les dépendances entre plugins se font par contrat** ;
- **un plugin est lié à un contrat, et c'est le contrat qui dit s'il est obsolète** : un plugin dont le
  contrat est obsolète ne fonctionne plus après une mise à jour, et on peut l'en **avertir** ;
- **on fait évoluer les contrats avec le motif de l'adaptateur** : l'application a son contrat interne
  et fournit au plugin le contrat `V1` ; si on le fait évoluer, on crée un `V2` et un adaptateur
  `V1`–`V2`, de sorte que les plugins écrits pour `V1` continuent de fonctionner.

**Vocabulaire.** Trois sens du mot « contrat » coexistent et restent distincts :

- le **contrat publié** : ce qu'un plugin implémente ou consomme, défini dans le SDK ; son identifiant est
  celui du **point d'extension** qu'il décrit (`diagnostic-fact-source`), suivi d'une **version** ;
- le **contrat interne** : un **port de `Voyager.Core`**
  ([ADR-0005](0005-architecture-du-code-hexagonale-par-domaine.md)), que l'application fait évoluer
  librement ;
- le **contrat gRPC** : un `.proto`, qui suit sa propre numérotation.

Contraintes à respecter : un plugin ne dépend que du SDK, jamais d'un autre plugin ni de `Voyager.Core`
([ADR-0015](0015-mecanisme-de-plugins.md), décision 5), pour que chaque plugin garde **son** contexte de
chargement ; pas de point d'extension sans deux implémentations connues ou prévues
([ADR-0002](0002-architecture-modulaire-par-contrats-et-plugins.md), règle 4) ; `Voyager.PluginHost` ne
dépend que de `Voyager.Sdk` (organisation du code).

## Options

### Comment un plugin dépend d'un autre

**Option A — Pas de dépendance** : une notion partagée entre plugins entre dans le cœur ou le SDK sous
forme de contrat (la réponse provisoire de la question Q27).

- Avantages : aucun nouveau mécanisme ; chaque plugin reste seul.
- Inconvénients : le SDK grossit de tout ce que deux plugins veulent partager ; un plugin de données ne
  peut pas être consommé par un autre sans passer par le projet.

**Option B — Dépendre d'un plugin précis**, par son identifiant et une plage de versions (comme un paquet
npm ou NuGet).

- Avantages : familier ; simple à exprimer (`requires: lodging >= 1.2`).
- Inconvénients : un plugin se lie à **l'implémentation** d'un autre, qui ne peut plus être remplacé par
  un autre plugin qui fait la même chose ; il faut résoudre des plages de versions entre plugins.

**Option C — Dépendre d'un contrat** : un plugin **fournit** des contrats et en **requiert**, avec une
version ; n'importe quel plugin (ou l'hôte) qui fournit le contrat convient. **C'est le choix de
l'utilisateur.**

- Avantages : le plugin fournisseur peut changer ; le contrat est le seul lien ; c'est ce qu'ADR-0002
  demande (des contrats, des implémentations).
- Inconvénients : il faut **définir et publier un contrat** (dans le SDK) avant de s'en servir ; il faut
  dire ce qui se passe quand plusieurs plugins fournissent le même contrat, ou aucun ; une résolution de
  versions reste nécessaire (entre contrats, pas entre plugins).

(Dans les options B et C, les types échangés traversent les contextes de chargement par le SDK : c'est le
même mécanisme, il ne départage pas les deux.)

### Comment un contrat évolue

**Option A — La version du SDK entier est le critère** (ADR-0015 aujourd'hui).

- Avantages : une seule règle de quelques lignes.
- Inconvénients : le grief du contexte (tout est écarté au moindre changement en `0.x`).

**Option B — Une version par contrat, sans rien conserver** : on ne garde que la dernière ; un plugin écrit
pour une version plus ancienne est écarté.

- Avantages : aucun code ancien à maintenir ; la casse est limitée au contrat qui change, ce qui est déjà
  un net progrès sur l'option A.
- Inconvénients : les auteurs doivent suivre le rythme de l'application, or un plugin **installé depuis un
  fichier** n'a pas forcément de mainteneur : chaque évolution d'un contrat le casse définitivement.

**Option C — Une version par contrat, et l'application garde les anciennes versions par des adaptateurs**
(le motif de l'adaptateur) pendant une période annoncée. **C'est le choix de l'utilisateur.**

- Avantages : un plugin écrit pour `V1` continue de fonctionner quand `V2` paraît ; l'application garde un
  contrat interne libre d'évoluer ; l'obsolescence est une décision **par contrat**, annoncée ; même logique
  que les paquets gRPC (`v1` reste servi le temps de la transition).
- Inconvénients : chaque version conservée est du code à maintenir et à tester ; certains changements de
  sens ne s'adaptent pas (on retire alors la version).

**Option D — Une version par contrat, et l'hôte sert plusieurs versions côte à côte, sans adaptateur**
(chaque version a sa propre implémentation dans l'hôte), ou un consommateur qui accepte plusieurs versions
(`versions: [1, 2]`) et choisit à l'exécution.

- Avantages : aucune façade à écrire ; c'est ce que fait la convention gRPC.
- Inconvénients : la logique est **dupliquée** dans chaque version servie (au lieu d'un contrat interne
  unique) ; un consommateur qui accepte plusieurs versions doit les gérer toutes.

## Décision

**C et C** : les dépendances se font **par contrat**, et les contrats évoluent **par versions et adaptateurs**.
Le **principe** est décidé maintenant ; **la mise en œuvre est découpée** (voir la décision 8) pour ne pas
construire un mécanisme avant d'en avoir besoin.

1. **Un contrat publié est l'unité de compatibilité.** Il a l'identifiant de son point d'extension et une
   **version entière** (`1`, `2`…), indépendante de la version du paquet gRPC éventuel (même convention de
   nommage, **numéros indépendants** : un contrat `V2` ne force pas un `.proto` `v2`). Dans une version, on
   ne fait que des **ajouts compatibles à la lecture binaire** (côté C# : méthodes par défaut d'interface,
   nouveaux types, valeurs facultatives, aucune valeur d'énumération sans traitement du cas inconnu, pas de
   champ ajouté à un `record` positionnel) ; tout le reste crée une **nouvelle version**
   ([ADR-0002](0002-architecture-modulaire-par-contrats-et-plugins.md), règle 3). Les types d'un contrat
   sont dans le SDK. La liste précise des ajouts permis est une convention à écrire. Un contrat ne peut
   exposer **aucun type défini par un plugin** : seuls traversent les contextes de chargement les types du
   SDK, de `Voyager.Core.Shared` et de NodaTime. Les types de `Voyager.Core.Shared` font **partie de la
   surface de chaque contrat qui les expose** : les changer rompt **tous** ces contrats (question Q29).
2. **Le manifeste dit ce qu'un plugin fournit et ce qu'il requiert**, avec la version :
   `provides: [{contract, version}]` et `requires: [{contract, version}]`. Le **contrat de base** d'un plugin
   (`IPlugin`, `IPluginBuilder`) est un contrat comme les autres, d'identifiant `plugin` : le champ `sdk` de
   l'ADR-0015 se déclare désormais ainsi, par une **version entière** (la forme exacte du manifeste revient à
   la spec). La règle de l'ADR-0015 (même majeure, même mineure en `0.x`) est **remplacée** par : un contrat
   est accepté si l'application le **prend en charge** à cette version, directement ou par un adaptateur.
   Un contrat est **une seule chose à deux moitiés**, le service .NET et l'écran d'interface
   (`@voyager/plugin-sdk`) : mêmes règles, **même numéro de version**, versionnées ensemble (choix de
   l'utilisateur). Les adaptateurs de la moitié d'interface suivent les mêmes principes ; leur forme revient
   à la spec. **Le numéro de version du SDK** (ADR-0007) continue de se calculer et prend un sens : une
   **majeure** quand une version de contrat est **retirée**, une **mineure** quand un contrat ou une version
   de contrat est **ajouté**, un correctif sinon ; il n'est plus le critère de compatibilité d'un plugin.
3. **Une dépendance est une exigence de contrat.** Tout `requires` est **obligatoire** (choix de
   l'utilisateur : pas de dépendance facultative au départ, à ajouter au premier plugin qui en a besoin).
   - **Résolution.** Un plugin est chargé **après** ceux dont il requiert un contrat (ordre topologique,
     déterministe : à égalité, l'ordre des identifiants), et son conteneur privé reçoit les services requis
     **avant** `ValidateOnBuild`, sous forme de collections du registre de l'hôte : il ne référence jamais
     l'assembly d'un autre plugin. Il est **écarté** quand un contrat requis n'est fourni par personne
     (absent, désactivé, écarté lui-même, ou fourni dans une version que l'application ne prend pas en
     charge), et cet écartement **se propage** en cascade ; la raison est une **valeur** (« prérequis non
     fourni », « contrat non pris en charge », « contrat obsolète », « dépendance circulaire ») avec le contrat
     et la version en paramètres, que l'interface traduit : l'hôte ne porte pas de texte.
   - **Cycles.** On détecte les cycles **entre plugins** (de toute longueur), pas entre contrats ; un plugin
     qui fournit et requiert le même contrat ne se reçoit pas lui-même ; les plugins d'un cycle sont tous
     écartés pour « dépendance circulaire ».
   - **Hôte.** L'hôte peut fournir un contrat (l'identifiant réservé du service local) : il est toujours
     présent et ne dépend d'aucune activation de plugin.
   - **Fournisseurs multiples.** Plusieurs plugins peuvent fournir le même contrat et le
     consommateur les reçoit tous (choix de l'utilisateur) ; un fournisseur en échec est ignoré sans gêner
     les autres. Un contrat qui n'admet qu'**un seul** fournisseur se déclare ainsi dans le SDK, et
     l'utilisateur choisit dans l'écran « Plugins » lequel est actif.
4. **Appariement des versions et adaptateurs.** Le **contrat interne** de l'application est un port de
   `Voyager.Core` (ADR-0005) ; chaque version publiée d'un contrat est une **façade stable**. Les versions
   prises en charge s'adaptent **directement à la version courante**, jamais les unes aux autres (« hub et
   rayons », pas de chaîne `V1` vers `V2` vers `V3`).
   - **Adaptateur descendant** : un **consommateur** de `V1` face à un service courant `V2`. L'hôte lui
     présente une façade `V1`, qui masque ce que `V2` ajoute. **Obligatoire** pour garder la version `V1`.
   - **Adaptateur montant** : un **fournisseur** `V1` face à un hôte qui attend `V2`. L'hôte enveloppe son
     implémentation. Il n'est **pas toujours possible** (`V2` peut exiger ce que `V1` ne calcule pas) ou il
     est dégradé ; s'il manque, un fournisseur `V1` n'est pas pris en charge.
   - **Règle d'appariement** pour un contrat fourni par un plugin et consommé par un autre, **sans port du
     cœur derrière** : la version courante tient lieu de hub. Consommateur `N` et fournisseur `M` :
     `N = M` direct ; `N < M` adaptateur descendant ; `N > M` adaptateur montant ; si l'adaptateur n'existe
     pas, ou si la version est retirée, la dépendance n'est pas satisfaite.
   - **Les adaptateurs vivent hors de `Voyager.PluginHost`**, dans un projet d'adaptation que les hôtes
     assemblent : `Voyager.PluginHost` ne dépend toujours que de `Voyager.Sdk`, alors qu'un adaptateur doit
     connaître le port de `Voyager.Core`.
   - **Un adaptateur est sans état** et recréé à volonté ; il est détruit **avant** le conteneur du plugin
     qu'il enveloppe. Les appels d'un plugin à un autre passent par la façade, qui applique les mêmes
     garde-fous que les appels de l'hôte (exception interceptée, limite de durée,
     [ADR-0015](0015-mecanisme-de-plugins.md), décision 4).
   - **Une suite de tests de conformité** (les tests qui décrivent ce que promet une version d'un contrat)
     tourne contre l'implémentation directe **et** contre l'adaptateur. Pour l'adaptateur montant, elle ne
     prouve que ce que `V1` promettait. Où elle est publiée pour les auteurs de plugins : à décider avec le
     premier contrat.
   - Ne s'adaptent pas : l'identité des objets, les unités et devises, l'ordre et la pagination, le modèle
     d'erreur, la concurrence, les délais, l'échelle de temps, le flux. On retire alors la version, et le
     contrat le dit.
5. **Une version de contrat a un état** : *prise en charge*, *dépréciée* (elle fonctionne, l'application
   avertit qu'elle sera retirée) ou *obsolète* (l'adaptateur a disparu : les plugins qui l'utilisent sont
   écartés pour « contrat obsolète »). **Cela se décide au cas par cas, contrat par contrat** (choix de
   l'utilisateur, question Q36) : quand l'adaptateur est possible, la version est conservée aussi longtemps
   que le projet le décide, et dépréciée avant d'être retirée ; quand il n'est pas possible (`V2` demande une
   information que l'adaptateur ne peut pas fournir et que `V1` ne fournit pas, ce qui ferait mal
   fonctionner le plugin), la version devient **obsolète dès la parution de `V2`**, sans période de
   dépréciation. Aucun nombre de versions ni durée n'est fixé. Où l'état est déclaré (le SDK, un fichier de
   l'hôte) revient à la spec.
6. **L'utilisateur est averti.** Dans l'écran « Plugins » (un plugin qui utilise une version dépréciée ou
   obsolète) et **à l'examen d'un paquet** (une version non prise en charge est refusée, une version
   dépréciée est signalée), selon le détail de la spec. **Avant d'appliquer une mise à jour de
   l'application**, elle **contrôle** si des plugins installés seraient écartés (un contrat qu'ils utilisent
   devient obsolète) et **informe** l'utilisateur : s'il **accepte**, la mise à jour s'applique ; s'il
   **refuse**, l'application se lance dans la version où elle est (choix de l'utilisateur, question Q37). Cela
   amende l'ADR-0014 (proposée), dont la mise à jour au démarrage est aujourd'hui imposée : elle ne devient
   un choix que **quand des plugins sont touchés**. Pour un plugin **livré**, l'obsolescence est un problème
   du projet que l'utilisateur ne peut pas résoudre : l'avertissement vise les plugins installés. Reste à
   décider (Q37) : **qui compare** et comment la nouvelle version publie les contrats qu'elle prend en charge
   (une liste jointe à la release, ou démarrer la nouvelle version en mode vérification pour réutiliser la
   même règle sans l'écrire deux fois), et ce qui arrive quand l'utilisateur refuse à chaque démarrage.
   **Ce que ce contrôle prouve, et ce qu'il ne prouve pas** : il dit si un plugin **serait accepté** (contrat
   pris en charge, manifeste valide et, en mode vérification, chargement sans échec) ; il ne prouve pas que
   son **comportement** reste correct, ce que garantissent les suites de conformité (décision 4). Pour
   cela, la CI peut charger dans chaque nouvelle version les paquets de plugins publiés précédemment.
7. **Effets sur les ADR voisins.** Dans l'ADR-0016 : la décision 9 (« version du SDK incompatible »)
   devient « contrat non pris en charge » ; la décision 6 (plantages) change : réactiver un plugin impose de
   réactiver ceux dont il dépend (« un à un » devient « par fermeture de dépendances ») et un plugin dont le
   prérequis est suspendu apparaît **écarté**, pas suspendu ; le marqueur ne désigne que le plugin que
   **l'hôte** appelle, et un plantage qui survient chez un fournisseur pendant l'appel d'un consommateur
   reste attribué à l'appelé direct (la méthode un à un reste le filet). Dans l'ADR-0014, la décision 3 (mise à jour sans choix) gagne
   l'étape de contrôle de la décision 6.
8. **Ce qu'on fait maintenant, et ce qu'on reporte.** Maintenant : les **versions par contrat** et le
   **nouveau critère de compatibilité** (décisions 1, 2 et 5), qui remplacent la version du SDK sans exiger
   ni adaptateur ni dépendance. Plus tard, **au premier plugin consommateur d'un autre ou à la première
   évolution réelle d'un contrat** : `requires`, l'ordre de chargement, les cycles et les adaptateurs
   (décisions 3 et 4). On ne construit pas d'adaptateur pour un contrat qui n'a pas de `V2`.

## Conséquences

- Positif : une mise à jour de l'application n'écarte plus tous les plugins mais seulement ceux dont un
  contrat est retiré ; un plugin peut en consommer un autre sans le référencer, donc sans toucher à
  l'isolation des contextes de chargement ; un plugin fournisseur peut être remplacé par un autre qui remplit
  le même contrat ; l'application garde un contrat interne libre ; le numéro de version du SDK porte
  enfin une information.
- Négatif : **chaque version conservée coûte** du code d'adaptation, une suite de tests et de la vigilance ;
  il faut de la **discipline** (des ajouts seulement, un contrat publié avant d'être utilisé) ; l'adaptateur
  montant est souvent impossible ou dégradé ; la surface de chaque contrat comprend les types de
  `Voyager.Core.Shared` (Q29) ; le manifeste change ; un contrat mal conçu coûte cher à faire évoluer ;
  **sans premier cas réel**, la partie reportée (décisions 3 et 4) reste une hypothèse.
- À faire ensuite, **maintenant** : écrire la convention des ajouts permis en C# ; passer le manifeste à
  des contrats versionnés et migrer le critère : `PluginManifestReader`, `SdkCompatibility` (qui devient la
  prise en charge d'un contrat), `PluginCatalog.Load` (qui ne reçoit plus une version du SDK mais les
  contrats pris en charge), `PluginRejectionReason` et son journal ; **le contrat gRPC**
  `proto/voyager/pluginhost/v1/pluginhost.proto` : la valeur `SDK_INCOMPATIBLE = 3` est **conservée** (jamais
  renumérotée) et de nouvelles valeurs sont ajoutées (`buf breaking` s'applique) ; côté interface,
  `PluginCard.tsx` et les textes français et anglais ; les deux plugins de démonstration ; le skill
  `/new-plugin` et ses modèles ; `tools/test-versions.ps1` et les tests concernés ; les commentaires du SDK ;
  l'**ADR-0014** (une étape de contrôle avant d'appliquer la mise à jour) et l'écran de mise à jour ;
  le glossaire et le schéma technique ; la spec de la gestion des plugins (la raison d'écartement, les
  dépendances à prévenir à la désactivation, à la désinstallation et à l'installation, la cascade
  d'écartement avec la liste des dépendants montrée avant la confirmation). **Plus tard**, au premier
  besoin : `requires`, l'ordre, les cycles, les adaptateurs et leur projet.
- Questions du backlog : Q27 (dépendances), Q36 (combien de versions de contrat garder), Q38 (un ou
  plusieurs fournisseurs), Q39 (dépendance facultative) et Q40 (contrats d'interface) sont **tranchées dans
  cette direction** ; Q37 (avertir avant une mise à jour) l'est dans son principe, qui compare et où la
  liste est publiée restant ouverts.
