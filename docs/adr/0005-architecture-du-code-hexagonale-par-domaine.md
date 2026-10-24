# ADR-0005 — Architecture du code : hexagonale, rangée par domaine

- **Statut** : Acceptée
- **Date** : 2026-10-24
- **Remplace** : —

## Contexte

L'[ADR-0003](0003-choix-de-la-stack.md) partage le code métier C# (`Voyager.Core`) entre le service
local et le serveur du groupe, et l'[ADR-0002](0002-architecture-modulaire-par-contrats-et-plugins.md)
veut un cœur minimal entouré de plugins. Il faut décider comment ce code est structuré et rangé.

L'architecture hexagonale (ports et adaptateurs) et la Clean Architecture appliquent la même règle :
**les dépendances pointent vers le métier**. Elles diffèrent surtout par le rangement des fichiers.

## Options

### Option A — Clean Architecture rangée par couche technique

`Domain/`, `Application/`, `Infrastructure/`, `Presentation/`, chaque métier réparti entre les couches.

- Avantages : très connue, nombreux modèles de projets.
- Inconvénients : travailler sur un sujet (le budget) oblige à naviguer dans toutes les couches ; un
  plugin, qui ajoute du métier, ne trouve pas de place naturelle.

### Option B — Hexagonale, rangée par domaine

Le cœur est découpé par domaine métier (`Trips`, `Catalog`, `Quotes`, `Bookings`, `Budget`). Chaque
domaine contient son modèle, ses cas d'usage et ses **ports** (interfaces). La technique (EF Core,
gRPC, synchronisation, chargement des plugins) est faite d'**adaptateurs** dans des projets séparés.

- Avantages : tout ce qui concerne un sujet est au même endroit ; le cœur reste testable sans base
  ni réseau ; s'aligne avec l'ADR-0002.
- Inconvénients : demande de la discipline pour que les domaines ne dépendent pas tous les uns des
  autres.

### Option C — Vertical slices

Un dossier par fonctionnalité (requête, traitement, accès aux données ensemble).

- Avantages : très direct, peu d'abstractions.
- Inconvénients : le code d'accès aux données est mélangé au métier ; incompatible avec un cœur
  partagé entre le service local (SQLite) et le serveur (PostgreSQL).

## Décision

**Option B**, choisie par l'utilisateur. Règles :

1. `Voyager.Core` ne dépend d'aucune bibliothèque technique (ni EF Core, ni gRPC, ni ASP.NET Core).
   Seules les bibliothèques de base listées par l'[ADR-0006](0006-dependances-principales.md)
   (NodaTime) y sont autorisées.
2. Les domaines communiquent par leurs types publics ; un domaine n'accède jamais aux détails
   internes d'un autre. Les dépendances entre domaines vont dans un seul sens (pas de cycle).
3. Les ports (interfaces) sont déclarés dans le domaine qui en a besoin ; les adaptateurs les
   implémentent dans les projets d'infrastructure.
4. Ces règles sont vérifiées par des **tests d'architecture** automatiques.

Le détail du rangement est dans [organisation du code](../conventions/organisation-du-code.md).

## Conséquences

- Les tests du cœur n'ont besoin ni de base de données ni de réseau : ils sont rapides et nombreux.
- Ajouter un domaine ou un plugin ne touche pas aux autres domaines.
- Les tests d'architecture font échouer le build si une dépendance interdite apparaît.
