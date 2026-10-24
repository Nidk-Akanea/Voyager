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

## État actuel : cadrage

- **Aucun code applicatif pour l'instant.** On construit d'abord la vision, le backlog et les décisions.
- **La stack n'est pas choisie.** Le `.gitignore` Visual Studio vient de la création du repo, ce n'est
  pas une décision. Ne crée pas de solution, de projet ni de `package.json` applicatif, et n'installe
  aucune dépendance, tant que l'ADR de choix de stack n'est pas au statut « Acceptée ».
- Quand la stack sera choisie, mets à jour : la section [Commandes](#commandes) ci-dessous,
  `.editorconfig`, `.gitignore` et la CI (`.github/workflows/`).

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
  vision.md               le produit, ses utilisateurs, ses principes, les questions ouvertes
  backlog.md              épics, idées, questions à trancher
  glossaire.md            termes métier FR ↔ noms de code EN
  adr/                    décisions d'architecture (une par fichier, numérotées)
  specs/                  spécifications fonctionnelles (une par fonctionnalité)
.claude/
  settings.json           permissions partagées de Claude Code
  skills/                 skills du projet (/new-adr, /new-spec)
.github/workflows/        CI
```

## Façon de travailler

1. **Une idée, un besoin** → une ligne dans [docs/backlog.md](docs/backlog.md).
2. **Une fonctionnalité à construire** → une spec dans `docs/specs/` (skill `/new-spec`).
   On n'implémente qu'une spec au statut « Validée ».
3. **Une décision structurante** (stack, stockage, architecture, hébergement, librairie majeure,
   format de données) → un ADR dans `docs/adr/` (skill `/new-adr`).
   - Tu rédiges l'ADR au statut « Proposée », avec les options comparées honnêtement.
   - **Seul l'utilisateur fait passer un ADR ou une spec à « Acceptée » / « Validée ».**
     Ne change jamais ce statut toi-même.
   - Un ADR accepté ne se modifie plus : on en crée un nouveau qui le remplace.
4. Quand un comportement change, mets à jour la spec et le glossaire dans le même commit.
5. Si une question n'a pas de réponse évidente, ajoute-la aux questions ouvertes du backlog plutôt
   que de trancher seul.

## Git

- Messages de commit en anglais, format [Conventional Commits](https://www.conventionalcommits.org/) :
  `feat:`, `fix:`, `docs:`, `chore:`, `ci:`, `refactor:`, `test:`.
- Branches : `feat/<sujet>`, `fix/<sujet>`, `docs/<sujet>`, `chore/<sujet>`.
- Ne committe et ne pousse que sur demande. Le travail se fait sur une branche puis une PR vers `main`.
- Aucun secret dans le repo (`.env` est ignoré).

## Commandes

| Action                  | Commande                  |
| ----------------------- | ------------------------- |
| Vérifier le Markdown    | `npx markdownlint-cli2`   |
| Build / tests / lancer  | à définir avec la stack   |

Lance le lint Markdown après avoir modifié de la documentation.

## Principes d'architecture

- **Tout est modulaire** ([ADR-0002](docs/adr/0002-architecture-modulaire-par-contrats-et-plugins.md),
  acceptée) : un cœur minimal, des contrats versionnés, et des plugins qui les implémentent — y compris
  pour les fonctionnalités de base. Ne crée un point d'extension que si au moins deux implémentations
  sont connues ou prévues.
- **Multi-membres dès le départ** : même si la collaboration arrive après le MVP, le modèle de données
  doit la prévoir.
- **Du générique au précis** : une donnée peut être une estimation puis s'affiner ; aucun calcul ne doit
  bloquer sur une information manquante.
- **Traçabilité** : chaque donnée externe garde sa source et sa date.

## Points d'attention métier

Ces règles valent quelle que soit la stack retenue :

- **Données personnelles** : itinéraires, photos géolocalisées et documents de voyage sont sensibles.
  Ne mets jamais de vraies données personnelles dans les exemples, fixtures ou captures.
  **Le repo GitHub est public.** Les voyages réels de l'utilisateur (tableurs, réservations) vont dans
  `private/`, ignoré par git ; seule une version anonymisée (voyageurs A, B, C ; sans noms, références
  de réservation ni coordonnées personnelles) peut être committée.
- **Dates et fuseaux** : un voyage traverse des fuseaux horaires. Stocke les instants en UTC avec le
  fuseau du lieu concerné ; les dates « de calendrier » (jour d'une étape) restent des dates sans heure.
- **Argent** : montants en décimal (jamais en flottant) accompagnés d'un code devise ISO 4217.
- **Lieux** : coordonnées en WGS 84 (latitude, longitude), comme les GPS et les EXIF des photos.
- **Hors-ligne** : on écrit souvent son carnet sans réseau. L'exigence exacte est une question ouverte
  du backlog ; garde-la en tête dans les choix d'architecture.
