# Spec — La bascule vers le client-serveur

- **Statut** : Validée
- **Épic** : E0 — Cadrage (squelette), avec E9 — Plateforme modulaire
- **Date** : 2026-11-04

## Objectif

Faire rejoindre au code les décisions des ADR [0018](../adr/0018-client-serveur-et-copie-de-voyage.md)
(client-serveur), [0019](../adr/0019-authentification-et-invitations.md) (authentification) et
[0020](../adr/0020-plugins-sur-le-serveur.md) (plugins sur le serveur), **sans rien ajouter de métier** :
l'application de bureau affiche ce que le serveur du groupe lui donne, le serveur charge les plugins, et le
service local disparaît. La page « À propos » sert de preuve, comme pour les étapes précédentes du squelette :
elle montre les mêmes informations qu'aujourd'hui, mais venues du serveur. Après la bascule, on peut construire
la première brique du cœur sur la bonne architecture.

## User stories

- En tant que **développeur**, je veux lancer Voyager avec Aspire et voir dans l'application les informations
  du serveur du groupe, afin de construire la suite sur l'architecture client-serveur.
- En tant que **membre**, je veux que la page « À propos » me dise à quel serveur je suis relié, sa version et
  les plugins qu'il fait tourner, afin de savoir ce qui sert mes données.
- En tant que **membre**, je veux que l'application me dise clairement quand le serveur est injoignable, et
  qu'elle se reconnecte seule quand il revient, afin de ne pas croire mes données perdues.
- En tant qu'**auteur de plugin**, je veux que mon plugin soit chargé par le serveur et que son panneau
  s'affiche dans l'application, afin de vérifier qu'il fonctionne dans la nouvelle architecture.
- En tant que **développeur**, je veux que le code du service local, de SQLite sur le poste et de la gestion des
  plugins côté Electron soit retiré, afin de ne pas entretenir deux architectures.

## Règles métier

### Le serveur

1. Le **serveur du groupe** sert à l'application, en gRPC-Web, les services de la page « À propos » : les faits
   de diagnostic (sa version, celle de .NET, l'état de sa base) et la liste des plugins qu'il a chargés, avec
   leur état.
2. Il **charge les plugins livrés** (les deux plugins de démonstration) au démarrage, avec le mécanisme de
   l'ADR-0015, depuis un chemin fixe : `/app/plugins` dans l'image, `artifacts/plugins/` en développement. Un
   plugin cassé est écarté avec la raison, et le serveur démarre quand même.
3. Les plugins de démonstration fournissent des faits **du serveur** : ce sont des points d'extension sans
   groupe (ADR-0020, décision 2).
4. **Identité de développement** : en développement, le serveur accepte les appels sans connexion, au nom d'un
   compte et d'un groupe de développement fixes, seulement si l'environnement est `Development` **et** qu'un
   réglage propre à Aspire est présent (ADR-0019, décision 9). Dans tout autre cas, il refuse de démarrer avec
   ce réglage. Sans identité de développement ni connexion réelle (qui viendra plus tard), le serveur refuse les
   appels.
5. Le serveur sait déjà dire, à chaque appel, **quelle application l'appelle et dans quelle version**, et refuse
   une application trop ancienne avec un message clair (ADR-0018, décision 6). Au départ, toute version est
   acceptée ; la règle existe pour qu'on puisse la resserrer.

### L'application de bureau

1. L'interface n'appelle plus le service local : elle appelle `voyager://app/api/<serveur>/…`, que le
   **processus principal** transmet au serveur (ADR-0019, décision 4). C'est le chemin interne `/api/` du protocole
   de l'application, plutôt qu'un hôte `voyager://api` à part : la page reste dans sa propre origine, donc sans CORS
   ni ouverture de sa politique de sécurité. La page n'appelle jamais le serveur elle-même et ne tient aucun jeton ;
   elle peut montrer son adresse (« À propos »). Le processus principal refuse un appel d'une origine inconnue, ne
   suit pas les redirections du serveur et n'envoie pas de cookie.
2. **Les serveurs enregistrés** : un utilisateur peut avoir des groupes sur plusieurs serveurs (question Q52),
   donc l'application connaît une **liste** de serveurs, chacun désigné par un identifiant, dès la bascule.
   - En développement, la liste contient le serveur lancé par Aspire. L'application installée lit la liste de
     ses serveurs dans un **réglage, vide par défaut** : le fichier `%LOCALAPPDATA%\Voyager\servers.json`,
     `{ "servers": [ { "id": "famille", "url": "https://…" } ] }`, où un identifiant est en minuscules, chiffres et
     tirets, et une adresse en HTTPS (HTTP seulement sur le poste lui-même), sans identifiant, mot de passe ni
     paramètre. Sans fichier, ou avec un fichier invalide, elle montre « aucun serveur configuré » et le journal dit
     pourquoi. Un essai avec `Setup.exe` se fait contre un serveur de développement, avec l'identité de
     développement.
   - **Chaque serveur a sa propre vue**, isolée (une session Electron par serveur, ADR-0019, décision 4) : les
     modules d'interface d'un serveur ne peuvent appeler que lui. Quand la liste compte plusieurs serveurs,
     l'application montre un **sélecteur de serveur** ; avec un seul, rien ne change à l'écran.
   - La page ne peut désigner qu'un serveur enregistré, jamais une adresse libre. Ajouter un serveur depuis
     l'application viendra avec la connexion réelle et les invitations.
3. Le processus principal **ne laisse passer que les services permis à la page** : pour cette bascule, ceux de
   la page « À propos ». Tout autre appel est refusé et journalisé.
4. **Les modules d'interface des plugins** viennent du serveur, dans la version qu'il a chargée (ADR-0020,
   décision 8) : le processus principal les télécharge, vérifie leur empreinte, les range sur le poste par
   (serveur, plugin, version) et les sert par `voyager://app`. Les modules livrés avec l'application servent de
   cache quand leur version est la même.
5. **État de la connexion**, par serveur : à la place de l'écran « le service local démarre » et de sa barrière,
   l'application montre l'état de la connexion au serveur affiché : connecté, en cours de connexion, injoignable
   (avec la raison lisible et « Réessayer »), ou application trop ancienne. Quand le serveur revient, elle se
   reconnecte seule et recharge ce qui est affiché. Un serveur injoignable n'empêche pas d'utiliser les autres.
6. **L'écran de démarrage** n'attend plus que la mise à jour de l'application ; la connexion au serveur se
   montre ensuite dans l'application. L'ADR-0014 (proposée) est révisée en ce sens au lot 2.
7. **La page « À propos »** montre, pour le serveur affiché, son adresse, sa version et s'il joint sa base
   PostgreSQL (sans première migration : elle viendra avec la première brique du cœur), puis la version de
   l'application, les plugins du serveur avec leur état et, pour chacun, ses faits ou son panneau, comme
   aujourd'hui.
8. **L'écran « Plugins »** de l'étape 1 de la gestion des plugins est retiré au lot 3 : la gestion passe sur le
   serveur et sera reconstruite avec sa spec (ADR-0020, décision 11). La liste des plugins du serveur reste
   visible dans la page « À propos ».

### Ce qui est retiré

1. Le **service local** (`Voyager.LocalService` et ses tests), son lancement et sa surveillance par Electron,
   son jeton local, sa base SQLite et ce qui la montre.
2. `Voyager.Persistence.Sqlite` et `Voyager.Sync`, et leurs mentions dans les tests d'architecture.
3. Le côté Electron de la gestion des plugins : la boîte de dépôt, la copie de préparation, les demandes
   (`requests.json`), le démarrage de rétablissement, et les messages de l'écran « Plugins ». Le code .NET
   réutilisable (examen d'un paquet, extraction, signature, compatibilité par contrat) reste dans
   `Voyager.PluginHost`, pour la future gestion sur le serveur ; ce qui n'a plus d'usage (l'état en fichiers,
   la trace pour un seul utilisateur) est retiré avec ses tests.
4. Dans le **paquet installable** : le service publié et les plugins livrés côté .NET ne sont plus dans le
   paquet de bureau ; seuls les modules d'interface livrés y restent.
5. La documentation suit : `CLAUDE.md` (état actuel, commandes), les schémas de `docs/schemas/`, les
   conventions, le glossaire (les termes caducs sont retirés), le skill `/new-plugin` et l'agent
   `code-reviewer`.

### Le découpage en lots

Chaque lot se livre seul, par sa propre PR, avec l'application qui fonctionne à la fin du lot.

1. **Lot 1 — Le serveur sert « À propos ».** Le serveur expose les deux services en gRPC-Web, charge les plugins
   livrés, accepte l'identité de développement, et ses tests passent. L'application parle encore au service
   local : rien ne change pour elle.
2. **Lot 2 — L'application parle aux serveurs**, livré en trois PR pour rester court à relire :
   - **2a** : le protocole interne `voyager://app/api`, la liste des serveurs enregistrés (le premier est montré),
     la liste des services permis, l'état de la connexion, la page « À propos » du serveur (avec la version de
     l'application et celle du serveur) ; l'onglet « Plugins » n'est plus montré, puisque la fenêtre ne parle plus
     au service local ;
   - **2b** : une vue isolée par serveur et le sélecteur ;
   - **2c** : les modules d'interface venus du serveur, l'écran de démarrage révisé.

   Le service local tourne encore, mais n'est plus appelé.
3. **Lot 3 — Le retrait.** Le service local, SQLite sur le poste, `Voyager.Sync`, la gestion des plugins côté
   Electron et l'écran « Plugins » sont retirés ; le paquet installable et la documentation suivent.

## Critères d'acceptation

- **Étant donné** Aspire lancé, **quand** j'ouvre la page « À propos », **alors** elle montre l'adresse et la
  version du serveur du groupe, l'état de sa base PostgreSQL, et les deux plugins de démonstration chargés par
  le serveur, avec leurs faits et le panneau de celui qui en a un.
- **Étant donné** un plugin cassé dans les plugins du serveur (la fixture de test), **quand** le serveur
  démarre, **alors** il démarre, le plugin est montré écarté avec la raison, et les autres fonctionnent.
- **Étant donné** deux serveurs enregistrés (par exemple deux serveurs de développement), **quand** j'ouvre
  l'application, **alors** un sélecteur me permet de passer de l'un à l'autre, et la page « À propos » de chacun
  montre ses propres plugins ; **étant donné** un seul serveur, **alors** aucun sélecteur n'apparaît.
- **Étant donné** la vue d'un serveur, **quand** un module d'interface y appelle l'autre serveur, **alors** le
  processus principal refuse l'appel ; un test le vérifie.
- **Étant donné** l'application installée sans serveur configuré, **quand** je la lance, **alors** elle montre
  « aucun serveur configuré ».
- **Étant donné** le serveur arrêté depuis le tableau de bord d'Aspire, **quand** l'application est ouverte,
  **alors** elle montre « serveur injoignable » avec « Réessayer » ; **quand** le serveur redémarre, **alors**
  elle se reconnecte seule et la page « À propos » se recharge.
- **Étant donné** le serveur lancé hors de l'environnement `Development` avec le réglage d'identité de
  développement, **quand** il démarre, **alors** il refuse de démarrer, avec un message clair ; un test le
  vérifie, sur l'image Docker aussi.
- **Étant donné** le serveur sans identité de développement, **quand** l'application l'appelle, **alors** l'appel
  est refusé comme non authentifié, et l'application le montre.
- **Étant donné** la page de l'application, **quand** un script y appelle un service qui n'est pas dans la liste
  permise, ou un serveur non enregistré, **alors** le processus principal refuse l'appel ; un test le vérifie.
- **Étant donné** l'application installée par `Setup.exe` (lot 3), **quand** je la lance, **alors** elle ne
  démarre plus de service local, et le paquet ne contient ni le service ni sa base.
- **Étant donné** le dépôt après le lot 3, **quand** je cherche `LocalService`, `Persistence.Sqlite` ou `Sync`,
  **alors** il n'en reste que des mentions historiques (ADR, backlog) ; build, tests, lint et formatage passent,
  sans avertissement.

## Cas limites

- **Hors-ligne** : l'application de bureau ne fonctionne plus sans le serveur (ADR-0018) ; elle le dit, sans
  rien perdre, puisqu'elle n'a rien à enregistrer.
- **Serveur lent** : un appel qui dépasse sa durée limite est montré comme une erreur passagère, avec
  « Réessayer », pas comme un serveur perdu.
- **Version du serveur différente** de celle de l'application : la page « À propos » montre les deux ; un module
  d'interface d'une version que l'application ne sait pas faire tourner est écarté, avec la raison, et le
  plugin s'affiche sans son panneau.
- **Données personnelles** : aucune ; la page ne montre que des informations techniques. Les journaux ne
  gardent pas l'adresse complète des requêtes transmises par le processus principal au-delà du service appelé.
- **Fichiers du poste** : les modules d'interface téléchargés sont rangés dans le dossier de données de
  l'application, par serveur ; ceux d'une version plus utilisée sont nettoyés.

## Hors périmètre

- La **connexion réelle** (comptes, OpenIddict, navigateur, jetons) : seule l'identité de développement existe
  après la bascule (ADR-0019).
- La **gestion des plugins sur le serveur** (installer, activer pour un groupe, suspendre) : elle a sa spec, à
  réécrire (ADR-0020).
- Les **groupes**, la base de données métier et la première migration PostgreSQL.
- **Ajouter ou retirer un serveur** depuis l'application : la liste vient d'un réglage ; l'ajout viendra avec la
  connexion réelle et les invitations (ADR-0019, décision 6).
- Le mobile et la copie de voyage.
- La mise en ligne du serveur (hébergeur, HTTPS, proxy) : le serveur tourne en développement.

## Questions ouvertes

Aucune : les réponses de l'utilisateur sont reportées dans les règles (le réglage des serveurs, vide par défaut,
et plusieurs serveurs dès la bascule ; l'écran « Plugins » retiré au lot 3 ; l'écran de démarrage qui n'attend
plus que la mise à jour ; la base PostgreSQL du serveur montrée sans migration).
