# Voyager — instructions pour Claude

## Le projet

Voyager sert à **préparer un voyage de groupe avec un budget juste**, en remplaçant le tableur
qu'on bricole d'habitude.

- **Un voyage est un projet** : des envies (thèmes, destinations, activités), puis des **devis**
  (simulations jour par jour, que chaque membre peut créer), puis une **facture** (le devis retenu,
  avec ce qui a réellement été payé).
- **Le budget est le cœur** : coûts directs, induits et indirects ; tarifs avec une base de calcul
  (par personne, par unité, forfait…) ; prévu vs payé ; part de chaque voyageur ; qui a payé quoi et
  remboursements.
- **Plusieurs vues** sur les mêmes données : timeline, planning, carte, programme, graphiques.
- **Plus tard** : le carnet de voyage (photos, kilomètres, lieux visités).

Détails : [docs/vision.md](docs/vision.md). Vocabulaire : [docs/glossaire.md](docs/glossaire.md).

## État actuel

- **Squelette — étapes 1 et 2 en place** : fondations (solution .NET, tests d'architecture, espace
  pnpm, ESLint, Prettier, Buf, CI, Aspire) et une tranche technique « À propos » de bout en bout :
  Electron → interface React (Fluent UI, français et anglais) → gRPC-Web → service local → SQLite.
  **Aucun code métier encore.** Le paquet installable est en place, sans publication automatique
  (`tools/release.ps1`, Velopack, journaux en fichiers, verrous Electron), avec un écran de démarrage et
  une mise à jour automatique au démarrage (ADR-0014, proposée) ; le service local est surveillé. Reste à
  faire : l'essai d'installation et de mise à jour, la première release publiée, puis l'étape 3, la
  preuve des plugins, après leur ADR.
- **Les décisions techniques sont prises** (ADR 0002 à 0013) : Electron + React/TypeScript pour
  l'interface, un service local .NET 10 pour la logique et le hors-ligne, un serveur hébergé pour
  plusieurs groupes en ASP.NET Core, gRPC (pas de REST), PostgreSQL + PostGIS (serveur) et SQLite
  (local), architecture hexagonale rangée par domaine, Git Flow avec versions calculées par
  GitVersion, français et anglais avec Fluent (Linguini côté .NET), installation et mises à jour avec
  Velopack, développement orchestré par Aspire (`Voyager.AppHost`, `Voyager.ServiceDefaults`),
  journaux de développement écrits dans Seq par Serilog, paquet assemblé avec `@electron/packager`
  (fuses verrouillées, identifiant de paquet `VoyagerApp`, journaux en fichiers sur le poste).
- Les étapes suivantes du squelette se font **sur demande** de l'utilisateur.

## Langues

- **Français** : documentation, specs, ADR, backlog, échanges avec l'utilisateur.
- **Anglais** : code, identifiants, commentaires dans le code, messages de commit, noms de branches.
- Le [glossaire](docs/glossaire.md) fait la correspondance entre les termes métier (FR) et les noms
  dans le code (EN). Utilise-le et complète-le quand un nouveau concept apparaît.

## Organisation du repo

```text
CLAUDE.md                 ce fichier
README.md                 présentation courte du projet
docs/
  vision.md               le produit, ses utilisateurs, ses principes
  backlog.md              épics, idées, questions à trancher
  glossaire.md            termes métier FR ↔ noms de code EN
  schemas/                schémas fonctionnel et technique (Mermaid)
  conventions/            règles de code : organisation, C#, TypeScript, gRPC, tests
  adr/                    décisions d'architecture (une par fichier, numérotées)
  specs/                  spécifications fonctionnelles (une par fonctionnalité)
.claude/
  settings.json           permissions et hooks partagés
  rules/                  règles chargées selon les fichiers touchés
  agents/                 agents de relecture et d'écriture de tests
  skills/                 procédures du projet (/new-adr, /new-spec, /backlog, /feature,
                          /new-use-case, /ship, /after-merge, /release, /hotfix,
                          /update-instructions)
  hooks/                  scripts des hooks (lint Markdown, formatage du code)
.github/workflows/        CI (build et tests, lint de la doc, calcul de version)
proto/                    contrats gRPC
src/dotnet/               projets .NET
src/ts/                   paquets TypeScript
tests/                    tests .NET
tools/                    scripts de développement et de livraison (`release.ps1`)
deploy/                   image Docker du serveur du groupe (ce qu'on livre à l'hébergeur)
Voyager.slnx              solution .NET ; réglages : global.json, Directory.*.props, nuget.config
package.json              espace de travail pnpm ; réglages : pnpm-workspace.yaml, eslint, prettier
GitVersion.yml            calcul des numéros de version (ADR-0007)
private/                  données réelles de l'utilisateur, ignorées par git
```

Détail et règles de rangement : [organisation du code](docs/conventions/organisation-du-code.md).

## Façon de travailler

1. **Une idée, un besoin** → `/backlog`.
2. **Une fonctionnalité à construire** → une spec dans `docs/specs/` (`/new-spec`).
   On n'implémente qu'une spec au statut « Validée ».
3. **Une décision structurante** (stack, stockage, architecture, hébergement, librairie majeure,
   format de données) → un ADR (`/new-adr`).
   - Tu rédiges l'ADR au statut « Proposée », avec les options comparées honnêtement.
   - **Seul l'utilisateur fait passer un ADR ou une spec à « Acceptée » / « Validée ».**
   - Un ADR accepté ne se modifie plus : on en crée un nouveau qui le remplace.
4. Quand un comportement change, mets à jour la spec et le glossaire dans le même commit.
5. Si une question n'a pas de réponse évidente, ajoute-la aux questions ouvertes du backlog plutôt
   que de trancher seul.
6. Avant de dire « fini », applique la [definition of done](.claude/rules/definition-of-done.md).
7. L'utilisateur découvre GitHub et les PR : explique ce que tu fais en termes simples.

## Outils de Claude

| Outil                         | Quand l'utiliser                                                                                                                                             |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Agent `architecture-reviewer` | Après une modification de code ou de `.proto` : conformité aux ADR et conventions                                                                            |
| Agent `code-reviewer`         | Après une modification de code : bugs, cas limites, fuites mémoire, performance                                                                              |
| Agent `privacy-reviewer`      | Quand journaux, données de test, exemples ou configuration Electron changent                                                                                 |
| Agent `code-polish`           | Sur du code qui fonctionne : refactorings locaux (et propositions plus larges), journaux, messages d'erreur, libellés, commentaires et documentation du code |
| Agent `test-writer`           | Pour écrire les tests d'un cas d'usage ou d'un calcul du cœur                                                                                                |
| `/ship`                       | Livrer une feature : vérifications, commit, push, PR vers `develop`, lecture de la CI                                                                        |
| `/after-merge`                | Après la fusion d'une PR par l'utilisateur : remettre la branche cible à jour, nettoyer                                                                      |
| `/backlog`                    | Ajouter une idée, une tâche d'épic ou une question ouverte                                                                                                   |
| `/release`, `/hotfix`         | À la demande de l'utilisateur : préparer et publier une version, corriger en urgence une version publiée                                                     |
| `/feature`                    | Implémenter une spec validée de bout en bout, jusqu'à `/ship`                                                                                                |
| `/new-use-case`               | Ajouter un cas d'usage dans un domaine du cœur, avec ses tests                                                                                               |
| `/new-adr`, `/new-spec`       | Rédiger une décision ou une spec                                                                                                                             |
| `/update-instructions`        | À la demande de l'utilisateur, après une étape importante : relire et mettre à jour ces instructions, créer des `CLAUDE.md` locaux si utile                  |

Les relecteurs travaillent sur le code, pas sur ton raisonnement : donne-leur la liste des fichiers
ou la branche, pas tes conclusions.

## Git

- Messages de commit en anglais, format [Conventional Commits](https://www.conventionalcommits.org/) :
  `feat:`, `fix:`, `docs:`, `chore:`, `ci:`, `refactor:`, `test:`.
- **Git Flow** ([ADR-0007](docs/adr/0007-git-flow-et-versions-gitversion.md)) : `main` = versions
  publiées et taguées ; `develop` = intégration, branche par défaut ; tout travail sur
  `feature/<sujet>` depuis `develop` (`/ship`) ; versions via `release/<x.y.z>` (`/release`) ;
  correctifs urgents via `hotfix/<x.y.z>` (`/hotfix`). Jamais de commit direct sur `main` ni `develop`.
- Fusions : feature → `develop` en **squash** ; release et hotfix → `main` puis `develop` en
  **commit de fusion**.
- **Versions calculées par GitVersion** (`GitVersion.yml`), pilotées par Conventional Commits ; un
  numéro pour l'application (`v*`), le SDK (`sdk/v*`) et chaque plugin (`plugins/<id>/v*`). Ne
  jamais écrire un numéro de version à la main, ni déplacer ou supprimer un tag.
- Ne committe et ne pousse que sur demande ; ne fusionne une PR que si l'utilisateur le demande.
- Aucun secret dans le repo (`.env` est ignoré).

## Commandes

| Action                                                                                                                                            | Commande                                                                                                                                                    |
| ------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Compiler                                                                                                                                          | `dotnet build Voyager.slnx`                                                                                                                                 |
| Tester (.NET)                                                                                                                                     | `dotnet test --solution Voyager.slnx`                                                                                                                       |
| Formater (.NET)                                                                                                                                   | `dotnet format Voyager.slnx` (vérifier : `--verify-no-changes`)                                                                                             |
| Installer les dépendances TypeScript (génère aussi le client gRPC)                                                                                | `pnpm install`                                                                                                                                              |
| Lint, types, tests, formatage (TypeScript)                                                                                                        | `pnpm lint`, `pnpm typecheck`, `pnpm test`, `pnpm format:check`                                                                                             |
| Construire l'interface et l'application de bureau                                                                                                 | `pnpm build`                                                                                                                                                |
| Contrats `.proto` (et régénérer le client TypeScript)                                                                                             | `pnpm proto:lint`, `pnpm proto:breaking`, `pnpm proto:generate`                                                                                             |
| Lancer tout en développement (Aspire : PostgreSQL + PostGIS, serveur du groupe, service local, interface Vite, fenêtre Electron, tableau de bord) | `pnpm install` une fois, puis F5 sur `Voyager.AppHost` ou `dotnet run --project src/dotnet/Voyager.AppHost` ; tableau de bord sur `https://localhost:17180` |
| Construire l'image du serveur du groupe                                                                                                           | `docker build -f deploy/GroupServer.Dockerfile .`                                                                                                           |
| Construire le paquet installable (Windows : service publié, application, Velopack ; résultat dans `artifacts/`)                                   | `pwsh tools/release.ps1` (`-Docker` ajoute l'image du serveur du groupe)                                                                                    |
| Vérifier le Markdown                                                                                                                              | `npx markdownlint-cli2`                                                                                                                                     |
| Calculer la version (PowerShell)                                                                                                                  | `dnx GitVersion.Tool --yes -- /showvariable FullSemVer`                                                                                                     |

## Principes d'architecture

- **Tout est modulaire** ([ADR-0002](docs/adr/0002-architecture-modulaire-par-contrats-et-plugins.md)) :
  un cœur minimal, des contrats versionnés, des plugins — y compris pour les fonctionnalités de base.
  Pas de point d'extension sans au moins deux implémentations connues ou prévues.
- **Hexagonale, rangée par domaine**
  ([ADR-0005](docs/adr/0005-architecture-du-code-hexagonale-par-domaine.md)) : `Voyager.Core` ne
  dépend d'aucune technique ; EF Core, gRPC et plugins sont des adaptateurs autour.
- **Aucun calcul métier hors de `Voyager.Core`** : l'interface formate et saisit.
- **Multi-membres dès le départ** : le modèle prévoit la collaboration, même si elle vient après le MVP.
- **Du générique au précis** : aucun calcul ne bloque sur une information manquante.
- **Traçabilité** : chaque donnée externe garde sa source et sa date.

Les règles détaillées de chaque langage sont dans `.claude/rules/` (chargées automatiquement) et
[docs/conventions/](docs/conventions/README.md).

## Points d'attention métier

- **Données personnelles** : itinéraires, photos géolocalisées et documents de voyage sont sensibles.
  Ne mets jamais de vraies données personnelles dans les exemples, fixtures ou captures.
  **Le repo GitHub est public.** Les données réelles de l'utilisateur vont dans `private/`, ignoré par
  git. L'utilisateur confronte lui-même le modèle à ses voyages passés : ne lui demande pas ses
  tableurs.
- **Dates et fuseaux** : un voyage traverse des fuseaux horaires. Instants en UTC avec le fuseau du
  lieu ; dates « de calendrier » (jour d'une étape) sans heure.
- **Argent** : montants en décimal (jamais en flottant) accompagnés d'un code devise ISO 4217.
- **Lieux** : coordonnées en WGS 84 (latitude, longitude), comme les GPS et les EXIF des photos.
- **Hors-ligne** : consulter le programme et saisir les dépenses sans réseau, puis synchroniser.
