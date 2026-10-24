# Décisions d'architecture (ADR)

Chaque décision structurante est consignée dans un fichier `NNNN-titre.md`, à partir du
[modèle](_template.md). Pour en rédiger un avec Claude : `/new-adr <sujet>`.

Cycle de vie : **Proposée** → **Acceptée** (par l'utilisateur) ou **Rejetée**. Un ADR accepté n'est
plus modifié ; s'il devient caduc, un nouvel ADR le **remplace**.

| ADR                                                            | Titre                                                 | Statut   | Date       |
| -------------------------------------------------------------- | ----------------------------------------------------- | -------- | ---------- |
| [0001](0001-consigner-les-decisions.md)                        | Consigner les décisions                               | Acceptée | 2026-10-24 |
| [0002](0002-architecture-modulaire-par-contrats-et-plugins.md) | Architecture modulaire par contrats et plugins        | Acceptée | 2026-10-24 |
| [0003](0003-choix-de-la-stack.md)                              | Choix de la stack                                     | Acceptée | 2026-10-24 |
| [0004](0004-stockage-des-donnees.md)                           | Stockage des données                                  | Acceptée | 2026-10-24 |
| [0005](0005-architecture-du-code-hexagonale-par-domaine.md)    | Architecture du code : hexagonale, rangée par domaine | Acceptée | 2026-10-24 |
| [0006](0006-dependances-principales.md)                        | Dépendances principales                               | Acceptée | 2026-10-24 |
| [0007](0007-git-flow-et-versions-gitversion.md)                | Git Flow et versions calculées par GitVersion         | Acceptée | 2026-10-24 |
