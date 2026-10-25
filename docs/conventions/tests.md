# Conventions de tests

## Outils

| Partie          | Outils                                                                                                       |
| --------------- | ------------------------------------------------------------------------------------------------------------ |
| .NET            | **MSTest** (via `MSTest.Sdk`), lancé par Microsoft.Testing.Platform : `dotnet test --solution Voyager.slnx`  |
| Assertions      | **AwesomeAssertions** (`result.Should().Be(…)`)                                                              |
| Simulations     | NSubstitute                                                                                                  |
| Heure           | `FakeClock` (`NodaTime.Testing`)                                                                             |
| Base de données | SQLite en mémoire (un fichier temporaire quand le test porte sur le fichier) ; PostgreSQL via Testcontainers |
| gRPC            | `WebApplicationFactory` (hôte ASP.NET Core en mémoire)                                                       |
| Architecture    | NetArchTest : règles de dépendance des ADR 0002 et 0005                                                      |
| TypeScript      | Vitest + Testing Library                                                                                     |
| Bout en bout    | Playwright pour Electron (plus tard)                                                                         |

AwesomeAssertions est le fork maintenu et gratuit de FluentAssertions 7, avec la même syntaxe ;
FluentAssertions 8 n'est gratuite qu'en usage non commercial ([ADR-0006](../adr/0006-dependances-principales.md)).

## Ce qu'on teste, et où

| Niveau       | Cible                                                                                                    | Quantité                                  |
| ------------ | -------------------------------------------------------------------------------------------------------- | ----------------------------------------- |
| Unitaire     | `Voyager.Core` : calculs de coûts, bases de calcul, répartition, remboursements, faisabilité, vote       | **Beaucoup** : c'est le cœur de la valeur |
| Intégration  | Adaptateurs : persistance (SQLite et PostgreSQL), synchronisation, services gRPC, chargement des plugins | Les chemins principaux et les cas limites |
| Architecture | Règles de dépendance entre projets et domaines                                                           | Une règle par interdiction                |
| Interface    | Composants qui ont de la logique d'affichage                                                             | Ciblé                                     |
| Bout en bout | Parcours critiques (créer un voyage, comparer des devis, saisir une dépense hors-ligne)                  | Quelques-uns                              |

- **Pas d'objectif chiffré de couverture**, mais tout calcul de `Voyager.Core` est testé.
- **Chaque bug corrigé arrive avec le test** qui l'aurait détecté.
- **Traductions** : un test vérifie que chaque message Fluent existe en français et en anglais, avec
  les mêmes variables (ADR-0008).
- Les cas limites du métier sont testés explicitement : arrondis, plusieurs devises, changement de
  fuseau, enfant à tarif réduit, devis incompatible avec un engagement.

## Nommage et structure

- Classe de test : `<ClasseTestée>Tests` (`ShareCalculatorTests`).
- Méthode : `Behavior_Condition_ExpectedResult`, en anglais.
  Exemple : `Split_RoomForThreeTravelers_SharesCostEqually`.
- Corps en trois blocs séparés par une ligne vide : préparation, action, vérification
  (*Arrange / Act / Assert*), sans commentaires `// Arrange` superflus.
- Un comportement par test.

## Données de test

- **Toujours fictives** : voyageurs `Traveler A`, `Traveler B`, `Child C` ; lieux et prix réalistes
  mais inventés. Jamais de vraies personnes, réservations ou coordonnées personnelles.
- Des constructeurs de données de test (*builders*) par domaine (`QuoteBuilder`) plutôt que des objets
  recopiés partout.
- L'heure est fixée avec un `FakeClock` ; aucun test ne dépend de l'heure réelle.

## En CI

Tous les tests .NET et TypeScript tournent sur chaque PR ; un test qui échoue bloque la fusion. Un
test instable (*flaky*) est corrigé ou supprimé, jamais ignoré.
