# ADR-0010 — Hébergement : un serveur hébergé pour plusieurs groupes

- **Statut** : Acceptée
- **Date** : 2026-10-25
- **Remplace** : —

## Contexte

L'[ADR-0003](0003-choix-de-la-stack.md) prévoit un **serveur du groupe** partagé par les membres, et
l'[ADR-0004](0004-stockage-des-donnees.md) le dote de PostgreSQL. La question Q4 restait ouverte :
**qui fait tourner ce serveur ?**

Voyager s'adresse à des familles et des groupes d'amis, pas à des informaticiens. Or héberger un
serveur soi-même (Docker, machine allumée en permanence, accès depuis Internet, certificat,
sauvegardes) est hors de portée d'un utilisateur ordinaire. L'utilisateur a aussi décidé qu'il paierait
les API externes (Q12).

## Options

| Option | Pour les membres | Pour l'hébergeur | Limites |
| --- | --- | --- | --- |
| **A. Serveur hébergé en ligne** par le porteur du projet | Installer l'application, accepter une invitation | Un serveur dans le cloud qui accueille plusieurs groupes | Coût mensuel ; séparation stricte des groupes ; responsabilité des données |
| **B. Auto-hébergement Docker** | Idem | Utilisateurs techniques (NAS, VPS, serveur maison) | Accès depuis Internet à configurer soi-même |
| **C. Serveur intégré à l'application d'un membre** | Idem | Un membre coche « héberger ce groupe » | Son PC doit rester allumé ; accès depuis Internet toujours difficile |
| **D. Pas de serveur, dossier partagé** (OneDrive…) | Rien à installer de plus | Personne | Synchronisation fragile, dépendance à un service tiers |

## Décision

**Option A, choisie par l'utilisateur ; l'option B reste possible avec le même logiciel.**

1. **Un serveur hébergé, plusieurs groupes.** Le serveur du groupe devient un serveur **multi-groupes** :
   chaque donnée de voyage appartient à un groupe, et un membre ne voit que les groupes dont il fait
   partie. La séparation est garantie côté serveur, dans chaque requête, et vérifiée par des tests.
   Les données de référence partagées (ADR-0004) restent propres à chaque groupe.
2. **Les membres ne déploient rien** : ils installent l'application de bureau
   ([ADR-0009](0009-installation-et-mises-a-jour-velopack.md), si elle est acceptée) et rejoignent un
   groupe par une **invitation**.
3. **Livraison du serveur** : une image Docker et un fichier Docker Compose (serveur + PostgreSQL
   avec PostGIS). C'est ce que déploie l'hébergeur : le porteur du projet en ligne, ou un utilisateur
   technique chez lui (option B).
4. **Mode solo** : l'application fonctionne aussi **sans serveur**, avec le seul service local, pour
   essayer Voyager ou l'utiliser seul. Le passage au mode groupe se fait plus tard, en rejoignant ou
   en créant un groupe.
5. **Pour le moment** : le serveur tourne **en développement** sur le poste du développeur, orchestré
   par Aspire avec PostgreSQL dans Docker ([ADR-0011](0011-aspire-pour-le-developpement.md)). Le
   choix de l'hébergeur en ligne viendra quand il y aura une version à ouvrir à un groupe (question
   Q22 du backlog).

## Conséquences

- **L'authentification devient indispensable** avant toute mise en ligne : comptes des membres,
  invitations, appartenance aux groupes. Elle fera l'objet d'un ADR dédié, déjà prévu au backlog.
- **Le modèle de données** porte un identifiant de groupe sur toutes les données de voyage ; la
  synchronisation (ADR à venir) ne transmet à un membre que les données de ses groupes.
- **Données personnelles et RGPD** : en hébergeant les voyages d'autres personnes, le porteur du
  projet devient responsable de leur traitement — hébergement dans l'Union européenne, politique de
  confidentialité, export et suppression des données à la demande, sauvegardes chiffrées. À traiter
  dans l'ADR de mise en ligne.
- **Chiffrement en transit** : l'image du serveur parle HTTP sur le port 8080. En ligne, un proxy
  inverse (le programme qui reçoit les connexions HTTPS et les transmet au conteneur) est
  **obligatoire** devant elle, avec HTTPS imposé ; le serveur n'est jamais exposé directement.
- **Coût** : un petit serveur suffit au début (quelques euros par mois) ; il sera chiffré avec le
  choix de l'hébergeur.
- **Vision** : « chaque groupe a sa propre instance » devient « un serveur accueille plusieurs
  groupes » ; les paquets de données restent le moyen de partager entre groupes.
