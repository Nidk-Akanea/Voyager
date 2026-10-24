# ADR-0006 — Dépendances principales

- **Statut** : Acceptée
- **Date** : 2026-10-24
- **Remplace** : —

## Contexte

Avant de créer le squelette, il faut fixer les bibliothèques de base : injection, journalisation,
dates, accès aux données, gRPC, résilience, tests, interface. Chaque dépendance est un engagement :
licence, maintenance, apprentissage, et pour les plugins, API à connaître.

Principes retenus :

1. **D'abord ce que fournit .NET** ; une bibliothèque externe seulement si elle apporte nettement plus.
2. **Pas de licence payante ou restrictive.** En 2025, MediatR, AutoMapper et FluentAssertions
   (à partir de la version 8) sont passées sous licence commerciale.
3. `Voyager.Core` et `Voyager.Sdk` n'acceptent que les dépendances listées ici.

## Options discutées

- **Injection** : conteneur Microsoft ou SimpleInjector. SimpleInjector vérifie très strictement sa
  configuration, mais avec ASP.NET Core il s'ajoute à côté du conteneur Microsoft au lieu de le
  remplacer (EF Core, gRPC, cache et configuration s'y enregistrent nativement), et les auteurs de
  plugins devraient apprendre son API. Le conteneur Microsoft, avec `ValidateOnBuild` et
  `ValidateScopes`, couvre l'essentiel de la vérification. → **Microsoft**.
- **Dates** : types .NET (`DateOnly`, `TimeZoneInfo`) ou NodaTime. NodaTime distingue proprement date
  de calendrier, instant et date-heure dans un fuseau, et embarque sa propre base IANA : résultat
  identique sous Windows et Linux. → **NodaTime**.
- **Correspondances** (domaine ↔ proto ↔ persistance) : AutoMapper, Mapperly ou code manuel. Le code
  manuel est explicite, vérifié par le compilateur, et rapide à écrire avec l'aide de l'IA.
  → **manuel**.
- **Assertions** : MSTest, FluentAssertions 8 (gratuite seulement en non commercial), 7 (figée) ou
  AwesomeAssertions (fork maintenu de la 7, même API, Apache 2.0). → **AwesomeAssertions**.

## Décision

### .NET

| Besoin                      | Choix                                                                                                                       | Où                   |
| --------------------------- | --------------------------------------------------------------------------------------------------------------------------- | -------------------- |
| Injection de dépendances    | `Microsoft.Extensions.DependencyInjection`, `ValidateOnBuild` et `ValidateScopes` activés dans tous les environnements      | hôtes, plugins       |
| Configuration               | `Microsoft.Extensions.Configuration` + `Options`, validées au démarrage                                                     | hôtes                |
| Dates, heures, fuseaux      | **NodaTime** (`LocalDate`, `Instant`, `ZonedDateTime`, `DateTimeZone`, `IClock`)                                            | **Core** et partout  |
| Journalisation (API)        | `Microsoft.Extensions.Logging` (`ILogger<T>`)                                                                               | partout sauf Core    |
| Journalisation (écriture)   | **Serilog** : fichiers tournants sur le poste, JSON sur le serveur                                                          | hôtes uniquement     |
| Observabilité (plus tard)   | OpenTelemetry                                                                                                               | serveur du groupe    |
| Accès aux données           | EF Core ; `Npgsql.EntityFrameworkCore.PostgreSQL` (+ NetTopologySuite, + NodaTime) ; `Microsoft.EntityFrameworkCore.Sqlite` | persistance          |
| gRPC                        | `Grpc.AspNetCore`, `Grpc.AspNetCore.Web`, `Google.Api.CommonProtos` ; génération par Buf                                    | contrats, hôtes      |
| Résilience                  | **Polly v8**, via `Microsoft.Extensions.Http.Resilience` (appels HTTP) et `Microsoft.Extensions.Resilience` (le reste)      | adaptateurs          |
| Cache des API               | `HybridCache` (Microsoft)                                                                                                   | serveur du groupe    |
| Chargement des plugins      | `AssemblyLoadContext` (intégré)                                                                                             | `Voyager.PluginHost` |
| Extraction HTML (plus tard) | AngleSharp                                                                                                                  | connecteurs          |
| Correspondances             | code manuel                                                                                                                 | adaptateurs          |

`Voyager.Core` ne dépend que de la BCL et de **NodaTime**.

### Tests .NET

MSTest · **AwesomeAssertions** · NSubstitute (simulations) · Testcontainers (PostgreSQL) · NetArchTest
(architecture) · `NodaTime.Testing` (`FakeClock`).

### TypeScript

| Besoin                               | Choix                                                  |
| ------------------------------------ | ------------------------------------------------------ |
| Application, packaging, mises à jour | Electron + Electron Forge (avec Vite)                  |
| Interface                            | React + Fluent UI React v9                             |
| Navigation                           | TanStack Router                                        |
| Données                              | TanStack Query                                         |
| Client gRPC                          | Connect-ES (`@connectrpc/connect-web`), généré par Buf |
| Formulaires                          | React Hook Form                                        |
| Traductions                          | i18next + react-i18next                                |
| Carte                                | MapLibre GL JS + react-map-gl                          |
| Graphiques                           | Apache ECharts                                         |
| Timeline                             | vis-timeline                                           |
| Journaux                             | electron-log                                           |
| Tests                                | Vitest, Testing Library, Playwright                    |
| Qualité                              | ESLint, typescript-eslint, Prettier                    |

Le composant de calendrier (Schedule-X ou FullCalendar, dont certaines fonctions sont payantes) sera
choisi avec l'écran planning.

### Écartées

MediatR et AutoMapper (licence commerciale, et inutiles ici), FluentValidation (les règles vivent dans
le domaine et renvoient un `Result`), Moq (collecte de données controversée en 2023 ; NSubstitute à la
place), SimpleInjector (voir ci-dessus).

## Conséquences

- Toute nouvelle dépendance s'ajoute à cet ADR (par un nouvel ADR qui le remplace) si elle touche
  `Voyager.Core`, `Voyager.Sdk` ou le SDK d'interface ; ailleurs, on vérifie licence et maintenance.
- NodaTime traverse toutes les couches : les correspondances vers proto (`google.type.Date`,
  `Timestamp`) et vers SQLite (convertisseurs) sont à écrire une fois, dans les adaptateurs.
- Les versions exactes sont fixées dans `Directory.Packages.props` et `package.json` au moment du
  squelette, et mises à jour régulièrement.
