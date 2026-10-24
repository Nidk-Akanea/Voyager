---
paths:
  - "src/dotnet/**"
  - "tests/**"
  - "plugins/*/dotnet/**"
  - "plugins/*/tests/**"
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.props"
---

# Code C\#

Référence complète : [conventions C#](../../docs/conventions/csharp.md),
[organisation du code](../../docs/conventions/organisation-du-code.md),
[tests](../../docs/conventions/tests.md). L'essentiel :

## Architecture (ADR-0002, 0005)

- Les dépendances pointent vers `Voyager.Core`. `Voyager.Core` ne dépend que de la BCL et de NodaTime :
  ni EF Core, ni gRPC, ni ASP.NET Core.
- `Voyager.Core` est rangé par domaine (`Trips`, `Catalog`, `Quotes`, `Bookings`, `Budget`, `Shared`),
  dans le sens de dépendance documenté. Un besoin qui casserait ce sens passe par un port.
- Un cas d'usage = une classe dans `<Domaine>/UseCases/`, injectée et appelée directement.
- Un plugin ne référence que `Voyager.Sdk`.
- Tout calcul métier est dans `Voyager.Core`, jamais dans un adaptateur ni dans l'interface.

## Écriture

- `TreatWarningsAsErrors` : n'ajoute pas de `#pragma` sans commentaire qui justifie.
- Espaces de noms au niveau du fichier, = chemin du dossier ; un type public par fichier.
- Classes `sealed` par défaut ; `record` pour les objets-valeurs ; constructeurs principaux pour l'injection.
- Constantes en `UPPER_SNAKE_CASE` ; champs privés en `_camelCase` ; suffixe `Async`.
- Noms métier tirés du [glossaire](../../docs/glossaire.md).

## Types

- Argent : `Money` (décimal + devise ISO 4217), jamais `double`/`float`.
- Temps : NodaTime — `LocalDate`, `Instant` + `DateTimeZone`, `LocalTime` ; heure via `IClock` injecté.
- Identifiants fortement typés, `Guid.CreateVersion7()`.
- Échec métier attendu → `Result` ; bug ou panne → exception, attrapée seulement aux frontières.

## Dépendances (ADR-0006)

Uniquement celles de l'[ADR-0006](../../docs/adr/0006-dependances-principales.md). Jamais MediatR,
AutoMapper, FluentValidation, Moq. Correspondances écrites à la main dans les adaptateurs.

## Journaux

`ILogger<T>`, messages structurés, **aucune donnée personnelle**. Seuls les hôtes connaissent Serilog.

## Tests

MSTest + AwesomeAssertions + NSubstitute ; `FakeClock` ; méthode `Behavior_Condition_ExpectedResult` ;
données toujours fictives (`Traveler A`, `Child C`). Pour écrire des tests, délègue à l'agent
`test-writer`.
