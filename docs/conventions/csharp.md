# Conventions C\#

Base : les [conventions de codage C# de Microsoft](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions).
Ce document liste ce que Voyager précise ou impose en plus. Les règles de style vérifiables sont dans
`.editorconfig` et appliquées au build.

## Rigueur (imposée par les outils)

Réglages communs dans `Directory.Build.props` :

| Réglage                          | Valeur                                            |
| -------------------------------- | ------------------------------------------------- |
| `TargetFramework`                | `net10.0`                                         |
| `LangVersion`                    | `latest`                                          |
| `Nullable`                       | `enable`                                          |
| `ImplicitUsings`                 | `enable`                                          |
| `TreatWarningsAsErrors`          | `true`                                            |
| `AnalysisLevel`                  | `latest-recommended`                              |
| `EnforceCodeStyleInBuild`        | `true`                                            |
| `ManagePackageVersionsCentrally` | `true` (versions dans `Directory.Packages.props`) |

- **Un avertissement est une erreur.** On ne le désactive que localement (`#pragma` ou attribut), avec
  un commentaire qui explique pourquoi.
- `dotnet format --verify-no-changes` tourne en CI.

## Nommage

| Élément                       | Règle                                             | Exemple                                  |
| ----------------------------- | ------------------------------------------------- | ---------------------------------------- |
| Types, méthodes, propriétés   | PascalCase                                        | `QuoteLine`, `ComputeShares`             |
| Constantes (`const`)          | MAJUSCULES séparées par `_`                       | `MAX_TRAVELERS`, `DEFAULT_CURRENCY_CODE` |
| Interfaces                    | `I` + PascalCase                                  | `IQuoteRepository`                       |
| Paramètres, variables locales | camelCase                                         | `quoteId`                                |
| Champs privés                 | `_` + camelCase                                   | `_repository`                            |
| Méthodes asynchrones          | suffixe `Async`                                   | `SaveAsync`                              |
| Espaces de noms               | = chemin du dossier                               | `Voyager.Core.Quotes.UseCases`           |
| Fichiers                      | un type public par fichier, fichier = nom du type | `QuoteLine.cs`                           |

Les noms métier viennent du [glossaire](../glossaire.md) : on écrit `Quote`, pas `Estimate` ni
`Simulation`.

## Style

- Espaces de noms **au niveau du fichier** (`namespace Voyager.Core.Quotes;`).
- `using` en dehors de l'espace de noms, `System` en premier.
- Accolades toujours, même pour une seule ligne.
- `var` quand le type est évident à la lecture.
- Classes **`sealed` par défaut** ; on ouvre à l'héritage quand c'est voulu.
- Constructeurs principaux (*primary constructors*) pour l'injection de dépendances.
- `record` pour les objets-valeurs et les messages ; immutabilité par défaut.

## Types métier

- **Argent** : `Money` (objet-valeur : `decimal` + `Currency` ISO 4217). Jamais `double` ni `float`.
  Les opérations entre devises différentes sont interdites sans conversion explicite.
- **Dates et heures** : **NodaTime** ([ADR-0006](../adr/0006-dependances-principales.md)), jamais
  `DateTime` dans le métier :
  - date de calendrier (jour d'une étape) : `LocalDate` ;
  - instant (départ d'un vol) : `Instant`, accompagné du fuseau du lieu (`DateTimeZone`, identifiant
    IANA comme `Europe/Madrid`) ; `ZonedDateTime` pour l'afficher ou raisonner en heure locale ;
  - créneau dans une journée : `LocalTime` de début et de fin.
- **Heure courante** : toujours via `IClock` (NodaTime) injecté, jamais `DateTime.Now` ni
  `SystemClock.Instance` en dur : le code reste testable (`FakeClock`).
- **Identifiants** : fortement typés (`readonly record struct QuoteId(Guid Value)`), créés avec
  `Guid.CreateVersion7()` (UUID v7, ADR-0004).
- **Coordonnées** : `GeoPoint` (latitude, longitude en WGS 84).

## Erreurs

- **Échec métier attendu** (devis incompatible, tarif manquant, saisie invalide) : le cas d'usage
  renvoie un `Result` (type maison dans `Voyager.Core.Shared`) avec une erreur typée. Pas d'exception.
- **Bug ou panne technique** (base inaccessible, état impossible) : exception. On ne l'attrape qu'aux
  frontières (hôte gRPC, synchronisation) pour la journaliser et la traduire.
- Jamais de `catch` vide ; jamais de `catch (Exception)` hors des frontières.

## Asynchrone

- `async` de bout en bout ; jamais `.Result` ni `.Wait()`.
- `CancellationToken` en dernier paramètre de toute méthode asynchrone publique, et transmis.
- Le cœur métier est majoritairement synchrone : les calculs n'ont pas d'entrée-sortie.

## Injection de dépendances et état

- Conteneur Microsoft (`Microsoft.Extensions.DependencyInjection`), avec `ValidateOnBuild` et
  `ValidateScopes` activés dans **tous** les environnements : une erreur d'enregistrement bloque le
  démarrage.
- Dépendances par le constructeur uniquement ; pas de *service locator*.
- Aucun état statique modifiable.
- Pas de bibliothèque de médiation (MediatR & co.) : un cas d'usage est une classe injectée et
  appelée directement.
- Pas de bibliothèque de correspondance (AutoMapper & co.) : les conversions domaine ↔ proto ↔
  persistance sont écrites à la main, dans les adaptateurs.

## Journalisation

- Le code utilise `ILogger<T>` ; seuls les hôtes et `Voyager.ServiceDefaults`, qu'ils sont seuls à
  référencer, connaissent Serilog, qui écrit les journaux (ADR-0006, ADR-0012).
- `ILogger<T>` avec des messages structurés : `logger.LogInformation("Quote {QuoteId} accepted", id)`,
  jamais d'interpolation de chaîne dans le message.
- **Aucune donnée personnelle dans les journaux** : pas de noms, d'adresses, de références de
  réservation, de coordonnées précises. Des identifiants techniques seulement. La règle vaut aussi
  pour les **scopes** de journalisation, les **attributs de traces** et les **routes** : la télémétrie
  peut être envoyée hors de la machine (ADR-0011).

## Textes destinés à l'utilisateur

- `Voyager.Core` ne traduit rien : il renvoie des codes stables (`reason`) et des valeurs ; la
  traduction se fait à la frontière, dans les hôtes ou l'interface ([ADR-0008](../adr/0008-globalisation-fluent-et-linguini.md)).
- Côté .NET, les messages sont des fichiers Fluent (`.ftl`) en français **et** en anglais, lus par
  Linguini derrière l'abstraction `ILocalizer` des hôtes ; jamais de texte d'interface en dur.
- Nombres, montants et dates sont formatés selon la langue de l'utilisateur.

## Commentaires et documentation

- En anglais. Un commentaire explique **pourquoi**, pas ce que fait le code.
- Commentaires de documentation XML (`///`) obligatoires sur l'API publique de `Voyager.Sdk` : c'est
  ce que lisent les auteurs de plugins.
- Pas de code commenté laissé dans le dépôt.

## Dépendances externes

- La liste des dépendances autorisées est dans l'[ADR-0006](../adr/0006-dependances-principales.md).
- Toute nouvelle bibliothèque dans `Voyager.Core` ou `Voyager.Sdk` passe par un ADR ; un test
  d'architecture vérifie leur liste. Les outils de build privés communs à tous les projets
  (`GitVersion.MsBuild` dans `Directory.Build.props`) ne sont pas des dépendances d'exécution et n'y
  comptent pas.
- Ailleurs, on vérifie la licence (pas de licence commerciale imposée, pas de copyleft fort) et la
  maintenance du projet avant de l'ajouter.
