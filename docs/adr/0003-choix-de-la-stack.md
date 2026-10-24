# ADR-0003 — Choix de la stack

- **Statut** : Acceptée
- **Date** : 2026-10-24
- **Remplace** : —

## Contexte

Les réponses aux questions de cadrage fixent les contraintes :

- **Q1** — Voyager est une **application de bureau « intelligente »**, comme Teams ou le client Claude
  pour Windows : installée, avec sa propre fenêtre, mise à jour automatiquement. Pendant le voyage,
  on se sert de son **téléphone** *(à confirmer)*.
- **Q2** — Sans réseau, on doit pouvoir **consulter le programme et saisir les dépenses** ; tout se
  synchronise au retour du réseau.
- **Plusieurs membres** modifient le même voyage : il faut un **serveur partagé** par le groupe.
- **[ADR-0002](0002-architecture-modulaire-par-contrats-et-plugins.md)** — tout est plugin, y compris
  les vues. Un plugin apporte souvent **de la logique et de l'interface**.
- **Préférences de l'utilisateur** :
  - back-end en **C# / .NET** ;
  - communication en **RPC** (gRPC ou JSON-RPC), **pas en REST** ;
  - préférence pour l'option B ci-dessous pour le front-end.
- **Besoins d'interface riches** : carte, timeline, planning, graphiques.

Le back-end est donc **.NET 10 (LTS) et ASP.NET Core**. L'hébergement du serveur fera l'objet d'un
ADR séparé (Q4).

## Options

### Option A — Blazor Hybrid (.NET MAUI), tout en C\#

Interface en composants Razor (HTML/CSS + C#) dans une coquille native .NET MAUI (Windows, macOS,
Android, iOS).

- Avantages : un seul langage ; un plugin = un seul paquet .NET (logique + interface) ; même code sur
  bureau et téléphone ; hors-ligne naturel.
- Inconvénients : .NET MAUI a une réputation de bugs ; écosystème de composants plus petit (cartes et
  graphiques via surcouches JavaScript) ; un Mac est nécessaire pour iOS.

### Option B — Electron + React (TypeScript), services .NET

L'architecture du client Claude (Electron) et, à peu de chose près, de Teams (fenêtre native et
interface web React).

- Avantages :
  - le plus grand écosystème d'interface : cartes, timelines, calendriers, graphiques prêts à
    l'emploi ; Fluent UI React donne l'apparence de Teams ;
  - architecture éprouvée pour les applications à plugins : VS Code (Electron) et Grafana (interface
    React + plugins back-end en gRPC) suivent ce modèle ;
  - rendu identique sur toutes les plateformes de bureau.
- Inconvénients :
  - deux langages (C# et TypeScript) ;
  - un plugin contient deux parties (.NET et JavaScript), à versionner ensemble ;
  - sans précaution, la logique métier doit être dupliquée côté client pour le hors-ligne
    (voir la parade dans la décision : le service local) ;
  - Electron ne tourne pas sur téléphone ; il faudra une autre coquille pour le mobile ;
  - Electron est lourd (chaque application embarque Chromium).

### Option C — Avalonia UI, tout en C\# natif (XAML)

- Avantages : un seul langage, rendu natif performant, plugins entièrement .NET.
- Inconvénients : écosystème réduit pour les cartes, timelines et graphiques ; XAML à apprendre ;
  mobile moins mûr ; pas de version navigateur simple.

## Décision

**Option B**, choisie par l'utilisateur, organisée ainsi pour en neutraliser les inconvénients.

### Architecture

```text
┌───────────────────────── Poste du membre (Windows) ─────────────────────────┐
│  Application Electron                                                        │
│   ├─ Interface React + TypeScript (Fluent UI React)                          │
│   │    └─ modules d'interface des plugins (chargés dynamiquement)           │
│   │              │ gRPC-Web (localhost)                                      │
│   │              ▼                                                           │
│   └─ Service local .NET (lancé par Electron)                                 │
│        ├─ Voyager.Core : modèle, contrats, calculs (C#)                      │
│        ├─ base locale SQLite : hors-ligne, consultation et saisie            │
│        └─ parties .NET des plugins                                           │
└──────────────────────────────────│───────────────────────────────────────────┘
                                   │ gRPC (synchronisation quand le réseau est là)
                                   ▼
                 Serveur du groupe (ASP.NET Core) : données partagées, synchro
```

1. **Le service local** est un petit processus ASP.NET Core embarqué dans l'application et lancé par
   Electron. Il exécute le code métier C# (`Voyager.Core`), possède la base locale et se synchronise
   avec le serveur du groupe. **La logique métier n'est écrite qu'une fois, en C#**, et fonctionne
   hors-ligne. L'interface React ne fait qu'afficher et saisir.
2. **Le serveur du groupe** réutilise `Voyager.Core` ; il centralise les données et diffuse les
   changements aux membres. Il peut tourner **en local** (sur un poste, un NAS) **ou en ligne** :
   c'est le même logiciel, seul l'hébergement change (Q4).
3. **Electron** plutôt que Tauri pour le bureau : processus principal Node.js pratique pour gérer les
   plugins et lancer le service local, précédent de VS Code, maturité. Choix confirmé par l'utilisateur (Q18).
   Tauri (plus léger, utilise WebView2 comme Teams, mais ajoute Rust) reste une alternative si le
   poids devient un problème.

### Communication : gRPC

- Les contrats sont écrits en **Protocol Buffers** (`.proto`). Ce sont les contrats versionnés de
  l'ADR-0002 : le code C# et TypeScript est **généré** à partir d'eux, des deux côtés.
- L'interface appelle le service local en **gRPC-Web** (pris en charge par ASP.NET Core), avec un
  client TypeScript généré (Connect-ES ou grpc-web).
- Le service local et le serveur du groupe se parlent en **gRPC** natif (HTTP/2), avec flux serveur
  pour pousser les changements des autres membres.
- JSON-RPC (par exemple StreamJsonRpc, utilisé par Visual Studio) a été écarté : plus simple, mais
  sans génération de code ni contrat strict partagé entre C# et TypeScript, ce qui affaiblit
  l'ADR-0002.

### Plugins : comment un plugin étend aussi l'interface

Un plugin est un paquet unique qui contient ses deux parties :

```text
voyager-plugin-hotel/
  plugin.json        manifeste : identifiant, version, versions de contrats compatibles,
                     points d'extension fournis
  contracts/*.proto  services gRPC propres au plugin (facultatif)
  dotnet/            assembly .NET : logique, chargée par le service local (et le serveur)
  ui/index.js        module JavaScript (ES module) : composants React
```

- L'application fournit un **SDK d'interface** (`@voyager/plugin-sdk`) : React, Fluent UI, le client
  gRPC et un **registre** des points d'extension. Les plugins utilisent la copie de React fournie par
  l'hôte (une seule instance en mémoire).
- Au démarrage, ou à l'installation, l'hôte lit les manifestes, charge la partie .NET dans le service
  local, puis charge le module d'interface par un `import()` dynamique.
- Le module s'enregistre auprès du registre : « je fournis la fiche d'une prestation de type hôtel »,
  « je fournis une vue carte », etc. L'interface de l'hôte affiche ce que le registre contient.
- Installer ou mettre à jour un plugin = déposer le nouveau paquet, puis recharger. Le rechargement à
  chaud est possible, mais redémarrer l'application suffit au début.

### Bibliothèques d'interface pressenties

Fluent UI React (apparence Teams), MapLibre GL (carte), Apache ECharts (graphiques), vis-timeline
(timeline). Licences à vérifier au moment du choix définitif de chacune.

## Conséquences

- Deux langages : C# pour toute la logique, TypeScript pour l'interface. La règle est stricte :
  **aucun calcul métier dans l'interface**.
- Les fichiers `.proto` deviennent le cœur des contrats : ils sont versionnés, relus avec soin, et
  toute évolution incompatible crée une nouvelle version de service.
- Le chargement d'assemblies .NET et de modules JavaScript **n'isole pas** un plugin malveillant :
  acceptable tant que seuls l'utilisateur et ses proches en écrivent ; à revoir si des tiers doivent
  en publier (Q11), dans l'ADR sur le mécanisme de plugins. Electron sera configuré avec
  `contextIsolation` activé et sans accès Node.js dans l'interface.
- L'application de bureau peut afficher une page web (Booking…) dans une vue intégrée et en extraire
  les informations pendant que l'utilisateur la consulte : une réponse naturelle à Q15.
- **Le téléphone est reporté** (Q19) : Electron ne tourne pas sur mobile. Piste principale :
  une application Capacitor qui réutilise les composants React, limitée à la consultation et à la
  saisie des dépenses, avec un stockage local et une synchronisation directe avec le serveur du
  groupe.
- ADR à suivre : hébergement du serveur (Q4), stockage et synchronisation, mécanisme de plugins
  (packaging, chargement, signature), format d'échange (Q16), application mobile (Q19).
- Quand cet ADR sera accepté : squelette de solution (.NET + Electron/React), build, tests, CI, et mise
  à jour de `CLAUDE.md` (section Commandes), `.editorconfig` et `.gitignore`.
