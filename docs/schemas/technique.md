# Schéma technique

> Première version, issue des ADR [0002](../adr/0002-architecture-modulaire-par-contrats-et-plugins.md),
> [0003](../adr/0003-choix-de-la-stack.md) et [0004](../adr/0004-stockage-des-donnees.md).

## Vue d'ensemble

```mermaid
flowchart LR
    subgraph Poste["Poste d'un membre (Windows)"]
        direction TB
        subgraph App["Application Electron"]
            direction TB
            Main["Processus principal (Node.js)<br/>plugins · mises à jour<br/>lancement du service local"]
            subgraph UI["Interface (React + TypeScript)"]
                direction TB
                Shell["Coquille<br/>navigation · Fluent UI React"]
                SDK["SDK plugins<br/>registre des points d'extension"]
                PUI["Modules d'interface des plugins<br/>vues · fiches de prestation"]
                WebView["Vue web intégrée<br/>extraction ciblée"]
                Shell --> SDK --> PUI
            end
        end
        subgraph Local["Service local (.NET 10)"]
            direction TB
            LApi["API gRPC-Web"]
            LCore["Voyager.Core<br/>modèle · contrats · calculs"]
            LPlug["Plugins .NET"]
            LSync["Synchronisation"]
            SQLite[("SQLite")]
            LApi --> LCore --> SQLite
            LPlug --> LCore
            LSync --> SQLite
        end
        App -- "gRPC-Web (localhost)" --> Local
    end

    subgraph Server["Serveur du groupe (ASP.NET Core)<br/>en local ou en ligne"]
        direction TB
        SApi["API gRPC"]
        SCore["Voyager.Core"]
        SPlug["Plugins .NET<br/>dont connecteurs d'API"]
        SSync["Synchronisation<br/>diffusion des changements"]
        Cache["Cache des réponses d'API"]
        PG[("PostgreSQL + PostGIS")]
        SApi --> SCore --> PG
        SPlug --> SCore
        SSync --> PG
        SPlug --> Cache
    end

    subgraph Ext["Services et sources externes"]
        direction TB
        Apis["API payantes<br/>vols · hôtels · activités"]
        Maps["Fonds de carte<br/>et itinéraires"]
        Pages["Pages web<br/>Booking…"]
        Packs["Paquets de données<br/>format Voyager · GTFS · GeoJSON"]
    end

    Local <-- "gRPC · flux de changements" --> Server
    Server -- "via le cache" --> Apis
    App -- "carte" --> Maps
    App -- "extraction ciblée" --> Pages
    Packs -. import .-> Local
```

## Les composants

| Composant | Rôle | Technologie |
| --- | --- | --- |
| Processus principal Electron | Fenêtre, mises à jour, installation des plugins, lancement et surveillance du service local, remise de son jeton à l'interface | Electron, Node.js |
| Interface | Afficher et saisir ; **aucun calcul métier** | React, TypeScript, Fluent UI React |
| SDK plugins | Registre où les modules d'interface des plugins déclarent ce qu'ils fournissent | TypeScript (`@voyager/plugin-sdk`) |
| Vue web intégrée | Afficher une page (fiche d'hôtel…) et en extraire les informations utiles | Electron `WebContentsView` |
| Service local | Logique métier, base hors-ligne, synchronisation, parties .NET des plugins | ASP.NET Core (.NET 10), gRPC-Web |
| Voyager.Core | Modèle commun, contrats, calculs (coûts, répartition, faisabilité) ; partagé client et serveur | Bibliothèque .NET |
| Serveur du groupe | Données partagées du groupe, synchronisation, connecteurs d'API payantes | ASP.NET Core (.NET 10), gRPC |
| Base locale | Copie de travail du membre, hors-ligne | SQLite (EF Core) |
| Base du serveur | Données de référence et voyages du groupe | PostgreSQL + PostGIS (EF Core, Npgsql) |
| Cache d'API | Limiter les appels aux API payantes | À définir |

## Les flux

1. **Interface → service local** : toujours en gRPC-Web sur la machine, même en ligne. L'interface ne
   parle jamais directement au serveur du groupe.
2. **Service local ↔ serveur du groupe** : synchronisation en gRPC quand le réseau est là ; le serveur
   pousse en flux les changements des autres membres.
3. **API payantes** : appelées **par le serveur du groupe**, qui détient les clés configurées par le
   propriétaire de l'instance et met les réponses en cache. Les clés ne sont jamais copiées sur les
   postes.
4. **Extraction ciblée** : faite **sur le poste**, dans la vue web intégrée, pendant que le membre
   consulte la page.
5. **Paquets de données** : importés par le service local, puis partagés avec le groupe par la
   synchronisation.
6. **Fonds de carte** : chargés directement par l'interface (fournisseur à choisir, Q5).

## Le démarrage et la connexion au service local

Mis en place par l'étape 2 du squelette (écran « À propos »).

```mermaid
sequenceDiagram
    participant P as Processus principal Electron
    participant L as Service local
    participant W as Fenêtre (interface React)
    P->>W: Ouvre voyager://app/index.html (fichiers de l'interface + CSP)
    P->>L: Lance l'exécutable avec un jeton secret tiré au hasard
    L->>L: Met la base SQLite à jour (migrations)
    L->>L: Écoute sur 127.0.0.1, sur un port libre qu'il choisit
    L-->>P: Annonce son adresse (VOYAGER_LISTENING …)
    W->>P: preload : getLocalServiceState() et abonnement aux changements
    P->>P: Vérifie que l'appel vient bien de voyager://app
    P-->>W: État du service local : prêt, avec son adresse et son jeton
    W->>L: gRPC-Web + en-tête x-voyager-token
    L->>L: Refuse sans le bon jeton ; CORS : seule l'origine voyager://app
    L-->>W: GetServiceInfo : version, moteur, mode, démarrage, état de la base
```

### Si le service local s'arrête

Le processus principal surveille le service qu'il a lancé (`localServiceSupervisor.ts`). L'interface
n'affiche ses écrans que lorsqu'il est prêt (`LocalServiceGate`).

```mermaid
sequenceDiagram
    participant P as Processus principal Electron
    participant L as Service local
    participant W as Fenêtre (interface React)
    L--xP: S'arrête alors que l'application tourne
    P-->>W: État « redémarrage » (poussé)
    P->>L: Relance après 1 s, puis 2 s, puis 4 s (3 fois en 60 s au plus)
    L-->>P: Nouvelle adresse (nouveau port) et nouveau jeton
    P-->>W: État « prêt » avec la nouvelle adresse et le nouveau jeton
    W->>W: Nouveaux clients gRPC, cache de données vidé
    Note over P,W: Au-delà : état « échec », bouton « Réessayer »<br/>(retryLocalService) qui relance depuis zéro
```

Un service qui n'arrive pas à démarrer n'est pas relancé en boucle : l'état « échec » est immédiat. La
raison de l'échec reste dans le journal de l'application ; elle n'est pas envoyée à l'interface (elle
peut contenir des chemins, donc le nom du compte). En développement, le service appartient à Aspire :
il n'est pas surveillé.

Le service local reste lié à l'application : Electron garde ouverte son entrée standard, et le
service s'arrête dès qu'elle se ferme. À la fermeture normale, Electron attend qu'il ait fini
(5 secondes au plus) ; si Electron plante, le service s'arrête de lui-même au lieu de rester orphelin.

Ce qui protège le service local, que tout programme de la machine pourrait appeler :

- il n'écoute que sur `127.0.0.1`, sur un port choisi à chaque lancement (donc après chaque relance) ;
- chaque appel doit porter le **jeton** tiré au démarrage (un nouveau à chaque relance), que le
  processus principal ne remet qu'à la page de l'interface ;
- CORS n'autorise que l'origine de l'interface (`voyager://app`).

Le jeton protège des pages web ouvertes dans un navigateur, des autres comptes de la machine et des
requêtes détournées vers `127.0.0.1` (*DNS rebinding*). Il ne protège pas d'un logiciel malveillant
qui tourne déjà sous le compte du membre : celui-ci peut lire l'environnement et la mémoire des
processus du compte.

La fenêtre est verrouillée : `contextIsolation`, `sandbox`, pas de `nodeIntegration`, aucune nouvelle
fenêtre, aucune navigation hors de l'interface, aucune permission (caméra, position…). Dans
l'application installée, une Content Security Policy ne laisse en plus la page parler qu'à
`127.0.0.1`.

**En développement**, Aspire (`Voyager.AppHost`) lance lui-même le service local, avec un jeton
généré et une base à part (`%LOCALAPPDATA%\Voyager\development\voyager.db`), l'interface avec Vite,
puis Electron, à qui il passe leurs adresses et le jeton (`VOYAGER_UI_URL`,
`VOYAGER_LOCAL_SERVICE_URL`, `VOYAGER_LOCAL_TOKEN`) : Electron ne lance alors pas son propre
service. L'application installée ignore ces variables, et ne transmet à son service local que les
variables système du poste (chemins, dossier temporaire…), en mode Production.

## Le démarrage de l'application installée

Mis en place par [ADR-0014](../adr/0014-ecran-de-demarrage-et-mise-a-jour-au-demarrage.md). La fenêtre
de démarrage est la même interface, page `?screen=startup` : elle est traduite comme le reste.

```mermaid
sequenceDiagram
    participant P as Processus principal Electron
    participant S as Fenêtre de démarrage
    participant U as Serveur de mises à jour
    participant L as Service local
    participant W as Fenêtre principale
    P->>S: Ouvre la petite fenêtre
    P-->>S: « Recherche de mises à jour »
    P->>U: Cherche une version plus récente (8 s au plus)
    alt Une version plus récente existe
        U-->>P: Fichier de versions
        P-->>S: « Téléchargement… » avec le pourcentage
        P->>U: Télécharge le paquet
        P-->>S: « Installation, Voyager va redémarrer »
        P->>P: Passe la main à Velopack et quitte
        Note over P: Velopack installe la nouvelle version<br/>et relance l'application
    else À jour, hors-ligne ou erreur
        P-->>S: « Démarrage de Voyager »
        P->>L: Lance le service (voir plus haut)
        L-->>P: Prêt (ou échec)
        P->>W: Ouvre la fenêtre principale
        P->>S: Ferme la petite fenêtre
    end
```

Le service local n'est lancé qu'une fois la mise à jour écartée : il garde des fichiers du dossier que
Velopack va remplacer. La source des mises à jour est celle écrite dans le paquet à sa fabrication
(`tools/release.ps1 -UpdateSource`), sinon les *releases* GitHub ; elle doit être en `https` (ou en
`http` vers la machine) car une mise à jour est du code que l'application exécute et qu'aucune
signature ne garantit encore. En développement, la fenêtre de démarrage n'apparaît pas, sauf avec la variable
`VOYAGER_SHOW_STARTUP_SCREEN` (ignorée par l'application installée) ; le service local ne reçoit pas les
messages de cette fenêtre : seule la fenêtre principale connaît son adresse et son jeton.

## Le paquet installable et les journaux

Mis en place par `tools/release.ps1` ([ADR-0009](../adr/0009-installation-et-mises-a-jour-velopack.md)).

```mermaid
flowchart LR
    G[GitVersion] --> P[dotnet publish<br/>service local, win-x64]
    G --> B[pnpm build<br/>interface + Electron]
    P --> A[packageApp<br/>asar + ressources + fuses]
    B --> A
    A --> V[vpk pack<br/>Velopack]
    V --> R[artifacts/release<br/>Setup.exe + paquets]
```

L'application installée contient, dans son dossier de ressources, le service local publié
(`resources/local-service`), les plugins (`resources/plugins`) et l'interface construite (`resources/ui`) ; le code d'Electron est dans
l'archive `app.asar`, hors les modules natifs (Velopack) qui restent à côté. Les *fuses* d'Electron
sont posées sur l'exécutable et relues avant d'emballer ; le canal (`stable`, `beta`, `alpha`) vient du
suffixe de la version.

Au démarrage, l'application installée cherche une version plus récente, la télécharge et la fait
installer (voir plus haut). Hors-ligne, ou sans release publiée, elle continue et s'ouvre (une ligne dans
le journal). Une application lancée depuis un simple dossier (non installée) ne cherche rien.

**Journaux** : un fichier pour l'application (`desktop.log`) et un par jour pour le service local
(`Voyager.LocalService-AAAAMMJJ.log`), dans `%LOCALAPPDATA%\Voyager\logs`, à côté de la base. Les
chemins y sont masqués (`~` pour le dossier personnel) et la règle « aucune donnée personnelle »
s'applique. Rien n'est envoyé hors de la machine.

## Les plugins dans le service local

Décidés par l'[ADR-0015](../adr/0015-mecanisme-de-plugins.md), éprouvés par la
[preuve des plugins](../specs/plateforme-preuve-des-plugins.md).

```mermaid
flowchart TB
    subgraph Hote["Service local (processus unique)"]
        Cat["PluginCatalog<br/>lit plugins/&lt;id&gt;/plugin.json"]
        Reg["ExtensionRegistry"]
        Col["DiagnosticFactCollector<br/>délai 5 s · 50 faits · try/catch"]
        Svc["ServiceDiagnosticFactSource<br/>le service lui-même"]
        Grpc["PluginHostService (gRPC-Web)<br/>ListPlugins · GetDiagnosticFacts"]
        subgraph P1["Contexte de chargement du plugin A"]
            C1["Conteneur privé<br/>ILogger + ses services"]
            S1["Source de faits du plugin A"]
        end
        subgraph P2["Contexte de chargement du plugin B (écarté)"]
            X["Manifeste invalide, SDK incompatible,<br/>conteneur invalide ou exception"]
        end
    end
    Shared["Assemblies partagées : Voyager.Sdk, Voyager.Core, NodaTime,<br/>abstractions d'injection et de journalisation"]
    Cat -->|"charge, valide contre provides"| P1
    Cat -.->|"raison journalisée, état affiché"| P2
    S1 --> Reg
    Svc --> Reg
    Reg --> Col
    Col --> Grpc
    P1 -. "types partagés" .-> Shared
    Hote -. "types partagés" .-> Shared
```

- Au démarrage, `PluginCatalog` lit chaque sous-dossier du dossier des plugins (donné par le processus
  principal, jamais par l'utilisateur) : un manifeste valide, un identifiant unique égal au nom du
  dossier, une version du SDK acceptée, puis le chargement de la partie .NET dans **son** contexte avec
  **son** conteneur d'injection. Tout échec écarte ce plugin seul, avec une raison ; le service démarre
  toujours.
- Un plugin **enregistre** (`builder.AddDiagnosticFactSource<T>()`), il ne remplace rien ; l'hôte vérifie
  l'inscription contre `provides` et la range dans le registre.
- À la demande de l'interface, le collecteur interroge toutes les sources en parallèle, chacune sous un
  délai et un `try/catch` : une source qui échoue, tarde ou renvoie des faits refusés est signalée, les
  autres s'affichent.

## Les plugins dans l'interface

Côté interface, la page charge ensuite la partie interface de chaque plugin chargé :

```mermaid
flowchart LR
    Page["Page de l'interface<br/>voyager://app/index.html<br/>(CSP : script-src 'self' + empreinte de l'import map)"]
    Map["Import map en ligne<br/>react, react-dom, Fluent UI, @fluent/react,<br/>@voyager/plugin-sdk → shared/*.js"]
    Shared["shared/*.js<br/>entrées de la même construction que la page :<br/>une seule copie de React"]
    Module["plugins/&lt;id&gt;/ui/index.js<br/>import() d'un module du plugin"]
    Texts["plugins/&lt;id&gt;/i18n/&lt;langue&gt;/messages.ftl"]
    Page --> Map --> Shared
    Page -->|"import()"| Module
    Module -->|"imports nus, résolus par la carte"| Shared
    Page -->|"fetch"| Texts
```

- Le processus principal sert `plugins/<id>/ui/*.js` et `plugins/<id>/i18n/<langue>/messages.ftl` depuis le
  dossier des plugins (jamais le manifeste ni les assemblies), et calcule l'empreinte de l'import map de
  `index.html` pour la CSP. En développement, Vite fait de même.
- Le module d'un plugin exporte `register(registry)` ; l'interface refuse ce que le manifeste n'annonce pas.
  Un module qui ne charge pas, ou un panneau qui plante, met ce plugin « en erreur » sans toucher au reste.
- Les messages d'un plugin commencent par son identifiant ; ils rejoignent ceux de la page sans jamais en
  remplacer un.

## Une saisie hors-ligne, pas à pas

```mermaid
sequenceDiagram
    actor M as Membre
    participant UI as Interface
    participant L as Service local
    participant DB as SQLite
    participant S as Serveur du groupe
    M->>UI: Saisit une dépense (sans réseau)
    UI->>L: gRPC-Web : AddExpense
    L->>L: Voyager.Core valide et calcule les parts
    L->>DB: Enregistre (identifiant UUID v7)
    L-->>UI: Dépense et budget mis à jour
    Note over L,S: Le réseau revient
    L->>S: gRPC : envoie les changements en attente
    S->>S: Fusionne (règles de l'ADR de synchronisation)
    S-->>L: Changements des autres membres
    L->>DB: Applique
    L-->>UI: Rafraîchit les vues
```

## Ce qui reste à décider

- Plugins de tiers : isolation, signature, installation par l'utilisateur, rechargement à chaud (ADR à venir, [ADR-0015](../adr/0015-mecanisme-de-plugins.md)).
- Synchronisation : résolution des conflits, suppressions, historique (ADR à venir).
- Hébergement du serveur du groupe (Q4), fournisseur de cartes (Q5), format d'échange (Q16).
