# Backlog

Liste vivante des épics, idées et questions. Une fonctionnalité prête à être construite obtient une
spec dans [specs/](specs/) ; une décision technique obtient un ADR dans [adr/](adr/).

## Épics

### E0 — Cadrage

- [ ] Valider la [vision produit](vision.md)
- [ ] Trancher les questions ouvertes bloquantes (Q1 à Q3)
- [ ] Définir le périmètre du MVP
- [ ] ADR : choix de la stack (dépend de Q1 et Q2)
- [ ] ADR : stockage des données et des photos
- [ ] Une fois la stack choisie : squelette de solution, build, tests, CI

### E1 — Planification

- [ ] Créer / modifier / supprimer un voyage
- [ ] Gérer les étapes d'un itinéraire (ajout, ordre, dates)
- [ ] Rattacher des lieux aux étapes (recherche de lieu, carte)
- [ ] Enregistrer des réservations et leurs documents
- [ ] Suivre un budget multi-devises

### E2 — Carnet de voyage

- [ ] Écrire une entrée de carnet (jour / étape / lieu)
- [ ] Ajouter des photos et lire leur géolocalisation
- [ ] Carte du voyage : prévu vs parcouru
- [ ] Relire le récit d'un voyage terminé

### E3 — Transverse

- [ ] Comptes utilisateurs et authentification (si Q3 l'exige)
- [ ] Fonctionnement hors-ligne et synchronisation (selon Q2)
- [ ] Export des données
- [ ] Partage d'un voyage ou d'un carnet

## Questions ouvertes

| #   | Question                                                              | Impact                    | Statut  |
| --- | --------------------------------------------------------------------- | ------------------------- | ------- |
| Q1  | Quelles plateformes : web, mobile natif, les deux, PWA ?              | Choix de stack            | Ouverte |
| Q2  | Quel niveau de hors-ligne : lecture seule, saisie, tout le voyage ?   | Stack, stockage, synchro  | Ouverte |
| Q3  | Mono-utilisateur, ou voyages partagés et édités à plusieurs ?         | Comptes, droits, synchro  | Ouverte |
| Q4  | Où sont hébergées les données (local seul, cloud, auto-hébergé) ?     | Stockage, confidentialité | Ouverte |
| Q5  | Quel fournisseur de cartes et de recherche de lieux (coût, licence) ? | Dépendances, coûts        | Ouverte |
| Q6  | Partage du carnet : lien privé, export PDF, page publique ?           | Périmètre E2 / E3         | Ouverte |

## Idées en vrac

- Import automatique des réservations depuis les mails de confirmation.
- Météo prévue sur les étapes.
- Statistiques de fin de voyage (distance, pays, dépenses).
