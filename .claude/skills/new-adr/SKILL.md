---
name: new-adr
description: Rédige un nouvel ADR (Architecture Decision Record) dans docs/adr/ pour une décision structurante de Voyager — choix de stack, stockage, architecture, hébergement, librairie majeure, format de données. À utiliser quand l'utilisateur veut trancher ou documenter une décision technique.
---

# Nouvel ADR

Sujet demandé : $ARGUMENTS

1. Liste `docs/adr/` et prends le numéro suivant le plus grand existant (format `NNNN`, sur 4 chiffres).
2. Vérifie qu'aucun ADR existant ne traite déjà le sujet. S'il y en a un au statut « Acceptée »,
   le nouvel ADR le **remplace** : indique-le dans les deux fichiers (`Remplace ADR-XXXX` /
   `Remplacée par ADR-YYYY`) et ne modifie rien d'autre dans l'ancien.
3. Copie `docs/adr/_template.md` vers `docs/adr/NNNN-<titre-en-kebab-case>.md` (titre en français,
   sans accents dans le nom de fichier).
4. Remplis-le en français :
   - **Contexte** : le besoin, les contraintes (voir `docs/vision.md` et les points d'attention
     métier de `CLAUDE.md`), ce qui force à décider maintenant.
   - **Options** : au moins deux options réelles, chacune avec avantages et inconvénients concrets.
     Pas d'option « homme de paille ».
   - **Décision** : l'option recommandée et pourquoi. Si l'information manque pour recommander,
     dis-le et liste ce qu'il faut savoir.
   - **Conséquences** : ce que ça implique (positif et négatif), ce qu'il faudra faire ensuite.
5. Statut : « Proposée », date du jour. **Ne mets jamais « Acceptée » toi-même** — c'est l'utilisateur
   qui décide.
6. Ajoute la ligne de l'ADR dans le tableau de `docs/adr/README.md`.
7. Lance `npx markdownlint-cli2` et corrige les erreurs.
8. Résume la recommandation à l'utilisateur en quelques lignes et demande-lui s'il l'accepte.
