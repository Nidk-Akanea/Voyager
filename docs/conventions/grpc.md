# Conventions gRPC et Protocol Buffers

Les fichiers `.proto` sont les **contrats** de l'[ADR-0002](../adr/0002-architecture-modulaire-par-contrats-et-plugins.md) :
le code C# et TypeScript en est généré. Ils se relisent avec autant de soin que du code public.

Base : le [guide de style Protocol Buffers](https://protobuf.dev/programming-guides/style/) et les
règles par défaut de **Buf**, qui vérifie le lint et la compatibilité en CI.

## Rangement et paquets

```text
proto/voyager/quotes/v1/quotes.proto            package voyager.quotes.v1;
proto/voyager/budget/v1/budget.proto            package voyager.budget.v1;
plugins/lodging/proto/voyager/plugins/lodging/v1/lodging.proto
                                                package voyager.plugins.lodging.v1;
```

- Un paquet par domaine et par version majeure. Les services techniques, sans domaine métier, ont
  aussi le leur : `voyager.diagnostics.v1` (écran « À propos »).
- `option csharp_namespace = "Voyager.Contracts.Quotes.V1";`

## Nommage

| Élément           | Règle                                                            | Exemple                                            |
| ----------------- | ---------------------------------------------------------------- | -------------------------------------------------- |
| Services          | PascalCase + `Service`                                           | `QuoteService`                                     |
| Méthodes          | PascalCase, verbe + nom                                          | `CreateQuote`, `ListQuotes`                        |
| Messages          | PascalCase                                                       | `QuoteLine`                                        |
| Requête / réponse | une paire dédiée par méthode                                     | `CreateQuoteRequest`, `CreateQuoteResponse`        |
| Champs            | snake_case                                                       | `quote_id`, `planned_amount`                       |
| Énumérations      | PascalCase ; valeurs en MAJUSCULES préfixées, `0` = non spécifié | `COST_TYPE_UNSPECIFIED = 0; COST_TYPE_DIRECT = 1;` |

## Types communs

| Donnée             | Type proto                                                                   |
| ------------------ | ---------------------------------------------------------------------------- |
| Montant            | `google.type.Money` (code devise, unités, nanos) — exact, jamais de `double` |
| Date de calendrier | `google.type.Date`                                                           |
| Instant            | `google.protobuf.Timestamp` + champ `string time_zone` (identifiant IANA)    |
| Identifiant        | `string` contenant un UUID                                                   |
| Coordonnées        | `google.type.LatLng`                                                         |

Un instant purement technique, qui ne se rattache à aucun lieu (l'heure de démarrage d'un service),
se passe de `time_zone`.

Côté C#, les adaptateurs convertissent ces types vers le domaine : `google.type.Date` ↔ `LocalDate`,
`Timestamp` ↔ `Instant`, `time_zone` ↔ `DateTimeZone` (NodaTime), `google.type.Money` ↔ `Money`.

## Compatibilité et versions

- **On ne renumérote jamais un champ** et on ne réutilise jamais un numéro : un champ supprimé est
  déclaré `reserved` (numéro et nom).
- Ajouter un champ ou une méthode est compatible. Renommer, changer un type ou supprimer ne l'est
  pas : cela crée une **nouvelle version** du paquet (`v2`), l'ancienne reste servie le temps de la
  transition.
- `buf breaking` compare chaque PR à sa branche cible (`develop`, ADR-0007) et bloque toute rupture
  non assumée.

## Code généré

- Le code généré depuis `proto/` **n'est pas committé** : il est produit au build (C#) et par
  `buf generate` (TypeScript, dossiers `src/gen/` ignorés par git), en local comme en CI, avant la
  vérification des types.
- Le premier contrat qui utilise les types Google (`google.type.Money`…) ajoute
  `buf.build/googleapis/googleapis` aux dépendances de `buf.yaml`.

## Erreurs

- Échec métier : code de statut gRPC adapté + détail structuré (`google.rpc.ErrorInfo`, avec une
  `reason` stable en MAJUSCULES, ex. `QUOTE_INCOMPATIBLE_WITH_COMMITMENT`).

| Situation                                                   | Code                       |
| ----------------------------------------------------------- | -------------------------- |
| Saisie invalide                                             | `INVALID_ARGUMENT`         |
| Objet introuvable                                           | `NOT_FOUND`                |
| Règle métier non respectée (devis incompatible, engagement) | `FAILED_PRECONDITION`      |
| Conflit de modification concurrente                         | `ABORTED`                  |
| Panne technique                                             | `INTERNAL` / `UNAVAILABLE` |

- Le message d'erreur est destiné aux développeurs (anglais) ; l'interface affiche un texte traduit à
  partir de la `reason`.

## Documentation

Chaque service, méthode et message a un commentaire `//` en anglais qui dit à quoi il sert : c'est
la documentation des auteurs de plugins.
