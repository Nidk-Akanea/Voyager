---
paths:
  - "docs/**"
  - "*.md"
---

# Documentation

- Rédigée en **français**, claire pour un lecteur qui découvre le projet ; lignes d'environ 100 colonnes.
- **ADR** (`docs/adr/`) : nouveaux ADR via `/new-adr`, au statut « Proposée ». Seul l'utilisateur les
  passe à « Acceptée ». Un ADR accepté ne se réécrit plus : un nouvel ADR le remplace. L'index
  `docs/adr/README.md` suit chaque changement de statut.
- **Specs** (`docs/specs/`) : via `/new-spec`, au statut « Brouillon ». Seul l'utilisateur les passe
  à « Validée ».
- **Glossaire** : tout nouveau terme métier y entre, avec son nom de code anglais ; on emploie les
  termes tels qu'ils y sont écrits.
- **Backlog** : idées, épics et questions ouvertes via `/backlog` ; une question tranchée garde sa
  ligne avec la réponse.
- **Schémas** (`docs/schemas/`) en Mermaid : à mettre à jour quand l'architecture change.
- Aucune donnée personnelle réelle dans les exemples : le repo est public.
- Les tableaux restent alignés (le lint l'exige) ; `npx markdownlint-cli2` doit passer.
