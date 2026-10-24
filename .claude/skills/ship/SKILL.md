---
name: ship
description: Livre le travail en cours de Voyager — vérifications (lint, build, tests), commit Conventional Commits en anglais, push de la branche, ouverture de la PR vers develop (Git Flow) avec une description en français, lecture de la CI. À utiliser quand l'utilisateur demande de committer, pousser, ouvrir une PR ou « livrer ».
---

# Livrer : commit, push et PR

Contexte éventuel donné par l'utilisateur : $ARGUMENTS

## 1. Vérifier la branche

- `git status` et `git branch --show-current`.
- Si tu es sur `main` ou `develop`, crée d'abord une branche `feature/<sujet>` (en anglais,
  kebab-case) **à partir de `develop` à jour**. Si tu es sur une `release/…` ou un `hotfix/…`,
  utilise plutôt `/release` ou `/hotfix`.
- Regarde ce qui va partir (`git status --short`, `git diff --stat`). Si un fichier inattendu apparaît
  (contenu de `private/`, tableur, base SQLite, `.env`, fichier généré), arrête-toi et demande.

## 2. Vérifier le travail

Applique la [definition of done](../../rules/definition-of-done.md) :

- `npx markdownlint-cli2` si de la documentation a changé.
- Build, tests, formatage et lint quand le code existe (voir la section Commandes de `CLAUDE.md`).
- Si du code ou des `.proto` ont changé et que les relecteurs n'ont pas encore été lancés sur cette
  version, lance `architecture-reviewer` et `code-reviewer` (et `privacy-reviewer` si des journaux,
  des données de test, des exemples ou la configuration Electron ont changé). Corrige ou présente
  leurs remarques à l'utilisateur avant de continuer.

Si une vérification échoue, n'ouvre pas la PR : montre l'erreur et propose la correction.

## 3. Committer

- `git add` des fichiers concernés (jamais `private/`).
- Message en anglais au format Conventional Commits : un titre court (`docs: …`, `feat: …`), une
  ligne vide, un corps qui explique **ce qui** change et **pourquoi**.
- Termine le message par la ligne d'attribution donnée par les instructions de la session.

## 4. Pousser et ouvrir la PR

- `git push -u origin <branche>`.
- `gh pr create --repo Nidk-Akanea/Voyager --base develop --head <branche>` avec le même titre que le
  commit (il deviendra le message du commit « squash » : il suit Conventional Commits) et une description **en français** :

  ```markdown
  ## Résumé

  Ce que la PR apporte, en quelques puces, avec des liens vers les fichiers importants.

  ## Vérification

  Ce qui a été vérifié et comment (lint, build, tests, relecteurs), et ce qui ne l'a pas été.

  ## Prochaines étapes

  Ce qui reste à faire ensuite, s'il y a lieu.
  ```

  suivie de la ligne d'attribution des PR donnée par les instructions de la session.
- Si `gh` répond `401 Bad credentials` alors que `gh auth status` indique une connexion valide, une
  variable `GITHUB_TOKEN` périmée traîne dans l'environnement : relance la commande avec
  `env -u GITHUB_TOKEN gh …`.

## 5. Lire la CI une fois

`gh pr checks <numéro> --repo Nidk-Akanea/Voyager`, une seule fois, sans boucle d'attente.

## 6. Rendre compte

Donne à l'utilisateur le lien de la PR, ce qu'elle contient, l'état de la CI, et ce qu'il doit faire :
relire puis fusionner avec « Squash and merge » (une feature va toujours dans `develop` en squash). **Ne fusionne pas toi-même** sauf demande explicite.
