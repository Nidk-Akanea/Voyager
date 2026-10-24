# ADR-0001 — Consigner les décisions

- **Statut** : Acceptée
- **Date** : 2026-10-24
- **Remplace** : —

## Contexte

Voyager démarre sans stack choisie et avec plusieurs questions ouvertes (plateformes, hors-ligne,
partage). Les choix qui en découleront seront structurants et difficiles à défaire ; il faut pouvoir
retrouver pourquoi ils ont été faits, y compris quand le travail est mené avec un assistant IA.

## Options

### Option A — Aucune trace formelle

- Avantages : aucun effort.
- Inconvénients : les raisons se perdent ; on rediscute les mêmes sujets.

### Option B — ADR en Markdown dans le repo

- Avantages : versionnés avec le code, relus en PR, lisibles par Claude comme contexte.
- Inconvénients : demande la discipline d'en écrire un à chaque décision structurante.

## Décision

Option B : un ADR par décision dans `docs/adr/`, numéroté, rédigé en français à partir du modèle
`_template.md`. Seul l'utilisateur fait passer un ADR au statut « Acceptée ».

## Conséquences

- Toute décision structurante (stack, stockage, architecture, hébergement, librairie majeure, format
  de données) passe par un ADR avant implémentation.
- Le skill Claude `/new-adr` automatise la création.
- Un ADR accepté n'est jamais réécrit : on le remplace par un nouveau.
