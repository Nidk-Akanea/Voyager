---
name: update-instructions
description: Relit et met à jour les instructions de Claude pour Voyager — CLAUDE.md (racine et sous-dossiers), règles .claude/rules, agents, skills, hooks et réglages — pour qu'elles restent exactes, cohérentes, concises et adaptées au code réel ; décide s'il faut créer des CLAUDE.md dans l'arborescence. Propose les changements, les applique après accord. À lancer quand l'utilisateur le demande, typiquement après une étape importante (squelette, nouveau dossier, ADR accepté, conventions modifiées).
disable-model-invocation: true
---

# Mettre à jour mes instructions

Périmètre éventuel donné par l'utilisateur : $ARGUMENTS

Ces fichiers décident de la façon dont je travaille. **Je ne les modifie qu'après l'accord de
l'utilisateur**, et je préfère retirer une instruction inutile qu'en ajouter une de plus : une
instruction de trop coûte de l'attention à chaque échange.

## 1. Faire l'inventaire

- Tous les `CLAUDE.md` du dépôt (hors `node_modules`, `bin`, `obj`).
- `.claude/rules/*.md`, `.claude/agents/*.md`, `.claude/skills/*/SKILL.md`, `.claude/hooks/*`,
  `.claude/settings.json`.
- Les sources de vérité qu'ils résument : `docs/adr/` (index et statuts), `docs/conventions/`,
  `docs/glossaire.md`, `docs/backlog.md`.
- Ma mémoire sur ce projet, si elle est accessible (`MEMORY.md` et ses fichiers) : les retours de
  l'utilisateur qui y figurent sont des candidats à inscrire dans le dépôt.

## 2. Voir ce qui a changé depuis la dernière révision

- Dernière révision : `git log -1 --format="%h %ad %s" --date=short --grep="update instructions"`.
  À défaut, le dernier commit qui touche `CLAUDE.md` ou `.claude/`.
- Depuis ce commit : `git log --stat <commit>..HEAD` — nouveaux dossiers et projets, scripts de
  build et de test, ADR acceptés, conventions modifiées, fichiers supprimés.
- Les remarques de l'utilisateur dans les PR fusionnées depuis
  (`gh pr list --state merged --repo Nidk-Akanea/Voyager`, puis `gh pr view <n> --comments`).

## 3. Contrôler

| Contrôle          | Question                                                                                                                                                                                                                   |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Exactitude        | Chaque chemin, fichier, commande, agent, skill et numéro d'ADR cité existe-t-il ? La section « État actuel » et la section « Commandes » décrivent-elles le dépôt d'aujourd'hui ?                                          |
| Cohérence         | Les instructions contredisent-elles un ADR accepté ou une convention ? L'ordre d'autorité est : ADR, puis `docs/conventions/`, puis `.claude/rules/`, puis `CLAUDE.md`. Une règle résume et renvoie, elle ne diverge pas   |
| Concision         | `CLAUDE.md` racine dépasse-t-il ~200 lignes ? Un passage ne concerne-t-il que certains fichiers (à déplacer dans une règle ou un `CLAUDE.md` local) ? Un texte est-il répété à plusieurs endroits ?                        |
| Couverture        | Une correction de l'utilisateur revient-elle souvent sans être écrite ? Une décision prise en conversation manque-t-elle ? Une erreur que je refais a-t-elle sa règle ?                                                    |
| Règles            | Les motifs `paths:` correspondent-ils à des fichiers qui existent ? Une règle jamais chargée est morte                                                                                                                     |
| Agents et skills  | Les descriptions disent-elles clairement quand les utiliser ? Les outils sont-ils réduits au nécessaire ? Une skill prévue au backlog a-t-elle maintenant ce qu'il faut pour exister (ex. `/feature` après le squelette) ? |
| Hooks et réglages | Les scripts existent-ils et fonctionnent-ils encore (test rapide par l'entrée standard) ? Une permission manque-t-elle pour une commande que j'utilise tout le temps, ou est-elle trop large ?                             |

## 4. Décider des `CLAUDE.md` dans l'arborescence

Un `CLAUDE.md` placé dans un dossier n'est lu que quand je travaille dans ce dossier. Une règle
`.claude/rules/` avec `paths:` est chargée selon le type de fichier, où qu'il soit.

**Crée un `CLAUDE.md` local** pour un dossier qui a à la fois :

- ses propres commandes (build, test, lancement, génération) ;
- une structure ou des pièges qu'il faut connaître pour y travailler ;
- assez de code pour que j'y travaille souvent.

Candidats, quand ils contiendront du code : `src/dotnet/`, `src/ts/`, `proto/`, `plugins/`,
`tests/`, `deploy/`.

**Préfère une règle `.claude/rules/`** pour une convention liée à un type de fichier et valable dans
plusieurs dossiers (C#, TypeScript, `.proto`, Markdown).

Un `CLAUDE.md` local : 60 lignes au plus, ne répète pas la racine, commence par une phrase qui dit à
quoi sert le dossier, puis commandes, structure, pièges et liens vers la documentation. Pas de
`CLAUDE.md` dans un dossier vide ou presque vide.

## 5. Proposer

Présente à l'utilisateur, en français, un tableau des changements regroupés en **corrections**,
**suppressions**, **ajouts** et **nouveaux fichiers** : fichier, changement, raison. Indique ce que
chaque changement m'aidera à mieux faire. Demande lesquels appliquer.

S'il n'y a rien d'utile à changer, dis-le simplement et arrête-toi.

## 6. Appliquer

1. Sur une branche `chore/update-instructions`, applique uniquement les changements acceptés.
2. Mets à jour la section « Outils de Claude » et l'arborescence de `CLAUDE.md` si des agents,
   skills, règles ou `CLAUDE.md` locaux ont changé.
3. Si un retour de ma mémoire est maintenant écrit dans le dépôt, propose de le retirer de la mémoire.
4. Lance `npx markdownlint-cli2`.
5. Propose `/ship`, avec un commit intitulé `chore(claude): update instructions` : c'est ce titre qui
   sert de repère à la prochaine révision.
