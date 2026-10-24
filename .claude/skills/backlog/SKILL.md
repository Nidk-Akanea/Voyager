---
name: backlog
description: Ajoute proprement une entrée au backlog de Voyager (docs/backlog.md) — une tâche dans un épic, une nouvelle question ouverte numérotée, la réponse à une question, ou une idée en vrac. À utiliser quand l'utilisateur a une idée, un besoin, une question à trancher, ou tranche une question existante.
---

# Ajouter au backlog

Ce que l'utilisateur veut noter : $ARGUMENTS

1. Lis `docs/backlog.md` en entier, et `docs/glossaire.md` pour employer les bons termes.
2. Détermine le type d'entrée :
   - **Tâche** d'un épic existant (E0 à E9) → une case `- [ ]` dans l'épic qui convient, formulée
     comme une action (« Comparer plusieurs devis côte à côte »).
   - **Nouvel épic** → seulement si rien d'existant ne convient ; demande d'abord à l'utilisateur.
   - **Question ouverte** → une ligne dans le tableau, numéro suivant le plus grand `Q` existant,
     avec la question, son impact et le statut « Ouverte ».
   - **Réponse à une question** → garde la ligne et passe son statut à **Tranchée** avec la réponse
     en une phrase. Reporte la décision là où elle compte (vision, spec, ADR) si besoin, et
     signale-le.
   - **Idée en vrac** → une puce dans « Idées en vrac ».
3. Vérifie qu'une entrée équivalente n'existe pas déjà ; si oui, complète-la au lieu de dupliquer.
4. Si l'entrée introduit un nouveau terme métier, ajoute-le au glossaire avec son nom de code anglais.
5. Garde les tableaux alignés et lance `npx markdownlint-cli2`.
6. Dis à l'utilisateur ce que tu as ajouté et où, en une ou deux phrases. Ne committe pas : ce sera
   fait avec `/ship`.
