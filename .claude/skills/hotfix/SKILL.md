---
name: hotfix
description: Mène un correctif urgent Git Flow sur une version publiée de Voyager (ADR-0007) — ouvre hotfix/x.y.z depuis main, applique et teste la correction, puis après accord fusionne dans main en commit de fusion, tague, et reporte dans develop. À utiliser quand une version publiée a un bug qui ne peut pas attendre la prochaine release.
disable-model-invocation: true
---

# Hotfix Git Flow

Le problème à corriger : $ARGUMENTS

Lis d'abord l'[ADR-0007](../../../docs/adr/0007-git-flow-et-versions-gitversion.md). Un hotfix est
réservé à un bug d'une version **publiée** ; sinon, c'est une `feature/…` vers `develop` (`/ship`).

## Ouvrir

1. `git status` doit être propre. `git switch main`, `git pull`.
2. Version : le correctif suivant de la version publiée (`0.2.0` → `0.2.1`) pour le composant
   concerné (application, SDK ou plugin). Confirme-la avec l'utilisateur.
3. Crée `hotfix/<x.y.z>` depuis `main`.
4. Écris d'abord le test qui reproduit le bug (agent `test-writer`), puis la correction, la plus
   petite possible. Commit `fix: …`.
5. Applique la [definition of done](../../rules/definition-of-done.md), dont `code-reviewer`.
6. Ajoute la correction aux notes de version : `docs/releases/<x.y.z>.md`.
7. Pousse la branche et ouvre la PR `hotfix/<x.y.z>` → `main`.

## Terminer (sur accord explicite)

1. CI au vert. Fusionne dans `main` **en commit de fusion** : `gh pr merge <n> --merge`.
2. Sur `main` à jour : tag annoté du composant corrigé (`v<x.y.z>`, `sdk/v…` ou `plugins/<id>/v…`),
   puis `git push origin <tag>`.
3. Reporte dans `develop` en commit de fusion (PR `main` → `develop`, ou `git merge --no-ff main`).
   Si une `release/…` est ouverte, reporte aussi le correctif dans cette branche.
4. Supprime `hotfix/<x.y.z>` (locale et distante).
5. Rends compte : version corrigée, tag, état de `main` et `develop`.
