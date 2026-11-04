# ADR-0018 — Client-serveur, et une copie de voyage sur le mobile

- **Statut** : Acceptée
- **Date** : 2026-11-04
- **Remplace** : [ADR-0004](0004-stockage-des-donnees.md) (stockage des données), dont il reprend les règles
  encore valables (décisions 7 et 8 ci-dessous)
- **Remplace en partie** :
  - [ADR-0003](0003-choix-de-la-stack.md) : le service local, sa base et la synchronisation (architecture,
    point 1) ;
  - [ADR-0006](0006-dependances-principales.md) : `Microsoft.EntityFrameworkCore.Sqlite` sur le poste ;
  - [ADR-0009](0009-installation-et-mises-a-jour-velopack.md) : le service local publié et mis à jour avec
    l'application (décision 6), les plugins livrés avec l'application (décision 7) ;
  - [ADR-0010](0010-hebergement-serveur-heberge-multi-groupes.md) : la décision 4, le mode solo sans serveur ;
  - [ADR-0011](0011-aspire-pour-le-developpement.md) : le service local lancé par Aspire ;
  - [ADR-0012](0012-journaux-de-developpement-dans-seq-avec-serilog.md) : la décision 6, l'application et son
    service local ;
  - [ADR-0013](0013-assemblage-du-paquet-de-l-application-de-bureau.md) : le service local publié dans le
    paquet ;
  - [ADR-0015](0015-mecanisme-de-plugins.md) : le chargement par le service local (décision 2), le
    chargement de l'interface (décision 7), la distribution avec l'application (décision 9) ;
  - [ADR-0016](0016-installation-et-gestion-des-plugins.md) : les plugins installés et gérés sur le poste
    (décisions 1 et 3 à 6), et la décision 8, dont il **lève** la règle « aucune partie serveur d'un plugin
    sur un serveur qui accueille plusieurs groupes » (voir la décision 4) ;
  - [ADR-0017](0017-contrats-versionnes-et-dependances-entre-plugins.md) : les deux moitiés d'un contrat, qui
    ne sont plus mises à jour ensemble (décision 2), l'avertissement avant une mise à jour de l'application et
    ses effets sur les ADR voisins (décisions 6 et 7) ;
  - l'[ADR-0014](0014-ecran-de-demarrage-et-mise-a-jour-au-demarrage.md), encore proposé, sera révisé
    directement (il démarre et surveille le service local).
- **Complétée par** : [ADR-0019](0019-authentification-et-invitations.md) (le mécanisme du jeton et le chemin des requêtes)

## Contexte

Les ADR 0003 et 0004 ont choisi une architecture **« d'abord sur le poste »** : chaque membre a sur son PC un
**service local** .NET, qui fait les calculs, charge les plugins et garde une **copie complète** de ses
voyages dans SQLite ; il la **synchronise** avec le serveur du groupe (PostgreSQL). L'ADR-0004 le reconnaît :
cette synchronisation est « le point le plus délicat de l'architecture ». Deux membres qui modifient le même
devis chacun de leur côté créent un conflit, qu'il faut résoudre champ par champ, pour chaque type de donnée,
y compris celles des plugins.

L'utilisateur revoit ce choix :

- **Le hors-ligne ne sert que pendant le voyage.** On prépare un voyage avec du réseau ; c'est sur place
  (train, avion, montagne, étranger sans forfait) qu'on en manque, et on a alors son **mobile ou sa
  tablette**, pas son PC. La question Q2 le disait déjà : sans réseau, on doit **consulter le programme et
  saisir les dépenses**.
- **Tout le monde travaille sur les mêmes données**, comme dans une application client-serveur classique :
  l'application de bureau **affiche les données du serveur**, sans service local.
- **Les plugins s'installent sur le serveur**, puisque c'est lui qui fait le travail.
- **La copie du voyage** part du serveur vers le **mobile ou la tablette**.

Une nuance sur un argument avancé, « SQLite est plus lent que PostgreSQL sur les gros volumes » : les données
d'un voyage tiennent en quelques milliers de lignes, et à ce volume SQLite, qui tourne dans l'application, est
souvent plus rapide. Le gros volume existe, mais dans les **données de référence** (les horaires d'une grande
ville au format GTFS font des millions de lignes). La conclusion tient pour une autre raison : **la
collaboration**, et la synchronisation qu'elle impose dès que chacun a sa copie.

Ce qui oblige à décider maintenant : la première brique du cœur, [le voyage et ses
voyageurs](../specs/projet-voyage-et-voyageurs.md), est validée et prête à être construite, avec une règle
« tout se fait sur le poste, sans réseau » qui vient de l'architecture actuelle. La construire dans SQLite,
puis la déplacer, coûterait une migration et une synchronisation inutiles.

Contraintes qui ne changent pas :

- **Aucun calcul métier hors de `Voyager.Core`** ([ADR-0005](0005-architecture-du-code-hexagonale-par-domaine.md)) ;
  l'interface ne fait que saisir et afficher.
- **Les fonctionnalités de base sont des plugins**, y compris les vues
  ([ADR-0002](0002-architecture-modulaire-par-contrats-et-plugins.md)).
- **Un serveur hébergé accueille plusieurs groupes**, et chacun ne voit que ses données (ADR-0010).
- **Le chargement d'un plugin .NET ne l'isole pas** (ADR-0015) : il s'exécute dans le processus qui le
  charge, avec tous ses droits.
- **Montants** en décimal avec leur devise, **instants** en UTC avec le fuseau du lieu, **dates de
  calendrier** sans heure, coordonnées en WGS 84.
- **Données personnelles** : les voyageurs (nom, date de naissance, avatar) en sont.

## Options

### L'architecture

#### Option A — D'abord sur le poste, synchronisation complète (l'architecture actuelle)

- Avantages : tout fonctionne sans réseau, préparation comprise ; usage seul sans compte ni serveur ; chaque
  poste garde de fait une sauvegarde de ses voyages ; une synchronisation qui pousse les changements les
  montre presque aussitôt aux autres membres.
- Inconvénients : **la synchronisation générale** (fusions par type de donnée, données des plugins,
  suppressions), le travail le plus long et le plus risqué du projet, pour un besoin, préparer hors-ligne, que
  l'utilisateur n'a pas ; deux modèles complets à tenir (SQLite et PostgreSQL) ; les données de référence
  volumineuses copiées sur chaque poste.

#### Option B — Le serveur d'abord, avec un service local sur le poste

Le serveur garde les données ; un service local sur chaque PC fait les calculs avec les plugins, et garde une
copie du voyage pour partir.

- Avantages : aucun plugin ne tourne sur le serveur hébergé, donc pas de question d'isolation ; calculs
  possibles hors-ligne sur le PC ; les plugins du groupe (ADR-0016, décision 8) alignent les postes.
- Inconvénients : le serveur ne peut pas contrôler les données propres à un plugin qu'il ne fait pas tourner ;
  deux séries de contrats (interface → service local → serveur) ; un service à installer, lancer et
  surveiller sur chaque PC ; deux postes aux versions de plugins différentes peuvent encore calculer
  différemment ; et la copie de voyage sur le PC, alors qu'on part avec son mobile.

#### Option C — Client-serveur classique, copie de voyage sur le mobile

L'application de bureau affiche et saisit les données du serveur. Le serveur exécute le cœur et les plugins.
Pour partir, on emporte une copie du voyage sur son mobile ou sa tablette.

- Avantages :
  - **une seule vérité, un seul calcul** : tous les membres voient les mêmes données et les mêmes totaux,
    calculés une fois, au même endroit, avec les mêmes plugins ;
  - **plus de synchronisation générale** : la copie de voyage descend du serveur, et seuls des **ajouts**
    remontent ;
  - **un seul modèle**, dans PostgreSQL, et une seule série de contrats entre les clients et le serveur ;
  - plus de service local à installer, lancer et surveiller sur chaque PC ;
  - les données de référence volumineuses et la recherche géographique (PostGIS) restent sur le serveur.
- Inconvénients :
  - **sans réseau, l'application de bureau ne sert à rien** ;
  - **les plugins tournent sur le serveur** : sur un serveur qui accueille plusieurs groupes, un plugin voit
    les données de tous, et un plugin qui plante arrête le serveur pour tous ;
  - **l'usage seul demande un compte** sur un serveur, donc l'authentification et la mise en ligne avant
    toute version utilisable, et un coût d'hébergement même pour un utilisateur seul ;
  - **les données personnelles des voyageurs vont sur le serveur**, et le serveur devient la seule copie :
    RGPD, sauvegardes et export deviennent indispensables ;
  - **l'application de bureau et le serveur se mettent à jour séparément** : il faut gérer leur compatibilité ;
  - **hors-ligne, le mobile ne calcule pas** si son application n'exécute pas le C# du cœur (voir plus bas) ;
  - le côté Electron de l'étape 1 de la gestion des plugins, construit pour le PC, ne sert plus.

#### Option D — Le serveur seulement, sans hors-ligne

Comme C, sans copie de voyage. Le plus simple, mais ne répond pas au besoin du voyage (Q2) : sans réseau,
plus de programme ni de saisie des dépenses, justement là où on en manque.

### Qui installe un plugin sur le serveur hébergé

- **Option 1 — L'hébergeur installe des plugins de confiance ; chaque groupe active ceux qu'il veut.**
  Seuls des plugins du projet entrent sur le serveur hébergé. Simple et sûr tant que les plugins viennent du
  projet ; un groupe ne peut pas apporter son propre plugin.
- **Option 2 — Un serveur par groupe** (un conteneur par groupe). Le groupe y installe ce qu'il veut, sans
  risque pour les autres. Mais l'hébergement coûte un conteneur et une base par groupe, et l'ADR-0010 (un
  serveur pour plusieurs groupes) est remis en cause.
- **Option 3 — Un processus par plugin, isolé, dès maintenant.** La seule réponse qui permettrait des
  plugins de tiers sur un serveur partagé ; mais c'est l'option B de l'ADR-0015, écartée pour son coût (un
  contrat gRPC par point d'extension, un processus à surveiller par plugin).

### Les calculs sur le mobile hors-ligne

- **Option 1 — Une application en technologie web (Capacitor)**, qui reprend les composants React : la
  copie emporte les **résultats déjà calculés** par le serveur ; une dépense ajoutée hors-ligne est montrée
  « à recalculer » jusqu'au retour du réseau. Simple, mais les parts ne bougent pas hors-ligne.
- **Option 2 — La même application, avec `Voyager.Core` compilé en WebAssembly** (.NET dans le navigateur
  de l'application) : le même C# calcule hors-ligne, sans seconde interface. Mais il faut aussi les plugins
  .NET sur le mobile, et le poids et la maturité de .NET WebAssembly sur mobile restent à éprouver.
- **Option 3 — Une application .NET (MAUI)**, qui exécute `Voyager.Core` hors-ligne. Les parts se
  recalculent sur place, mais il faut aussi les plugins sur le mobile, et une seconde interface à écrire.

### L'application de bureau

- **Option 1 — Garder Electron.** Déjà construit (installation, mises à jour, fenêtre, protocole
  `voyager://`) ; c'est l'application de bureau que l'utilisateur voulait (Q1).
- **Option 2 — Une application web dans le navigateur.** Rien à installer, mais plus d'application de
  bureau, et le travail d'installation déjà fait (ADR 0009, 0013, 0014) ne sert plus.

## Décision

**Option C**, choisie par l'utilisateur, avec ces choix secondaires : **l'hébergeur installe des plugins
relus et signés par le projet, chaque groupe active les siens, et un plugin ne voit que les données de son
groupe** (options 1 et 3, choix de l'utilisateur), **une copie qui emporte les résultats calculés** (option 1,
recommandée), et **Electron gardé** (option 1, recommandée).

1. **Le serveur du groupe est la source de vérité et fait le travail.** Voyages, voyageurs, devis, budget et
   données de référence sont dans **PostgreSQL** (dernière version stable) avec **PostGIS**. Le serveur
   exécute **les cas d'usage et les calculs** de `Voyager.Core`, avec les plugins. Chaque donnée appartient à
   un groupe, et le serveur vérifie l'accès à chaque requête (ADR-0010). Il ne fait confiance à aucun client.
   - **Un serveur est une instance de l'application** et gère plusieurs groupes ; un utilisateur peut être
     membre de groupes sur des **serveurs différents**, et l'application de bureau se connecte à chacun
     (question Q52, tranchée).
   - **Un voyageur appartient à l'utilisateur** qui l'a déclaré, et l'utilisateur appartient à des groupes
     (une famille, une bande d'amis) (question Q48, tranchée).
   - **Les avatars** des voyageurs sont enregistrés dans la base du serveur ; les photos du carnet seront
     décidées le moment venu (question Q50, tranchée pour les avatars).
2. **Les identifiants** sont des UUID v7 **créés par le client** (l'application de bureau comme le mobile).
   Une écriture est **idempotente** : renvoyer la même écriture (même identifiant, même auteur, même contenu),
   par exemple après un accusé de réception perdu, réussit sans rien changer ; un identifiant qui existe déjà
   avec un autre contenu, ou dans un autre groupe, est refusé.
3. **L'application de bureau affiche et saisit, sans service local.**
   - L'interface React parle au serveur en **gRPC-Web**, sur HTTPS, **par l'intermédiaire du processus
     principal** d'Electron : il ajoute le jeton de l'utilisateur, qui n'est jamais lisible par la page (les
     modules d'interface des plugins s'y exécutent), et il ne laisse passer que les services permis à la
     page. Une opération d'administration demande en plus une confirmation native ou une nouvelle
     authentification (comme l'ADR-0016, décision 4). Le mécanisme (protocole intercepté, CORS, CSP) est
     décidé avec l'ADR d'authentification.
   - Le serveur pousse les changements des autres membres par **un seul flux** par fenêtre, partagé par tous
     les écrans. La reprise après une coupure, l'expiration du jeton pendant le flux et le passage par le
     proxy HTTPS sont à préciser à la construction.
   - Aucune donnée de voyage n'est enregistrée sur le PC (seuls les réglages, les journaux de l'ADR-0013 et
     les modules d'interface des plugins le sont). Sans réseau, l'application le dit, et rien n'est mis en
     attente.
4. **Les plugins s'exécutent sur le serveur.**
   - La partie .NET d'un plugin est chargée **par le serveur**, avec le mécanisme de l'ADR-0015 (un contexte
     de chargement par plugin) et les contrats de l'ADR-0017.
   - Un plugin est **installé pour tout le serveur** par son administrateur ; **chaque groupe active** ceux
     qu'il veut. Tous les groupes ont donc la même version d'un plugin.
   - **Les plugins livrés** (les fonctionnalités de base) sont dans l'**image du serveur**.
   - Sur le **serveur hébergé**, seuls des plugins **du projet** s'installent : ceux qu'il écrit, et ceux
     qu'on lui propose, qu'il relit et signe (question Q47, tranchée). La règle de l'ADR-0016
     (décision 8), « aucune partie serveur d'un plugin sur un serveur qui accueille plusieurs groupes », est
     **levée pour eux** : du code écrit et signé par le projet a la même confiance que le serveur lui-même.
     Tant que la clé de signature de production n'est pas décidée (question Q31), le serveur hébergé n'a que
     les plugins livrés dans son image.
   - Sur un **serveur auto-hébergé**, l'administrateur installe ce qu'il veut, à ses risques, avec
     l'avertissement de l'ADR-0016.
   - **Un plugin ne voit que les données du groupe qui l'appelle** (question Q47, tranchée). Il n'y accède que
     par les ports du cœur, limités au groupe de l'appel, et ne garde aucun état partagé entre groupes ;
     l'**isolation** de chaque plugin dans son propre processus le garantira, y compris contre une erreur.
     Quand construire cette isolation est à décider dans l'ADR des plugins sur le serveur ; des plugins **de
     tiers** non relus par le projet l'attendent dans tous les cas (question Q11).
   - L'accord des membres prévu par l'ADR-0016 (décision 8) disparaît : rien ne s'installe plus sur leur
     poste. Seul le choix du groupe d'activer un plugin compte.
   - Un plugin qui plante ou boucle est **désactivé pour le seul groupe en cause**, pas pour tout le serveur
     (question Q51, tranchée). Pour que son plantage n'arrête pas le serveur, et pour le repérer, les pistes
     sont : les calculs dans un processus séparé du serveur, une durée limite par appel, et un suspect retenu
     lors de deux plantages de suite ; elles seront détaillées dans l'ADR des plugins sur le serveur.
   - Le détail (installation, activation par groupe, mises à jour, données propres à un plugin, en JSONB ou
     dans ses tables, écrans d'administration) fera l'objet d'un **ADR des plugins sur le serveur**.
5. **Les modules d'interface des plugins.** Ceux des plugins livrés sont dans le paquet de l'application ;
   ceux des plugins installés sont téléchargés depuis le serveur par le **processus principal**, qui vérifie
   leur empreinte et leur signature, les range sur le PC **par serveur**, et les sert par `voyager://app`. La
   page ne charge donc aucun script qui n'est pas passé par cette vérification : c'est la signature qui
   protège. Un module **non signé** par le projet (serveur auto-hébergé) ne se charge qu'après l'**accord
   explicite** de l'utilisateur, dans une boîte native, pour ce serveur et cette version du plugin.
6. **La compatibilité entre l'application et le serveur.** Ils se mettent à jour à des moments différents
   (Velopack d'un côté, l'hébergeur de l'autre).
   - L'API gRPC du serveur est versionnée par paquet (`v1`, `v2`…, voir `docs/conventions/grpc.md`) ; le
     serveur sert la version courante et la précédente.
   - L'application annonce sa version ; une application **trop ancienne** est refusée avec un message clair
     (« mettez Voyager à jour ») ; une application **trop récente** pour le serveur le dit aussi (« ce serveur
     doit être mis à jour »).
   - Un module d'interface déclare la version de `@voyager/plugin-sdk` qu'il attend ; l'application ne charge
     pas un module qu'elle ne sait pas faire tourner, et le plugin s'affiche sans son écran, avec la raison.
     Les deux moitiés d'un contrat (ADR-0017, décision 2) ne sont plus mises à jour ensemble : c'est cette
     vérification qui les accorde.
7. **La copie de voyage, sur le mobile ou la tablette.** On peut **emporter** un voyage sur son mobile.
   - La copie contient ce qu'il faut pour **consulter** le voyage, avec les **résultats déjà calculés** par le
     serveur (budget, parts).
   - **Hors-ligne, on ne fait qu'ajouter** des éléments nouveaux : d'abord des **dépenses**, plus tard ce que
     le carnet de voyage apportera (photos, lieux visités). Rien de ce que le serveur connaît ne se modifie ni
     ne se supprime hors-ligne ; un ajout pas encore envoyé reste, lui, modifiable sur le mobile.
   - Un ajout fait hors-ligne est montré **« à recalculer »** : les totaux restent ceux du serveur jusqu'au
     retour du réseau. Aucun calcul métier ne se fait sur le mobile.
   - Au retour du réseau, les ajouts partent au serveur, qui les contrôle comme toute écriture, recalcule, et
     met la copie à jour.
   - **Un voyage commencé est presque définitif** : on n'y supprime plus rien, on **annule** (l'élément reste,
     marqué annulé). Une dépense ajoutée hors-ligne qui cite un élément annulé entre-temps est donc acceptée
     (question Q49, tranchée). Seul un membre qui a perdu ses droits voit son ajout **refusé** : l'ajout reste
     sur le mobile, signalé, et rien ne se perd en silence. Cette règle sera écrite dans la spec des dépenses
     et dans celle du voyage, quand ses dates existeront.
   - **Reporté à la construction du mobile** : la technologie de l'application (Q19) ; l'affichage de la
     copie alors que les vues sont des plugins ; la saisie d'une dépense dont les champs dépendent des
     plugins ; le stockage et le **chiffrement** de la copie, réduite au nécessaire et effacée quand on quitte
     le groupe ; un jeton valable pendant des semaines hors-ligne ; le moment où l'on emporte un voyage.
8. **Accès aux données** : Entity Framework Core avec **Npgsql**, sur le serveur seulement, avec un seul
   modèle et une seule série de migrations.
9. **Règles communes**, reprises de l'ADR-0004 : **montants** en décimal (`numeric`) avec leur code devise ;
   **instants** en UTC avec l'identifiant du fuseau du lieu ; **dates de calendrier** sans heure ; **calculs
   métier en C#**, dans `Voyager.Core`, jamais dans une requête propre à la base. **PostGIS sert à chercher**
   (lieux proches, zones), pas au calcul du budget.
10. **L'usage seul passe par le serveur** : un compte, et un groupe d'une seule personne. Le mode solo sans
    serveur de l'ADR-0010 (décision 4) est abandonné. Un utilisateur technique peut toujours héberger son
    propre serveur avec l'image Docker (ADR-0010, option B).
11. **L'authentification se décide avant la première brique.** Le profil de l'utilisateur, qui est un
    voyageur, se rattache à son compte, et chaque donnée à un groupe : l'**ADR** d'authentification et
    d'invitations est rédigé avant la construction de cette brique. Sa **construction** peut venir après : en
    développement, une identité et un groupe fixes, fournis par Aspire, en tiennent lieu.

## Conséquences

- **Positif** :
  - plus de synchronisation générale : l'ADR de synchronisation prévu au backlog se réduit à **la copie de
    voyage** (la télécharger, envoyer les ajouts, traiter les ajouts refusés) ;
  - tous les membres voient les mêmes données et les mêmes totaux ;
  - le serveur contrôle toutes les données, y compris celles des plugins, puisqu'il les fait tourner ;
  - un seul modèle, une seule base, une seule série de contrats ;
  - plus de service local à lancer et surveiller sur chaque PC ; l'application de bureau devient plus simple.
- **Négatif** :
  - l'application de bureau ne fonctionne qu'avec du réseau ;
  - **aucune version utilisable**, même par l'utilisateur seul, avant l'authentification et la mise en ligne
    (Q22 : hébergeur, RGPD, sauvegardes, proxy HTTPS), ou un serveur auto-hébergé ; on ne peut plus essayer
    Voyager sans compte ;
  - les données personnelles des voyageurs sont sur le serveur dès la première brique : hébergement dans
    l'Union européenne, sauvegardes chiffrées, export et suppression à la demande ;
  - le serveur est la seule copie : sauvegardes et **export** (principe 5 de la vision) ne sont plus des
    options ;
  - le serveur porte toute la charge des calculs, pour tous les groupes ;
  - tant que les plugins tournent dans le processus du serveur, un plugin qui plante l'arrête pour tous les
    groupes ; et un groupe ne peut pas installer lui-même un plugin sur le serveur hébergé, il doit le proposer
    au projet ;
  - les écrans d'administration des plugins sont dans l'application : une page qui exécute des modules de
    plugins peut demander des opérations d'administration, d'où le filtre et la confirmation de la décision 3 ;
  - la page « À propos » montre aujourd'hui des faits de diagnostic : venus d'un serveur partagé, ils sont à
    réserver à l'administrateur ;
  - hors-ligne, le mobile montre des totaux qui ne tiennent pas compte des dépenses qu'on vient d'ajouter ;
  - l'application connaît plusieurs serveurs, et un utilisateur présent sur deux serveurs y a deux séries de
    voyageurs, sans lien entre elles ; le compte sur chaque serveur est décidé avec l'ADR d'authentification.
- **Le travail déjà fait** :
  - **reste utile** : `Voyager.Core`, le SDK des plugins, l'interface React, les contrats gRPC, l'application
    Electron et son installation ; la compatibilité par contrat (PR 26) ; et dans `Voyager.PluginHost`, le
    chargement, l'examen d'un paquet, l'extraction sûre, l'empreinte, la signature, l'état et le marqueur des
    plantages (côté .NET des PR 27, 28 et 31), qui passent dans le serveur ;
  - **ne sert plus en l'état** : le service local (`Voyager.LocalService` et ses tests), sa surveillance par
    Electron, sa base SQLite et la tranche « À propos » qui la montre ; `Voyager.Persistence.Sqlite` et
    `Voyager.Sync` ; le côté Electron de la gestion des plugins (boîte de dépôt, copie de préparation,
    demandes écrites par le processus principal, boîtes de confirmation) et l'écran « Plugins » tel qu'il
    est, qui devient un écran d'administration.
- **Questions tranchées par l'utilisateur** : Q47 (plugins relus par le projet, activés par groupe, qui ne
  voient que leur groupe), Q48 (les voyageurs appartiennent à l'utilisateur), Q49 (dans un voyage commencé, on
  annule au lieu de supprimer), Q50 (les avatars dans la base), Q51 (un plugin désactivé pour le seul groupe en
  cause) et Q52 (plusieurs serveurs).
- **Si l'ADR est accepté**, il faudra :
  - passer l'ADR-0004 à « Remplacée par ADR-0018 » ; ajouter aux ADR 0003, 0006, 0009, 0010, 0011, 0012,
    0013, 0015, 0016 et 0017 une ligne « Remplacée en partie par ADR-0018 » (sur le modèle de « Complétée
    par » de l'ADR-0006) ; réviser l'ADR-0014 ; mettre l'index à jour ;
  - rédiger l'**ADR d'authentification et d'invitations**, puis l'**ADR des plugins sur le serveur** ;
  - revoir les specs : [le voyage et ses voyageurs](../specs/projet-voyage-et-voyageurs.md) (la règle « tout
    se fait sur le poste, sans réseau », l'avatar enregistré sur le poste, le critère « poste sans réseau », le
    cas limite des données personnelles), la gestion des plugins et la preuve des plugins ; une spec validée
    repasse au statut « Brouillon » jusqu'à ce que l'utilisateur la valide de nouveau ;
  - mettre à jour la [vision](../vision.md) (l'usage seul, le téléphone, le serveur partagé), le glossaire
    (« Service local », « Mode solo », « Serveur du groupe », « Demande de plugin », « Redémarrer pour
    appliquer », « Registre d'extensions », « Source de faits de diagnostic »), le backlog (Q2, Q26, Q30, Q33
    tranchée par la décision 4, Q45 sans objet), les schémas de `docs/schemas/`, le `README.md`, l'état actuel
    dans `CLAUDE.md` ;
  - mettre à jour les conventions (`organisation-du-code.md`, `tests.md`, `typescript.md`) et les outils de
    Claude (`.claude/rules/typescript.md`, les skills `new-plugin` et `new-use-case`, l'agent `code-reviewer`) ;
  - dans le code :
    - **serveur** : `Voyager.GroupServer` (gRPC-Web, CORS, identité de développement, `Voyager.PluginHost`,
      services de diagnostic et des plugins, EF Core et migrations), l'image `deploy/GroupServer.Dockerfile`
      (plugins livrés, Linux : question Q35), la construction des plugins par Aspire rattachée au serveur ;
    - **retraits** : `Voyager.LocalService` et ses tests, `Voyager.Persistence.Sqlite`, `Voyager.Sync`, le
      service local dans Aspire et dans `tools/release.ps1`, les tests d'architecture qui les citent ;
    - **bureau** : `src/ts/grpc-client` (le jeton), `src/ts/plugin-sdk` (le client du service local),
      `src/ts/desktop/src/main` (lancement et surveillance du service local, démarrage, plugins servis,
      gestion des plugins, CSP), le preload, le paquet (`extraResource` des plugins), la barrière
      `LocalServiceGate` de l'interface, et l'adresse du serveur dans l'application empaquetée.
