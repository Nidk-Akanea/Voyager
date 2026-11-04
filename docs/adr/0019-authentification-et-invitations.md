# ADR-0019 — Authentification, groupes et invitations

- **Statut** : Acceptée
- **Date** : 2026-11-04
- **Remplace** : — (complète [ADR-0010](0010-hebergement-serveur-heberge-multi-groupes.md), qui rend
  l'authentification indispensable, [ADR-0018](0018-client-serveur-et-copie-de-voyage.md), qui la place avant
  la première brique du cœur et lui renvoie le mécanisme du jeton, et
  [ADR-0006](0006-dependances-principales.md), pour les dépendances de la décision 11)

## Contexte

Avec l'[ADR-0018](0018-client-serveur-et-copie-de-voyage.md), toutes les données vivent sur le serveur du
groupe, y compris celles d'un utilisateur seul. Le serveur doit donc savoir **qui** l'appelle et **à quels
groupes** cette personne appartient, avant même la première brique du cœur : le profil de l'utilisateur, qui
est un voyageur, se rattache à son compte.

Ce que les décisions précédentes imposent :

- **Plusieurs serveurs, indépendants** : un serveur est une instance de l'application (le serveur hébergé par
  le projet, ou un serveur auto-hébergé avec l'image Docker). Un utilisateur peut être membre de groupes sur
  plusieurs serveurs (question Q52). Un serveur auto-hébergé doit fonctionner **sans dépendre d'un service du
  projet** ni d'un fournisseur en ligne.
- **Le jeton n'est jamais lisible par la page** : les modules d'interface des plugins s'y exécutent
  (ADR-0018, décision 3), dont des modules non signés venus d'un serveur auto-hébergé, chargés avec l'accord
  de l'utilisateur (ADR-0018, décision 5). Une opération d'administration demande une confirmation native ou
  une nouvelle authentification.
- **Le mobile travaille hors-ligne pendant des semaines** : sa connexion doit survivre au voyage, et un
  téléphone perdu doit pouvoir être déconnecté (ADR-0018, décision 7).
- **Les utilisateurs ne sont pas techniciens** : une famille, une bande d'amis. Rejoindre un groupe doit
  tenir en un geste : « on m'a envoyé un lien ».
- **Rôles** : il faut quelqu'un pour gérer un groupe (inviter, activer les plugins du groupe) et quelqu'un
  pour gérer le serveur (installer les plugins, ADR-0018, décision 4).
- **Données personnelles** : un compte en est une (adresse e-mail, nom). Le dépôt est public, et rien de réel
  n'y entre. Les règles des groupes sont des règles métier : elles vont dans le cœur
  ([ADR-0005](0005-architecture-du-code-hexagonale-par-domaine.md)).
- **En développement**, une identité et un groupe fixes, fournis par Aspire, tiennent lieu
  d'authentification en attendant sa construction (ADR-0018, décision 11).

## Options

### Qui tient les comptes

#### Option A — Chaque serveur tient ses comptes et délivre ses jetons (OpenID Connect intégré)

Le serveur du groupe contient son propre fournisseur d'identité : ASP.NET Core Identity pour les comptes
(mots de passe hachés, limites d'essais, confirmation de l'adresse e-mail, second facteur) et **OpenIddict**
(licence Apache 2.0) pour délivrer des jetons selon le standard **OpenID Connect**.

- Avantages :
  - chaque serveur est autonome : l'auto-hébergement ne dépend de personne ;
  - un standard : l'application de bureau, l'application mobile et plus tard d'autres clients utilisent le
    même flux, avec des bibliothèques éprouvées ;
  - la page de connexion vit sur le serveur : d'autres moyens de connexion (clés d'accès, lien envoyé par
    e-mail, comptes Google, Microsoft ou Apple) s'y ajoutent plus tard sans toucher aux applications ;
  - pas de coût par utilisateur, et les données des comptes restent chez l'hébergeur.
- Inconvénients :
  - un fournisseur d'identité à configurer et à tenir à jour dans le serveur (clés de signature, pages de
    connexion, réinitialisation du mot de passe) ;
  - le serveur doit pouvoir **envoyer des e-mails** (confirmation, mot de passe oublié) : un réglage de plus
    pour l'hébergeur ;
  - un compte par serveur : un utilisateur présent sur deux serveurs a deux comptes.

#### Option B — Des jetons simples d'ASP.NET Core Identity, avec un formulaire dans l'application

ASP.NET Core Identity seul (`MapIdentityApi`) : l'application envoie l'adresse et le mot de passe, et reçoit
un jeton et un jeton de renouvellement propres à ASP.NET Core.

- Avantages : le plus simple à construire ; pas de passage par le navigateur. Le risque qu'un module de plugin
  lise le mot de passe se traite par une fenêtre de connexion à part, sans plugins.
- Inconvénients : un format propre à ASP.NET Core, pas un standard : chaque nouveau moyen de connexion
  (comptes Google, clés d'accès) demande de changer les applications, et l'application mobile doit refaire le
  même formulaire.

#### Option C — Un fournisseur d'identité en ligne (Auth0, Microsoft Entra External ID…)

- Avantages : comptes, e-mails, clés d'accès et comptes Google prêts à l'emploi ; sécurité tenue par un
  spécialiste.
- Inconvénients : un serveur auto-hébergé dépend d'un service extérieur, ou doit en configurer un ; coût par
  utilisateur au-delà d'un seuil ; les données des comptes partent chez un tiers (RGPD : un sous-traitant de
  plus, souvent hors de l'Union européenne).

#### Option D — Un fournisseur d'identité à part, auto-hébergé (Keycloak…)

- Avantages : complet et standard ; une instance peut servir plusieurs serveurs.
- Inconvénients : un second logiciel (Java) et une seconde base à déployer à côté du serveur ; bien trop lourd
  pour un utilisateur qui héberge son propre serveur.

Duende IdentityServer, l'équivalent d'OpenIddict, a été écarté : sa licence est payante au-delà d'un seuil de
chiffre d'affaires, alors qu'OpenIddict est libre.

### Le premier moyen de connexion

- **Un mot de passe** : connu de tous, et il fonctionne sans envoi d'e-mails ; mais il s'oublie et se
  réutilise d'un site à l'autre.
- **Sans mot de passe** (un lien envoyé par e-mail, ou une clé d'accès) : plus simple pour des non-techniciens
  et plus sûr ; mais le lien exige un envoi d'e-mails fiable à chaque connexion, et les clés d'accès sont
  encore mal connues. Les deux pourront s'ajouter à la page de connexion sans changer les applications.

### Comment l'application de bureau se connecte

- **Option 1 — Par le navigateur du système** (flux « code d'autorisation avec PKCE », recommandé par la
  norme RFC 8252 pour les applications installées) : l'application ouvre la page de connexion du serveur dans
  le navigateur, qui la renvoie à l'application avec un code ; le processus principal échange ce code contre
  les jetons. L'utilisateur reconnaît la page de son serveur, et l'application ne voit jamais son mot de
  passe.
- **Option 2 — Dans une fenêtre de l'application**, sans plugins : plus intégré visuellement, mais une
  fenêtre intégrée peut imiter n'importe quelle page de connexion, et les comptes Google ou Microsoft
  refusent souvent d'y être utilisés.

### Le chemin des requêtes, sur le bureau

L'[ADR-0018](0018-client-serveur-et-copie-de-voyage.md) (décision 3) veut que le jeton reste hors de la page.

- **(a) Un protocole interne** (`protocol.handle` d'Electron) : la page appelle une adresse de l'application,
  le processus principal ajoute le jeton et transmet au serveur. La page ne voit ni le jeton ni l'adresse du
  serveur ; pas de CORS, et la politique de sécurité du contenu (CSP) reste fermée à l'extérieur. Le passage
  des flux gRPC-Web par ce protocole est à éprouver.
- **(b) L'en-tête ajouté au vol** (`session.webRequest`) : la page appelle directement le serveur, et le
  processus principal ajoute le jeton à la requête. Simple, mais la CSP doit s'ouvrir à l'adresse du serveur,
  le serveur doit ouvrir le CORS à l'origine de l'application, et la page connaît l'adresse.
- **(c) Un transport par messages entre la page et le processus principal** (IPC, par le preload) : la page
  envoie ses appels par messages, le processus principal les exécute. Les flux s'y prêtent bien, mais il faut
  écrire et maintenir ce transport pour le client Connect.

## Décision

**Option A**, avec **un mot de passe** pour commencer, **la connexion par le navigateur du système**
(option 1) et **le protocole interne** (a) (recommandés). Chaque serveur reste autonome, et le standard
OpenID Connect sert aussi bien le bureau, le mobile que les moyens de connexion à venir.

1. **Un compte par serveur.**
   - Chaque serveur tient ses comptes avec **ASP.NET Core Identity** et délivre ses jetons avec **OpenIddict**.
   - Un compte a une **adresse e-mail**, unique sur le serveur, et un **nom affiché** ; rien d'autre n'est
     demandé. L'adresse est **confirmée** quand le serveur sait envoyer des e-mails (question Q53).
   - On se connecte avec l'adresse et un **mot de passe** ; les clés d'accès, le lien par e-mail et les comptes
     Google, Microsoft ou Apple pourront s'ajouter à la page de connexion sans changer les applications.
   - L'**administrateur du serveur** a en plus un **second facteur** obligatoire (code à usage unique d'une
     application d'authentification, ou clé d'accès) : installer un plugin, c'est faire exécuter du code par le
     serveur.
   - Un utilisateur présent sur deux serveurs y a deux comptes, sans lien entre eux (question Q52).
   - Qui peut créer un compte et un groupe sur le serveur hébergé (inscription ouverte, ou sur invitation) est
     la question Q54 ; un mineur peut-il avoir un compte, la question Q55.
2. **La connexion passe par le navigateur du système**, avec le flux « code d'autorisation avec PKCE »
   d'OpenID Connect.
   - Le processus principal reçoit le code par une adresse de retour sur `127.0.0.1`, sur un port choisi au
     moment de la connexion ; le serveur accepte n'importe quel port pour cette adresse, comme le veut la
     RFC 8252 (à régler dans OpenIddict). On préfère ce retour à un schéma propre à l'application
     (`voyager://`), que n'importe quelle autre application peut déclarer sous Windows.
   - Après une déconnexion, la connexion suivante redemande le mot de passe (`prompt=login`), pour qu'une
     autre personne sur le même PC ne reprenne pas le compte précédent par la session du navigateur.
   - L'application mobile utilisera le même flux.
3. **Les jetons.**
   - Un **jeton d'accès** court (de l'ordre de 15 minutes), signé par le serveur, ne porte que l'identifiant du
     compte, ni adresse ni nom. Le serveur relit à chaque requête que le compte est actif, et ses groupes et ses
     rôles, pour qu'un retrait prenne effet tout de suite.
   - Un **jeton de renouvellement** sert à en obtenir un nouveau sans se reconnecter. Il change à chaque usage ;
     un jeton déjà utilisé qui revient fait révoquer toute la série (signe d'un vol), après un court délai de
     grâce (`RefreshTokenReuseLeeway` d'OpenIddict) pour qu'une réponse perdue sur un réseau instable ne
     déconnecte pas l'utilisateur. Sa durée glisse avec l'usage, avec un maximum absolu, et dépend du client :
     quelques semaines sur le bureau, de quoi couvrir un voyage sur le mobile. Les valeurs se règlent à la
     construction.
   - Un jeton expiré pendant le voyage ne fait perdre **aucun ajout en attente** : les ajouts partent après la
     reconnexion.
   - Sur le bureau, le processus principal garde le jeton de renouvellement **chiffré par le système**
     (`safeStorage` d'Electron, qui s'appuie sur DPAPI sous Windows), **un par serveur**, et refuse de
     l'enregistrer si ce chiffrement n'est pas disponible. Il protège contre les autres comptes Windows et le
     vol du disque, pas contre un autre programme lancé par le même utilisateur. Le jeton d'identité n'est pas
     gardé sur le disque. Sur le mobile, le jeton va dans le stockage sécurisé du système. La page ne voit
     jamais aucun jeton.
   - **Changer ou réinitialiser son mot de passe**, ou changer d'adresse, révoque tous les jetons de
     renouvellement du compte.
   - **Les appareils connectés** : l'application de bureau montre la liste des appareils connectés au compte
     (application, date du dernier usage) et permet d'en déconnecter un, ou tous. Un téléphone perdu se
     déconnecte ainsi ; sa copie de voyage s'efface à son prochain contact avec le serveur. Hors réseau, elle
     reste lisible : c'est pourquoi elle est chiffrée (ADR-0018, décision 7).
   - **Se déconnecter** révoque le jeton de renouvellement, ferme la session du serveur (fin de session OpenID
     Connect) et efface le jeton du poste.
4. **Le chemin des requêtes, sur le bureau : le protocole interne.**
   - La page appelle `voyager://api/<serveur>/…`, où `<serveur>` est l'identifiant d'un **serveur déjà
     enregistré** dans l'application, jamais une adresse libre. Le processus principal vérifie que le service
     est permis à la page, ajoute le jeton d'accès, et transmet au serveur en HTTPS.
   - **Chaque serveur a sa propre vue**, isolée (une session Electron par serveur) : les modules d'interface
     d'un serveur ne peuvent appeler que ce serveur. Un module non signé d'un serveur auto-hébergé ne peut donc
     pas agir sur le serveur hébergé au nom de l'utilisateur.
   - La CSP de la page est fermée à toute adresse extérieure, pour toutes les directives (`connect-src`,
     `img-src`, `form-action`, `frame-src`…), et la navigation hors de l'application est bloquée.
   - Les **opérations d'administration** (du groupe ou du serveur) demandent une **confirmation native**,
     affichée par le processus principal. Une opération sensible (supprimer un groupe, installer un plugin)
     demande en plus une **connexion récente**, que le **serveur vérifie lui-même** (date de connexion portée
     par le jeton, nouvelle connexion demandée sinon) : la confirmation native ne protège que contre la page.
   - Quand le jeton d'accès expire pendant un flux de changements, le serveur ferme le flux ; le processus
     principal renouvelle le jeton et le rouvre.
   - Ce chemin, et le passage des flux gRPC-Web par le protocole interne, sont à éprouver dès la bascule.
5. **Les groupes et les rôles.**
   - Les règles des groupes (au moins un administrateur, transmission du rôle, ancien membre) sont des règles
     métier : elles vont dans un domaine du cœur (`Groups`), avec un port qui donne l'identité de l'appelant.
     ASP.NET Core Identity et OpenIddict restent des adaptateurs dans `Voyager.GroupServer` ; `Voyager.Core`
     n'en dépend pas.
   - Un **groupe** réunit des comptes du même serveur (une famille, une bande d'amis). Celui qui le crée en est
     l'**administrateur**.
   - **Administrateur du groupe** : invite et retire des membres, donne ou retire le rôle d'administrateur,
     active les plugins du groupe, renomme ou supprime le groupe. Un groupe garde toujours au moins un
     administrateur.
   - **Membre** : tout le reste (voyages, voyageurs, devis, dépenses). Les membres d'un groupe voient le nom
     affiché des autres, jamais leur adresse e-mail. Les droits plus fins, voyage par voyage, viendront avec la
     collaboration (E6).
   - **Supprimer un groupe** est différé : les membres sont prévenus, un export leur est proposé, et la
     suppression peut être annulée pendant 7 jours.
   - **Administrateur du serveur** : un rôle à part, sur le serveur et non dans un groupe. Il installe les
     plugins (ADR-0018, décision 4) et gère le serveur. **L'application ne lui donne aucun accès** aux données
     des groupes dont il n'est pas membre ; il voit la liste des comptes (adresse, nom), pas leur appartenance
     aux groupes.
   - **Le premier administrateur du serveur** est désigné par un **code d'installation** à usage unique, que le
     serveur écrit dans ses journaux à son premier démarrage (ou qu'une commande du conteneur produit) ; il est
     consommé par la personne qui se connecte avec. Jamais par la première personne qui s'inscrit, ni par une
     adresse placée dans un réglage.
   - L'usage seul est un groupe d'une personne, dont elle est l'administrateur.
6. **Les invitations.**
   - Un administrateur du groupe crée une invitation ; l'application lui montre un **lien** à envoyer par le
     moyen qu'il veut (message, e-mail). Le serveur peut aussi l'envoyer par e-mail s'il sait le faire ; il ne
     garde pas l'adresse après l'envoi.
   - Le lien est `https://<serveur>/invite#<code>` : le code est dans la partie de l'adresse qui n'est jamais
     envoyée au serveur, et n'apparaît donc pas dans les journaux du proxy. La page du serveur propose
     « Ouvrir dans Voyager » ou explique comment installer l'application ; elle ne transmet pas l'adresse de
     provenance (`Referrer-Policy: no-referrer`). Le lien de réinitialisation du mot de passe suit la même
     règle.
   - Le code est **aléatoire** (au moins 128 bits, sans rapport avec l'identifiant de l'invitation), **à usage
     unique**, valable 7 jours, et l'administrateur peut l'annuler. Le serveur n'en garde que l'empreinte et
     limite les essais. Il n'est **consommé** qu'au moment où la personne, connectée, rejoint le groupe, jamais
     à la simple ouverture du lien (les aperçus des messageries ouvrent les liens).
   - Le lien n'est pas lié à une adresse : qui le reçoit entre. L'administrateur voit qui a rejoint par chaque
     lien. Une famille de cinq demande cinq liens.
   - **Un serveur inconnu**, découvert par un lien, n'est ajouté à l'application qu'après une **confirmation
     native** qui montre son adresse : un faux lien ne mène pas en silence vers un serveur pirate. Le schéma
     `voyager://` venu de l'extérieur n'accepte que la forme d'une invitation (`voyager://join…`), jamais
     `api` ni `app`, et ne transporte jamais de jeton.
7. **Sécurité de la connexion.**
   - HTTPS obligatoire hors du développement (le proxy de l'ADR-0010), y compris pour un serveur auto-hébergé
     sur un réseau local : OpenIddict l'exige.
   - Des essais limités par adresse et par compte, avec des **délais qui grandissent** plutôt qu'un blocage du
     compte, qu'un tiers pourrait provoquer exprès ; les adresses IP qui servent à ces limites sont gardées peu
     de temps.
   - Un mot de passe d'au moins 12 caractères. Aucune page (inscription, mot de passe oublié, invitation) ne
     révèle si une adresse a déjà un compte : la réponse est la même dans tous les cas.
   - Les **clés** de signature et de chiffrement d'OpenIddict, et celles qui protègent les cookies et les liens
     (Data Protection), durent après un redémarrage (dans la base ou un volume) ; elles ne sont **ni dans le
     dépôt ni dans l'image** Docker : elles viennent des secrets de l'hébergeur, et sont générées pour chaque
     poste en développement.
   - **Journaux** : jamais de mot de passe, de jeton, d'en-tête `Authorization`, de code d'autorisation,
     d'adresse de retour avec son code, de code d'invitation ou de réinitialisation, d'adresse e-mail ni de nom
     affiché. Un compte y est désigné par son identifiant. La télémétrie (OpenTelemetry) et les journaux des
     requêtes HTTP sont réglés pour ne garder ni les en-têtes d'authentification ni les paramètres des
     adresses.
8. **Supprimer son compte.** Un utilisateur peut supprimer son compte à tout moment.
   - Son compte, ses jetons et ses appareils connectés sont effacés.
   - **Ses voyageurs** (son profil et ceux qu'il a déclarés, question Q48) sont effacés, sauf ceux qu'un voyage
     d'un groupe cite encore (parts, dépenses) : ceux-là sont **anonymisés** (« voyageur supprimé », sans date
     de naissance, avatar ni description), pour ne pas casser les budgets des autres.
   - Ce qu'il a saisi dans un voyage du groupe (une dépense, par exemple) reste au groupe, avec pour auteur
     « ancien membre ». C'est une pseudonymisation : les autres membres savent de qui il s'agissait ; elle se
     justifie par l'exactitude des comptes du groupe.
   - S'il est le seul administrateur d'un groupe qui a d'autres membres, le rôle passe au membre qu'il choisit,
     sinon au plus ancien : la suppression n'est jamais refusée.
   - Un groupe dont il est le seul membre est supprimé avec ses données ; un export lui est proposé avant.
   - Dans les sauvegardes, l'effacement prend effet à la fin de leur durée de conservation (ADR de mise en
     ligne, Q22).
   - Le serveur purge aussi les comptes jamais confirmés après quelques jours, les invitations expirées ou
     annulées, et les jetons révoqués ou expirés.
9. **En développement.** Aspire crée un compte et un groupe de développement fixes, et le serveur accepte une
   **identité de développement** sans connexion, seulement si l'environnement est `Development` **et** qu'un
   réglage propre à Aspire est présent. Ailleurs, le serveur refuse de démarrer avec ce réglage, et un test le
   vérifie, sur l'image publiée aussi. Les comptes de développement et d'essai sont fictifs
   (`traveler-a@example.test`).
10. **Ce qu'un module d'interface peut faire.** Le jeton reste caché, mais un module d'interface agit avec les
    droits de l'utilisateur sur son serveur, dans tous ses groupes, pour les services permis à la page : c'est
    la liste de ces services, la confirmation native et la connexion récente vérifiée par le serveur qui
    protègent les opérations d'administration, pas le secret du jeton.
11. **Les dépendances** :
    - `OpenIddict.AspNetCore` et `OpenIddict.EntityFrameworkCore` (Apache 2.0) ;
    - `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (MIT) ;
    - pour le processus principal, `oauth4webapi` ou `openid-client` (MIT), à choisir à la construction
      (`openid-client` 6 n'existe qu'en module ES, à vérifier avec le processus principal).

## Conséquences

- **Positif** :
  - chaque serveur est autonome, auto-hébergé compris, sans service extérieur ni coût par utilisateur ;
  - un seul standard pour le bureau et le mobile, et de nouveaux moyens de connexion sans changer les
    applications ;
  - l'application ne voit jamais le mot de passe, et la page jamais un jeton ; un serveur ne peut pas agir sur
    un autre au nom de l'utilisateur ;
  - les rôles sont simples : administrateur du groupe, membre, administrateur du serveur.
- **Négatif** :
  - le serveur porte un fournisseur d'identité : clés à garder et à renouveler, pages de connexion,
    d'inscription et de mot de passe oublié à écrire (Razor Pages, avec cookies et protection contre les
    requêtes forgées) et à traduire (Fluent et Linguini, [ADR-0008](0008-globalisation-fluent-et-linguini.md)) ;
  - le serveur doit **envoyer des e-mails** : un service d'envoi pour le serveur hébergé, un réglage SMTP pour
    un serveur auto-hébergé (question Q53) ;
  - un serveur auto-hébergé doit avoir un certificat HTTPS, même sur un réseau local (complète l'ADR-0010,
    option B) ;
  - se connecter fait passer par le navigateur, ce qui surprend un peu la première fois ; deux serveurs, deux
    comptes et deux mots de passe ;
  - une vue par serveur dans l'application de bureau : l'interface montre un serveur à la fois ;
  - **l'hébergeur garde un accès technique** à la base, aux sauvegardes et au processus du serveur, et les
    plugins qu'il installe y tournent : il doit être de confiance, et la politique de confidentialité le dira
    (ADR de mise en ligne, Q22) ; un serveur auto-hébergé en est lui-même responsable ;
  - une application mobile en technologie web (Q19) devra sans doute passer par le CORS : l'argument « pas de
    CORS » ne vaut que pour le bureau ;
  - le chemin des requêtes par le processus principal est à éprouver avec les flux gRPC-Web.
- **Données personnelles** : le compte (adresse, nom) s'ajoute aux voyageurs dans ce que l'hébergeur garde ;
  la politique de confidentialité, l'export et les durées de conservation relèvent de l'ADR de mise en ligne
  (Q22).
- **Questions ouvertes** : Q53 (envoi des e-mails), Q54 (qui peut créer un compte et un groupe), Q55 (comptes
  des mineurs).
- **Glossaire** : Compte, Administrateur du groupe, Administrateur du serveur, Ancien membre, Appareil
  connecté ; Utilisateur, Membre et Invitation sont précisés.
- **Si l'ADR est accepté** :
  - ajouter « Complétée par ADR-0019 » aux ADR 0006, 0010 et 0018 ;
  - construire l'identité de développement avec la bascule, puis la connexion réelle avant toute mise en
    ligne ;
  - revoir la spec du voyage et de ses voyageurs : l'utilisateur est le titulaire d'un compte, et son profil de
    voyageur se crée à sa première connexion sur un serveur ;
  - écrire une spec des comptes, des groupes et des invitations (écrans, textes, cas limites) avant de les
    construire.
