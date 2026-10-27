# Organisation du code

> En place depuis l'étape 1 du squelette ; l'étape 2 a rempli le service local, l'interface et
> l'application de bureau pour l'écran « À propos ». Découle des ADR
> [0003](../adr/0003-choix-de-la-stack.md), [0004](../adr/0004-stockage-des-donnees.md) et
> [0005](../adr/0005-architecture-du-code-hexagonale-par-domaine.md).

## Le dépôt

Un seul dépôt (*monorepo*) pour tout : .NET, TypeScript, contrats et plugins.

```text
Voyager/
├─ Voyager.slnx                 solution .NET (format .slnx)
├─ global.json                  version du SDK .NET, lanceur de tests, version de MSTest.Sdk
├─ nuget.config                 seule source de paquets : nuget.org
├─ Directory.Build.props        réglages communs à tous les projets .NET (rigueur, analyseurs)
├─ Directory.Packages.props     versions centralisées des paquets NuGet
├─ package.json                 racine de l'espace de travail pnpm (scripts communs)
├─ pnpm-workspace.yaml          liste des paquets TypeScript, délai minimal avant d'installer une version
├─ tsconfig.base.json           réglages TypeScript communs (strict)
├─ eslint.config.js             règles ESLint communes
├─ .prettierrc.json             réglages Prettier
├─ .nvmrc                       version de Node.js
├─ buf.yaml                     lint et contrôle de compatibilité des contrats .proto
├─ GitVersion.yml               calcul des numéros de version (ADR-0007)
├─ proto/                       contrats gRPC : la source de vérité
│  └─ voyager/<domaine>/v1/*.proto
├─ src/
│  ├─ dotnet/                   projets .NET
│  └─ ts/                       paquets TypeScript
├─ plugins/                     plugins livrés avec Voyager (un dossier par plugin)
├─ tests/                       tests .NET (un projet de test par projet testé)
├─ deploy/                      image Docker du serveur du groupe (GroupServer.Dockerfile)
├─ tools/                       scripts de développement
└─ docs/                        documentation (vision, ADR, specs, conventions)
```

## Les projets .NET (`src/dotnet/`)

| Projet                           | Rôle                                                                                                                                                                           | Peut dépendre de                              |
| -------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------- |
| `Voyager.Core`                   | Le métier : domaines, cas d'usage, ports. Aucune dépendance technique                                                                                                          | BCL .NET et NodaTime (ADR-0006)               |
| `Voyager.Sdk`                    | Les points d'extension .NET que les plugins implémentent ; publié comme paquet versionné                                                                                       | `Voyager.Core`                                |
| `Voyager.Contracts`              | Code C# généré depuis `proto/`                                                                                                                                                 | bibliothèques gRPC                            |
| `Voyager.Persistence`            | Adaptateur EF Core commun : modèle de persistance, implémentation des ports de stockage                                                                                        | `Voyager.Core`, EF Core                       |
| `Voyager.Persistence.Sqlite`     | Fournisseur et migrations SQLite (service local)                                                                                                                               | `Voyager.Persistence`                         |
| `Voyager.Persistence.PostgreSql` | Fournisseur et migrations PostgreSQL + PostGIS (serveur)                                                                                                                       | `Voyager.Persistence`                         |
| `Voyager.Sync`                   | Moteur de synchronisation entre service local et serveur                                                                                                                       | `Voyager.Core`, `Voyager.Contracts`           |
| `Voyager.PluginHost`             | Découverte, chargement et cycle de vie des plugins                                                                                                                             | `Voyager.Sdk`                                 |
| `Voyager.LocalService`           | Hôte ASP.NET Core du service local (gRPC-Web, SQLite)                                                                                                                          | tous les adaptateurs                          |
| `Voyager.GroupServer`            | Hôte ASP.NET Core du serveur du groupe (gRPC, PostgreSQL)                                                                                                                      | tous les adaptateurs                          |
| `Voyager.ServiceDefaults`        | Réglages techniques communs aux hôtes : télémétrie OpenTelemetry, journaux dans Seq en développement, points de santé, résilience HTTP, adresses d'écoute (ADR-0011, ADR-0012) | paquets techniques uniquement                 |
| `Voyager.AppHost`                | Orchestration Aspire, **en développement uniquement** : PostgreSQL + PostGIS, hôtes, tableau de bord (ADR-0011)                                                                | les deux hôtes ; aucun projet ne le référence |

Règle d'or : **les flèches vont vers `Voyager.Core`**, jamais dans l'autre sens. Les hôtes
(`LocalService`, `GroupServer`) sont les seuls à tout assembler.

### L'intérieur de `Voyager.Core`

Rangé par domaine (ADR-0005). Les noms suivent le [glossaire](../glossaire.md).

```text
Voyager.Core/
├─ Shared/        types transverses : Money, Currency, GeoPoint, identifiants, Result
├─ Trips/         voyage, membres, voyageurs, envies
├─ Catalog/       prestations, tarifs, bases de calcul, lieux, transports
├─ Quotes/        devis, jours, créneaux, lignes, vote, faisabilité
├─ Bookings/      réservations, engagements, compatibilité
└─ Budget/        montants, parts, paiements, remboursements
```

Dans chaque domaine :

```text
Quotes/
├─ Quote.cs                  entité (racine d'agrégat)
├─ QuoteLine.cs              entité ou objet-valeur du domaine
├─ QuoteId.cs                identifiant fortement typé
├─ IQuoteRepository.cs       port : ce dont le domaine a besoin pour stocker
└─ UseCases/
   ├─ CreateQuote.cs         un cas d'usage = une classe (commande + traitement)
   └─ CompareQuotes.cs
```

Dépendances entre domaines, dans un seul sens (« A → B » : A utilise B). Les domaines sont rangés
dans l'ordre `Shared`, `Catalog` / `Trips`, `Quotes`, `Bookings`, `Budget` : un domaine peut utiliser
ceux qui le précèdent, jamais ceux qui le suivent. Les besoins prévus aujourd'hui :

- `Catalog` → `Shared` (le catalogue est une donnée de référence : il ne connaît aucun voyage) ;
- `Trips` → `Shared` ;
- `Quotes` → `Trips`, `Catalog` ;
- `Bookings` → `Quotes` ;
- `Budget` → `Bookings`, `Quotes`.

Un besoin qui casserait ce sens se règle par un port, pas par une référence directe.

## Les paquets TypeScript (`src/ts/`)

| Paquet        | Rôle                                                                                                          |
| ------------- | ------------------------------------------------------------------------------------------------------------- |
| `desktop`     | Application Electron : processus principal, script `preload`, lancement du service local, gestion des plugins |
| `ui`          | Interface React : coquille, navigation, écrans des fonctionnalités de base                                    |
| `plugin-sdk`  | `@voyager/plugin-sdk` : registre des points d'extension d'interface, composants et client fournis aux plugins |
| `grpc-client` | Client TypeScript généré depuis `proto/`                                                                      |

Dans `ui`, rangement par fonctionnalité, en miroir des domaines :

```text
ui/src/
├─ app/           coquille, routes, fournisseurs (thème, données, i18n)
├─ features/
│  ├─ trips/
│  ├─ quotes/     QuoteList.tsx, QuoteCard.tsx, useQuotes.ts…
│  └─ budget/
├─ components/    composants réutilisables sans métier
└─ i18n/          messages Fluent : fr/*.ftl, en/*.ftl (ADR-0008)
```

Dans `desktop`, un dossier par processus d'Electron :

```text
desktop/src/
├─ main/          processus principal : fenêtre, protocole voyager://, lancement du service local,
│                 verrouillage (navigation, permissions)
├─ preload/       script preload : la seule porte entre l'interface et Electron (contextBridge)
└─ shared/        ce que les deux partagent : noms des canaux IPC
```

Les adaptateurs gRPC d'un hôte .NET (services générés depuis `proto/`, intercepteurs) sont rangés
dans son dossier `Api/` (`Voyager.LocalService/Api/`).

## Les plugins (`plugins/`)

Chaque plugin a la structure décrite dans l'ADR-0003 :

```text
plugins/lodging/
├─ plugin.json
├─ proto/voyager/plugins/lodging/v1/*.proto
├─ dotnet/Voyager.Plugins.Lodging/      référence Voyager.Sdk uniquement
├─ ui/                                  référence @voyager/plugin-sdk uniquement
└─ tests/Voyager.Plugins.Lodging.Tests/
```

Un plugin ne référence **jamais** `Voyager.Core` directement ni les projets d'infrastructure : il
passe par le SDK. C'est ce qui garantit que les plugins de base et ceux écrits plus tard sont égaux.

## Les tests (`tests/`)

Un projet de test par projet testé, avec le suffixe `.Tests` :
`tests/Voyager.Core.Tests`, `tests/Voyager.Persistence.Tests`… plus `tests/Voyager.Architecture.Tests`
pour les règles de dépendance. Les tests TypeScript sont à côté du code (`QuoteCard.test.tsx`).
Détails : [tests](tests.md).

`tests/plugin-fixtures/` contient de petits plugins qui n'existent que pour tester l'hôte de plugins
(`tests/Voyager.PluginHost.Tests`) : un plugin conforme, un par défaut (exception au chargement, source
lente, conteneur invalide, inscription non déclarée…). Ils ne sont jamais livrés ; chacun s'appelle
`Voyager.PluginFixtures.<Nom>` et ne référence que `Voyager.Sdk`.
