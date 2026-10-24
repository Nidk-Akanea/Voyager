# ADR-0004 — Stockage des données

- **Statut** : Acceptée
- **Date** : 2026-10-24
- **Remplace** : —

## Contexte

L'[ADR-0003](0003-choix-de-la-stack.md) place les données à **deux endroits** :

- **le serveur du groupe** (ASP.NET Core), qui centralise les voyages et les données de référence du
  groupe. Il peut tourner en local (un poste, un NAS) ou en ligne ;
- **le service local** de chaque poste, qui garde une copie pour travailler hors-ligne (Q2) et se
  synchronise avec le serveur.

Besoins propres à Voyager :

- des données **relationnelles** : voyages, devis, lignes, tarifs, paiements, parts, remboursements ;
- des données **extensibles** : chaque plugin ajoute ses propres informations (ADR-0002) ;
- des données **géographiques** : lieux, hiérarchie, distances, « ce qui est près de mon hôtel » ;
- des **montants exacts** (décimaux, jamais de flottants) et des **dates avec fuseau horaire** ;
- une **création hors-ligne** sur plusieurs postes, sans collision d'identifiants.

L'utilisateur propose **PostgreSQL**.

## Options

### Serveur du groupe

#### Option S1 — PostgreSQL

- Avantages :
  - libre et gratuit, sans limite de taille, très robuste ;
  - **JSONB** : stocker et interroger efficacement les données propres aux plugins ;
  - **PostGIS** : la référence pour les données géographiques (distances, proximité, zones) ;
  - fournisseur Entity Framework Core (Npgsql) très mûr ;
  - s'installe partout : poste, NAS, Docker, ou en service managé chez tous les hébergeurs.
- Inconvénients : un serveur de base à installer et à administrer quand le groupe s'auto-héberge
  (atténué par Docker).

#### Option S2 — SQL Server

- Avantages : naturel dans l'écosystème .NET, outillage Visual Studio, support géographique intégré.
- Inconvénients : licence payante au-delà de l'édition Express (limitée à 10 Go et à des ressources
  réduites) ; plus lourd à auto-héberger ; JSON moins bien intégré que JSONB.

#### Option S3 — SQLite aussi sur le serveur

- Avantages : aucun serveur de base à installer ; parfait pour un usage solo.
- Inconvénients : écritures concurrentes limitées ; pas de géographie avancée sans extension ; peu
  adapté si le serveur est en ligne et utilisé par plusieurs membres à la fois.

### Service local (hors-ligne)

Une base **embarquée** est indispensable : on n'installe pas PostgreSQL sur le poste de chaque
membre. **SQLite** s'impose (fiable, un seul fichier, fournisseur EF Core officiel). LiteDB ou une
base documentaire embarquée ont été écartés : le modèle est relationnel.

## Décision

**PostgreSQL sur le serveur du groupe, SQLite dans le service local.**

1. **Serveur** : PostgreSQL (dernière version stable) avec l'extension **PostGIS**. Les données
   propres aux plugins vont dans des colonnes **JSONB** ou dans des tables du plugin, selon le
   mécanisme de plugins à définir.
2. **Local** : SQLite, une base par poste, qui contient les voyages du membre et les données de
   référence utiles.
3. **Accès aux données** : Entity Framework Core avec les deux fournisseurs (Npgsql et SQLite).
   Le modèle est commun (`Voyager.Core`) ; chaque base a ses propres migrations.
4. **Règles communes aux deux bases** :
   - **identifiants UUID v7**, générés par le client : un membre peut créer un devis hors-ligne
     sans risque de collision avec un autre membre ;
   - **montants** en décimal (`numeric` côté PostgreSQL ; converti sans perte côté SQLite) avec leur
     code devise ;
   - **instants** en UTC avec l'identifiant du fuseau du lieu ; **dates de calendrier** sans heure ;
   - **calculs métier en C#** (`Voyager.Core`), jamais dans des requêtes propres à une base : ils
     doivent donner le même résultat en ligne et hors-ligne. Les calculs géographiques côté client
     utilisent NetTopologySuite.
5. **Installation** : le serveur du groupe et sa base seront livrés ensemble (par exemple un fichier
   Docker Compose), pour qu'un groupe s'auto-héberge en une commande. L'hébergement en ligne est
   traité dans l'ADR d'hébergement (Q4).

## Conséquences

- Deux moteurs à maintenir : tests automatisés sur les deux, et vigilance sur les différences
  (types, géographie, JSON). La règle « calculs en C# » limite fortement ce risque.
- PostGIS rend possibles côté serveur des fonctions utiles (lieux proches, zones), mais le client
  hors-ligne n'en dispose pas : ces fonctions doivent rester des confort en ligne, pas des
  dépendances du calcul de budget.
- La **synchronisation** entre SQLite et PostgreSQL (qui gagne en cas de modifications concurrentes,
  suppression, historique) fera l'objet d'un ADR dédié : c'est le point le plus délicat de
  l'architecture.
- Le mode **solo** (un membre sans serveur de groupe, uniquement le service local) devient possible
  à peu de frais *(à confirmer comme fonctionnalité)*.
