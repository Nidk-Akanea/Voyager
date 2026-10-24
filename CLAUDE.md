# Voyager — instructions pour Claude

## Le projet

Voyager est une application qui accompagne un voyage du début à la fin :

1. **Préparer** — planifier un voyage : itinéraire, étapes, réservations, budget.
2. **Raconter** — tenir un carnet de voyage : notes, photos, lieux visités sur une carte.

Les deux volets partagent le même modèle (un voyage, ses étapes, ses lieux) : ce qui a été planifié
devient la trame du carnet. Détails et questions ouvertes : [docs/vision.md](docs/vision.md).

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

## Points d'attention métier

Ces règles valent quelle que soit la stack retenue :

- **Données personnelles** : itinéraires, photos géolocalisées et documents de voyage sont sensibles.
  Ne mets jamais de vraies données personnelles dans les exemples, fixtures ou captures.
- **Dates et fuseaux** : un voyage traverse des fuseaux horaires. Stocke les instants en UTC avec le
  fuseau du lieu concerné ; les dates « de calendrier » (jour d'une étape) restent des dates sans heure.
- **Argent** : montants en décimal (jamais en flottant) accompagnés d'un code devise ISO 4217.
- **Lieux** : coordonnées en WGS 84 (latitude, longitude), comme les GPS et les EXIF des photos.
- **Hors-ligne** : on écrit souvent son carnet sans réseau. L'exigence exacte est une question ouverte
  du backlog ; garde-la en tête dans les choix d'architecture.
