# ADR-0002 — Architecture modulaire par contrats et plugins

- **Statut** : Acceptée
- **Date** : 2026-10-24
- **Remplace** : —

## Contexte

L'expression de besoin pose une exigence forte : l'outil doit être **entièrement modulaire**.
Chaque élément répond à un contrat qu'on peut modifier, améliorer ou compléter par un plugin.

Plusieurs parties de Voyager varient beaucoup et vont s'enrichir dans le temps :

- **les types de prestation** : vol, hébergement, activité, transport, visa, et ceux qu'on n'a pas
  encore imaginés ;
- **les bases de calcul et règles de répartition** : par personne, par unité, forfait, part enfant…
- **les sources de données** : saisie manuelle, API de vols ou d'hôtels (souvent payantes), extraction
  de sites, import ;
- **les vues** : timeline, planning, carte, programme, graphiques ;
- **les exports** : PDF, tableur, calendrier.

Si ces éléments sont codés en dur dans le cœur, chaque ajout oblige à le modifier et à le retester.
La stack n'est pas encore choisie : cet ADR fixe le **principe**. Le mécanisme technique (format des
plugins, chargement, isolation) sera décidé avec la stack.

## Options

### Option A — Application monolithique classique

Les fonctionnalités sont développées directement dans l'application, avec un découpage en modules
internes.

- Avantages : plus simple et plus rapide à démarrer ; pas de contrats à stabiliser.
- Inconvénients : ne répond pas à l'exigence ; chaque nouveau type de prestation ou connecteur touche
  le cœur ; impossible pour un tiers d'ajouter une source de données.

### Option B — Cœur minimal + contrats + plugins

Le cœur ne connaît que des concepts abstraits (voyage, devis, ligne, montant, lieu…) et des **points
d'extension**. Chaque point d'extension a un **contrat** versionné. Les fonctionnalités, y compris
celles de base, sont livrées comme plugins qui implémentent ces contrats.

- Avantages : répond à l'exigence ; ajout d'une source ou d'une vue sans toucher au cœur ; les
  connecteurs payants ou fragiles (extraction web) sont isolés et remplaçables ; testabilité par
  contrat.
- Inconvénients : plus de conception au départ ; les contrats doivent être stables et versionnés ;
  risque de sur-abstraction si on crée des points d'extension « au cas où ».

## Décision

**Option B**, avec ces règles :

1. Le cœur définit le modèle commun (voir le [glossaire](../glossaire.md)) et les contrats ; il ne
   dépend d'aucun plugin.
2. Les fonctionnalités de base sont livrées comme des plugins, pour prouver que les contrats suffisent.
3. Chaque contrat est **versionné** ; une évolution incompatible crée une nouvelle version.
4. On crée un point d'extension quand **au moins deux implémentations** sont connues ou prévues
   (ex. deux sources de prix de vols). Sinon, on garde le code simple et on extrait plus tard.
5. Premiers points d'extension identifiés : type de prestation, base de calcul, règle de répartition,
   connecteur de données, vue, export.

## Conséquences

- La conception commence par le modèle commun et les contrats, avant les fonctionnalités.
- Le choix de stack devra être évalué sur sa capacité à charger des plugins et à les isoler.
- Questions à trancher plus tard : qui peut écrire des plugins, comment ils sont distribués et
  sécurisés (Q11 du [backlog](../backlog.md#questions-ouvertes)).
- Un ADR technique, après celui de la stack, décrira le mécanisme concret des plugins.
