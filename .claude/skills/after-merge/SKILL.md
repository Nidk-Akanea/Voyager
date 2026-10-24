---
name: after-merge
description: Après la fusion d'une PR de Voyager sur GitHub, remet le poste à jour — retour sur develop, récupération de la fusion, vérification que la branche est bien intégrée, suppression de la branche locale. À utiliser quand l'utilisateur dit qu'il a fusionné une PR ou demande de remettre son PC à jour.
---

# Après une fusion : remettre le poste à jour

PR ou branche concernée (si l'utilisateur l'a précisée) : $ARGUMENTS

1. **Repérer la branche** : la branche courante (`git branch --show-current`), sauf si l'utilisateur
   en désigne une autre. Si tu es déjà sur `develop` ou `main`, retrouve la dernière branche locale fusionnée.
2. **Vérifier qu'il n'y a rien en cours** : `git status --short`. S'il reste des modifications non
   committées, arrête-toi et demande quoi en faire.
3. **Vérifier la fusion** : `gh pr view <branche> --repo Nidk-Akanea/Voyager --json state,mergeCommit,baseRefName`.
   Si la PR n'est pas `MERGED`, dis-le et arrête-toi.
4. **Revenir sur la branche cible et récupérer** : la PR indique sa cible (`baseRefName`, en
   général `develop`). `git switch <cible>`, puis `git pull --prune`.
5. **Vérifier que tout est intégré** : une fusion « squash » change l'historique, c'est donc le
   contenu qu'on compare, pas les commits, et seulement sur les fichiers de la branche (la cible a pu
   recevoir d'autres features entre-temps) :
   `git diff --stat <branche> <cible> -- $(git diff --name-only $(git merge-base <branche> <cible>) <branche>)`
   doit être vide.
   - Vide : supprime la branche locale avec `git branch -D <branche>` (`-D` est nécessaire après un
     squash, et sans risque puisque le contenu est dans la cible).
   - Pas vide : ne supprime rien, montre la différence à l'utilisateur.
6. **Rendre compte** en français : le commit arrivé sur la cible (`git log --oneline -1`), la branche
   supprimée ou conservée et pourquoi, et l'état final (`git status -sb`).

Si `gh` répond `401 Bad credentials` alors que `gh auth status` indique une connexion valide, relance
la commande avec `env -u GITHUB_TOKEN gh …`.
