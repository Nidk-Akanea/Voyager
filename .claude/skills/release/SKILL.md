---
name: release
description: Mène une release Git Flow de Voyager (ADR-0007) — ouvre release/x.y.z depuis develop avec la version calculée par GitVersion, prépare les notes de version, puis après accord fusionne dans main en commit de fusion, tague l'application, le SDK et les plugins qui ont changé, et reporte dans develop. À utiliser quand l'utilisateur veut préparer, publier ou terminer une version.
disable-model-invocation: true
---

# Release Git Flow

Demande de l'utilisateur (version, composants, étape) : $ARGUMENTS

Lis d'abord l'[ADR-0007](../../../docs/adr/0007-git-flow-et-versions-gitversion.md). Une release se fait
en **deux temps** : l'ouvrir, puis la terminer quand l'utilisateur la valide. Demande à l'utilisateur
où il en est si ce n'est pas clair.

## Temps 1 — Ouvrir la release

1. `git status` doit être propre. Mets `develop` à jour : `git switch develop`, `git pull`.
2. Calcule la version : `dnx GitVersion.Tool --yes -- /showvariable MajorMinorPatch` (sous
   PowerShell). Propose ce numéro à l'utilisateur ; il peut en choisir un autre.
3. Repère les composants qui ont changé depuis leur dernier tag (`git describe --tags --match …`,
   `git log <dernier-tag>..develop -- <chemin>`) : application (`v*`), SDK (`sdk/v*`), chaque plugin
   (`plugins/<id>/v*`). Pour chacun, calcule sa version (voir l'ADR-0007).
4. Crée `release/<x.y.z>` depuis `develop` et pousse-la.
5. Rédige les **notes de version** en français dans `docs/releases/<x.y.z>.md` à partir des commits
   (`git log <dernier-tag>..HEAD --oneline`) : nouveautés (`feat`), corrections (`fix`), ruptures,
   versions de chaque composant publié. Committe-les sur la branche de release.
6. Seuls des correctifs, la documentation et les notes vont sur la branche de release : pas de
   nouvelle fonctionnalité.
7. Dis à l'utilisateur ce qui part dans la release et ce qu'il doit vérifier avant de la terminer.

## Temps 2 — Terminer la release (sur accord explicite)

1. Vérifie : build, tests, lint et CI au vert sur `release/<x.y.z>` (`gh pr checks` si une PR existe).
2. Ouvre la PR `release/<x.y.z>` → `main` (description : les notes de version). Après accord de
   l'utilisateur, fusionne-la **en commit de fusion**, jamais en squash :
   `gh pr merge <n> --merge --repo Nidk-Akanea/Voyager`.
3. Sur `main` à jour, crée les tags **annotés** des composants publiés, puis pousse-les :
   `git tag -a v<x.y.z> -m "Voyager <x.y.z>"`, `git tag -a sdk/v<…>`, `git tag -a plugins/<id>/v<…>`,
   puis `git push origin <tags>`.
4. Reporte dans `develop` : PR `main` → `develop` fusionnée **en commit de fusion** (ou
   `git merge --no-ff main` sur `develop` puis push, si l'utilisateur préfère sans PR).
5. Supprime la branche `release/<x.y.z>` (locale et distante) une fois les deux fusions faites.
6. Vérifie avec GitVersion que `main` donne exactement `<x.y.z>` et que `develop` est passé à la
   version mineure suivante en `alpha`.
7. Rends compte : version publiée, tags créés, état de `main` et `develop`.

Les tags ne se déplacent ni ne se suppriment : en cas d'erreur après publication, on fait un hotfix.
