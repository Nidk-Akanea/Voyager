# Décisions d'architecture (ADR)

Chaque décision structurante est consignée dans un fichier `NNNN-titre.md`, à partir du
[modèle](_template.md). Pour en rédiger un avec Claude : `/new-adr <sujet>`.

Cycle de vie : **Proposée** → **Acceptée** (par l'utilisateur) ou **Rejetée**. Un ADR accepté n'est
plus modifié ; s'il devient caduc, un nouvel ADR le **remplace**.

| ADR                                                              | Titre                                                   | Statut             | Date       |
| ---------------------------------------------------------------- | ------------------------------------------------------- | ------------------ | ---------- |
| [0001](0001-consigner-les-decisions.md)                          | Consigner les décisions                                 | Acceptée           | 2026-10-24 |
| [0002](0002-architecture-modulaire-par-contrats-et-plugins.md)   | Architecture modulaire par contrats et plugins          | Acceptée           | 2026-10-24 |
| [0003](0003-choix-de-la-stack.md)                                | Choix de la stack                                       | Acceptée           | 2026-10-24 |
| [0004](0004-stockage-des-donnees.md)                             | Stockage des données                                    | Remplacée par 0018 | 2026-10-24 |
| [0005](0005-architecture-du-code-hexagonale-par-domaine.md)      | Architecture du code : hexagonale, rangée par domaine   | Acceptée           | 2026-10-24 |
| [0006](0006-dependances-principales.md)                          | Dépendances principales                                 | Acceptée           | 2026-10-24 |
| [0007](0007-git-flow-et-versions-gitversion.md)                  | Git Flow et versions calculées par GitVersion           | Acceptée           | 2026-10-24 |
| [0008](0008-globalisation-fluent-et-linguini.md)                 | Globalisation : Fluent, avec Linguini côté .NET         | Acceptée           | 2026-10-24 |
| [0009](0009-installation-et-mises-a-jour-velopack.md)            | Installation et mises à jour : Velopack                 | Acceptée           | 2026-10-24 |
| [0010](0010-hebergement-serveur-heberge-multi-groupes.md)        | Hébergement : un serveur hébergé pour plusieurs groupes | Acceptée           | 2026-10-25 |
| [0011](0011-aspire-pour-le-developpement.md)                     | Aspire pour le développement                            | Acceptée           | 2026-10-25 |
| [0012](0012-journaux-de-developpement-dans-seq-avec-serilog.md)  | Journaux de développement écrits dans Seq avec Serilog  | Acceptée           | 2026-10-25 |
| [0013](0013-assemblage-du-paquet-de-l-application-de-bureau.md)  | Assemblage du paquet de l'application de bureau         | Acceptée           | 2026-10-26 |
| [0014](0014-ecran-de-demarrage-et-mise-a-jour-au-demarrage.md)   | Écran de démarrage et mise à jour au démarrage          | Proposée           | 2026-10-26 |
| [0015](0015-mecanisme-de-plugins.md)                             | Mécanisme de plugins : découverte, chargement et preuve | Acceptée           | 2026-10-26 |
| [0016](0016-installation-et-gestion-des-plugins.md)              | Installation et gestion des plugins                     | Acceptée           | 2026-10-29 |
| [0017](0017-contrats-versionnes-et-dependances-entre-plugins.md) | Contrats versionnés et dépendances entre plugins        | Acceptée           | 2026-11-02 |
| [0018](0018-client-serveur-et-copie-de-voyage.md)                | Client-serveur, et une copie de voyage sur le mobile    | Acceptée           | 2026-11-04 |
| [0019](0019-authentification-et-invitations.md)                  | Authentification, groupes et invitations                | Acceptée           | 2026-11-04 |
| [0020](0020-plugins-sur-le-serveur.md)                           | Les plugins sur le serveur                              | Acceptée           | 2026-11-04 |
