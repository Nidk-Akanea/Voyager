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
| Processus principal Electron | Fenêtre, mises à jour, installation des plugins, lancement et surveillance du service local | Electron, Node.js |
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

- Mécanisme de plugins : format du paquet, signature, chargement, rechargement (ADR à venir).
- Synchronisation : résolution des conflits, suppressions, historique (ADR à venir).
- Hébergement du serveur du groupe (Q4), fournisseur de cartes (Q5), format d'échange (Q16).
