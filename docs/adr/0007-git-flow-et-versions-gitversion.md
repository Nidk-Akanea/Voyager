# ADR-0007 — Git Flow et versions calculées par GitVersion

- **Statut** : Acceptée
- **Date** : 2026-10-24
- **Remplace** : —

## Contexte

Jusqu'ici, tout le travail partait de `main` et y revenait par PR. L'utilisateur veut :

- développer en **Git Flow** : une branche d'intégration, des versions préparées puis publiées ;
- **calculer les numéros de version** avec GitVersion plutôt que de les écrire à la main ;
- versionner ainsi **l'application et les add-ins** (plugins).

Voyager a plusieurs composants qui n'évoluent pas au même rythme (ADR-0002, 0003) : l'application,
le SDK des plugins (`Voyager.Sdk`, `@voyager/plugin-sdk`), et chaque plugin livré avec Voyager. Les
contrats `.proto` ont déjà leur propre versionnement par paquet (`v1`, `v2`).

## Options

### Branches

- **GitHub Flow** (une seule branche `main` + branches courtes) : simple, mais pas de branche
  d'intégration ni de préparation de version.
- **Git Flow** : `main` (versions publiées), `develop` (intégration), `feature/*`, `release/*`,
  `hotfix/*`. Plus de cérémonie, mais une séparation nette entre ce qui est en cours et ce qui est
  publié. → **Git Flow**, choix de l'utilisateur.

### Versions des plugins

- **Un seul numéro pour tout** : simple, mais un plugin change de version sans avoir changé, et les
  auteurs de plugins ne savent plus quand le SDK rompt la compatibilité.
- **Un numéro par composant** : chaque composant a ses propres tags et GitVersion ne compte que les
  commits qui le touchent. Plus juste, un peu plus de configuration. → **Un numéro par composant**,
  choix de l'utilisateur.

### Fusions

- **Toujours en squash** : casse Git Flow, car `main` et `develop` ne partagent plus d'historique après
  une release, et GitVersion ne retrouve plus ses repères.
- **Squash pour les features, commit de fusion pour les releases et hotfixes** : historique propre sur
  `develop`, historique fidèle entre `main` et `develop`. → **retenu**, choix de l'utilisateur.

## Décision

### Branches

| Branche           | Part de   | Revient dans          | Fusion           | Rôle                                                                  |
| ----------------- | --------- | --------------------- | ---------------- | --------------------------------------------------------------------- |
| `main`            | —         | —                     | —                | Uniquement des versions publiées, chacune taguée                      |
| `develop`         | `main`    | —                     | —                | Intégration ; **branche par défaut** sur GitHub                       |
| `feature/<sujet>` | `develop` | `develop`             | squash           | Tout travail : fonctionnalité, doc, outillage, correction non urgente |
| `release/<x.y.z>` | `develop` | `main` puis `develop` | commit de fusion | Préparer une version : derniers correctifs, notes de version          |
| `hotfix/<x.y.z>`  | `main`    | `main` puis `develop` | commit de fusion | Corriger en urgence une version publiée                               |

- Le type du changement reste dans le message de commit (Conventional Commits) : `feature/vision-v2`
  peut contenir un commit `docs: …`. Le titre de la PR d'une feature devient le message du commit
  « squash » : il suit donc lui aussi Conventional Commits.
- On ne committe jamais directement sur `main` ni sur `develop`.

### Versions

GitVersion (version 6, mode `GitFlow/v1`) calcule les numéros en **SemVer** :

| Branche                                 | Exemple de version |
| --------------------------------------- | ------------------ |
| `develop`                               | `0.2.0-alpha.7`    |
| `feature/devis`                         | `0.2.0-devis.3`    |
| `release/0.2.0`                         | `0.2.0-beta.2`     |
| `main` (après la release, tag `v0.2.0`) | `0.2.0`            |
| `hotfix/0.2.1`                          | `0.2.1-beta.1`     |

- Point de départ : **`0.1.0`**. On reste en `0.x` tant que le MVP n'est pas atteint ; `1.0.0`
  marquera la première version utilisable par un groupe.
- Les messages de commit pilotent l'incrément : une rupture (`feat!:` ou `BREAKING CHANGE`) augmente
  le majeur, `feat:` le mineur, `fix:` le correctif. `+semver: major|minor|patch|none` reste possible.

### Un numéro par composant

| Composant                                              | Tags                  | Commits comptés                          |
| ------------------------------------------------------ | --------------------- | ---------------------------------------- |
| Application (service local, serveur, interface)        | `v0.2.0`              | tout le dépôt sauf `plugins/` et `docs/` |
| SDK des plugins (`Voyager.Sdk`, `@voyager/plugin-sdk`) | `sdk/v1.0.0`          | le code du SDK                           |
| Chaque plugin                                          | `plugins/<id>/v1.3.0` | `plugins/<id>/`                          |

- La configuration commune est dans `GitVersion.yml`. Pour le SDK et chaque plugin, le build lance
  GitVersion avec un préfixe de tag et un filtre de chemins propres au composant.
- La version d'un plugin est inscrite dans son `plugin.json`, à côté de la **plage de versions du SDK**
  qu'il accepte (ex. `>=1.2.0 <2.0.0`). Le SDK suit strictement SemVer : son majeur ne change que si
  un plugin existant doit être modifié.
- Une release peut publier l'application, le SDK et des plugins ensemble ou séparément : à la fusion
  dans `main`, chaque composant qui a changé reçoit son tag.

## Conséquences

- La branche `develop` est créée et devient la branche par défaut sur GitHub : les PR la visent
  d'office.
- Les skills `/ship` et `/after-merge` travaillent avec `develop` ; deux skills `/release` et
  `/hotfix` mènent les releases et les correctifs urgents de bout en bout.
- La CI calcule et affiche la version à chaque PR ; avec le squelette, cette version sera injectée dans
  les assemblies .NET, les `package.json` et les `plugin.json`.
- Le filtrage des commits par chemin pour le SDK et les plugins sera mis au point avec le squelette,
  quand ces dossiers existeront ; s'il se révèle trop fragile, un nouvel ADR pourra revenir à un
  numéro commun pour les plugins livrés avec l'application.
- Les tags sont la source de vérité des versions publiées : on ne les déplace ni ne les supprime.
