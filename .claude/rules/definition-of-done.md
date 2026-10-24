# Definition of done

Avant de dire qu'une tâche est terminée, vérifie chaque point qui s'applique. Si un point n'a pas pu
être vérifié, dis-le explicitement à l'utilisateur au lieu de l'omettre.

## Toujours

- Le travail est sur une branche Git Flow ([ADR-0007](../../docs/adr/0007-git-flow-et-versions-gitversion.md)) :
  `feature/<sujet>` partie de `develop`, ou `release/…` / `hotfix/…` via `/release` et `/hotfix`.
  Jamais de commit direct sur `main` ni sur `develop`.
- `npx markdownlint-cli2` passe si de la documentation a changé.
- Le [glossaire](../../docs/glossaire.md) contient tout nouveau terme métier, avec son nom de code.
- Les ADR et specs touchés gardent le statut que l'utilisateur leur a donné : tu ne passes jamais
  toi-même un document à « Acceptée » ou « Validée ».
- Les questions sans réponse évidente sont dans les questions ouvertes du
  [backlog](../../docs/backlog.md), pas tranchées en silence.

## Quand du code a changé

- Le build passe **sans aucun avertissement**.
- Tous les tests passent ; tout nouveau calcul de `Voyager.Core` a ses tests ; un bug corrigé a le test
  qui l'aurait détecté.
- Le formatage est appliqué (`dotnet format`, Prettier) et le lint passe (ESLint `--max-warnings 0`).
- `buf lint` et `buf breaking` passent si un `.proto` a changé.
- Les agents de relecture ont été lancés et leurs remarques traitées ou discutées avec l'utilisateur :
  `architecture-reviewer`, `code-reviewer`, et `privacy-reviewer` si des journaux, des données de
  test, des exemples ou la configuration Electron ont changé.
- `code-polish` est passé sur le code nouveau ou modifié : journaux, messages d'erreur, libellés et
  documentation du code sont à niveau.
- La spec concernée, les schémas de `docs/schemas/` et `CLAUDE.md` (section Commandes) reflètent ce
  qui a été fait.

## Pour livrer

- Commit en anglais au format Conventional Commits, puis PR vers `develop` avec une description en
  français : utilise `/ship`. Les releases et les hotfixes passent par `/release` et `/hotfix`.
- Tu ne fusionnes une PR que si l'utilisateur te le demande explicitement.
