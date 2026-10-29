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
2. Calcule la version : `pwsh tools/get-version.ps1 -Variable MajorMinorPatch` (application).
   Propose ce numéro à l'utilisateur ; il peut en choisir un autre.
3. Repère les composants qui ont changé depuis leur dernier tag et calcule leur version avec
   `pwsh tools/get-version.ps1 -All` : une ligne par composant (application `v*`, SDK `sdk/v*`, chaque
   plugin `plugins/<id>/v*`), avec sa version, son `x.y.z` (`MajorMinorPatch`) et `Changed` (modifié depuis
   son dernier tag ; un composant sans tag compte comme modifié). Seuls ceux qui ont changé sont publiés.
   Un seul : `-Component <id>`. Le SDK et les plugins prennent leur numéro de leurs seuls tags et commits,
   jamais du nom de la branche : un composant inchangé garde sa version.
   Si le SDK change de version mineure (`0.x`) ou majeure, vérifie le champ `sdk` du manifeste de chaque
   plugin livré : un plugin écrit pour une autre version est refusé au démarrage.
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
3. Sur `main` à jour, relance `pwsh tools/get-version.ps1 -All`. Avant les tags, la colonne `Version` d'un
   composant modifié est de la forme `0.2.0-2` : **le numéro à taguer est son `MajorMinorPatch`** (`0.2.0`),
   celui qui figure déjà dans les notes de version. Crée les tags **annotés** des seuls composants dont
   `Changed` est vrai, puis pousse-les : `git tag -a v<x.y.z> -m "Voyager <x.y.z>"`, `git tag -a sdk/v<…>`,
   `git tag -a plugins/<id>/v<…>`, puis `git push origin <tags>`. Vérifie avant de pousser (`git tag --list`)
   qu'aucun tag n'est « nu » (sans `v`, ni préfixe) : il compterait pour tous les composants.
4. Reporte dans `develop` : PR `main` → `develop` fusionnée **en commit de fusion** (ou
   `git merge --no-ff main` sur `develop` puis push, si l'utilisateur préfère sans PR).
5. Supprime la branche `release/<x.y.z>` (locale et distante) une fois les deux fusions faites.
6. Vérifie avec GitVersion que `main` donne exactement `<x.y.z>` (et chaque composant publié sa version
   taguée : `pwsh tools/get-version.ps1 -All`) et que `develop` est passé à la version mineure suivante
   en `alpha`.
7. Rends compte : version publiée, tags créés, état de `main` et `develop`.

Les tags ne se déplacent ni ne se suppriment : en cas d'erreur après publication, on fait un hotfix.
