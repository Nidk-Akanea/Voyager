---
name: new-spec
description: Rédige la spécification fonctionnelle d'une fonctionnalité de Voyager dans docs/specs/ (planification de voyage ou carnet de voyage). À utiliser avant d'implémenter une fonctionnalité, ou quand l'utilisateur veut décrire, cadrer ou détailler un besoin.
---

# Nouvelle spec

Fonctionnalité demandée : $ARGUMENTS

1. Lis `docs/vision.md`, `docs/glossaire.md` et `docs/backlog.md` pour situer la fonctionnalité
   (épic concerné, termes existants).
2. Vérifie dans `docs/specs/` qu'aucune spec ne couvre déjà le sujet ; si oui, propose de la compléter
   plutôt que d'en créer une nouvelle.
3. Copie `docs/specs/_template.md` vers `docs/specs/<epic>-<sujet-en-kebab-case>.md`
   (ex. `planification-ajout-etape.md`, `carnet-import-photos.md`).
4. Remplis-la en français, du point de vue de l'utilisateur du produit, sans détail d'implémentation :
   - user stories courtes (« En tant que…, je veux…, afin de… ») ;
   - règles métier numérotées ;
   - critères d'acceptation vérifiables (Étant donné / Quand / Alors) ;
   - cas limites : fuseaux horaires, devises, hors-ligne, données personnelles, quand ils s'appliquent ;
   - hors périmètre explicite.
5. Ce que tu ne sais pas, ne l'invente pas : mets-le dans la section « Questions ouvertes » de la spec.
6. Ajoute au glossaire tout nouveau terme métier, avec son nom de code anglais.
7. Rattache la spec à son épic dans `docs/backlog.md` (lien vers le fichier).
8. Statut « Brouillon ». **Seul l'utilisateur passe une spec à « Validée ».**
9. Lance `npx markdownlint-cli2` et corrige les erreurs, puis liste à l'utilisateur les questions
   ouvertes qui bloquent la validation.
