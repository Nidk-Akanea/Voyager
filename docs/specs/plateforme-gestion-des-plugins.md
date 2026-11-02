# Spec — Gestion des plugins

- **Statut** : Validée
- **Épic** : E9 — Plateforme modulaire (transverse)
- **Date** : 2026-10-30

## Objectif

Aujourd'hui les plugins sont livrés avec l'application et l'utilisateur ne peut rien en faire. Cette
fonctionnalité lui donne la main : **voir** ses plugins, en **installer** depuis un fichier, les
**remplacer**, les **désinstaller**, les **activer** et les **désactiver**, et **retrouver** celui qui fait
planter Voyager sans perdre l'accès à son voyage. Elle applique à l'application de bureau le mécanisme décidé
par l'[ADR-0016](../adr/0016-installation-et-gestion-des-plugins.md), avec la compatibilité et les
dépendances **par contrat** de l'[ADR-0017](../adr/0017-contrats-versionnes-et-dependances-entre-plugins.md).
Elle **complète** la [spec de la preuve des plugins](plateforme-preuve-des-plugins.md) : la règle « il n'y a
pas de dossier de plugins de l'utilisateur » n'est plus vraie, et les règles de compatibilité par version du
SDK sont **remplacées** par la section « Compatibilité » ci-dessous. Les plugins d'un groupe, le catalogue en
ligne et la clé de signature de production n'en font pas partie (voir « Hors périmètre »).

## User stories

- En tant que **membre**, je veux voir tous mes plugins, d'où ils viennent, s'ils sont vérifiés et s'ils
  fonctionnent, afin de savoir ce qui tourne dans mon application.
- En tant que **membre**, je veux installer un plugin depuis un fichier que j'ai reçu en sachant ce que
  j'installe et ce que cela risque, afin de décider en connaissance de cause.
- En tant que **membre**, je veux remplacer un plugin par une autre version, ou le désinstaller, afin de
  garder mon application en ordre.
- En tant que **membre**, je veux désactiver un plugin sans le perdre, et le réactiver plus tard, afin de
  l'essayer ou de m'en passer un temps.
- En tant que **membre**, je veux qu'un fichier piégé ne puisse ni abîmer mon ordinateur à l'installation
  ni effacer mes voyages, afin de pouvoir examiner un paquet sans crainte.
- En tant que **membre**, je veux que Voyager démarre malgré un plugin qui le fait planter, et que
  l'application m'aide à trouver lequel, afin de ne pas rester bloqué dehors.
- *(Étape 2)* En tant que **membre**, je veux savoir quels plugins dépendent d'un plugin avant de le
  désactiver ou de le désinstaller, afin de ne pas en casser d'autres sans le savoir.
- *(Étape 2)* En tant que **membre**, je veux être prévenu avant qu'une mise à jour de l'application
  rende un de mes plugins inutilisable, afin de décider de l'appliquer ou non.
- En tant qu'**auteur de plugin**, je veux fabriquer un paquet de plugin à partir d'un plugin construit,
  afin de le partager.

## Décisions déjà prises

- Elles viennent de l'ADR-0016. **L'import d'un fichier est permis avec un avertissement**, sans réglage
  préalable : c'est un choix de l'utilisateur, et un plugin importé est « Non vérifié », du code de tiers
  sans isolation accepté à ses risques.
- **Un plugin fautif se trouve en désactivant les plugins au fur et à mesure** : l'utilisateur l'a demandé
  en répondant à la question Q26 (on propose le suspect, et quand il y en a plusieurs on démarre sans les
  plugins installés et on les réactive un à un).
- Les changements s'appliquent au **prochain démarrage du service local**, avant qu'aucun plugin ne soit
  chargé.
- **Aucune limite de taille fixe** pour un paquet : la plupart des plugins apporteront des données, parfois
  volumineuses. Un écart assumé avec l'ADR-0016, qui prévoyait des limites « dont la spec fixerait les
  valeurs » : seule l'espace disque disponible borne l'extraction. Les autres protections (chemins, contenu
  autorisé) restent.
- **Pas de « Démarrer sans aucun plugin »** : si tout est un plugin, l'application serait vide. Un plugin
  **livré** qui planterait est un défaut de l'application, corrigé par une mise à jour.
- **Confirmation renforcée** : on garde la version simple (rappel du soupçon, case « Je comprends »,
  « Annuler » par défaut) ; retaper l'identifiant après un deuxième plantage sera **revu à l'usage**.
- Acceptés tels que proposés : les **demandes successives** (la dernière remplace la précédente, une demande
  en attente s'annule, le paquet reste dans la boîte jusqu'à ce qu'on le retire) ; l'**état après un
  remplacement** (il garde son état, la suspension est levée, la même version se réinstalle) ; une
  **signature dont la clé est inconnue** (le paquet reste « Non vérifié ») ; la page « **À propos** »
  inchangée (chargés et écartés seulement) ; le **mode de rétablissement** (les plugins installés passent
  à « désactivé pour le rétablissement » et se réactivent un à un, avec leurs prérequis à l'étape 2).
- **Dépendances et compatibilité par contrat** (ADR-0017, choix de l'utilisateur) : un plugin fournit et
  requiert des **contrats** versionnés, jamais un plugin précis ; la compatibilité se juge contrat par
  contrat, avec des **adaptateurs** pour garder les anciennes versions quand c'est possible, et une version
  qui ne peut pas s'adapter devient **obsolète**, au cas par cas. Plusieurs fournisseurs par défaut,
  toute exigence obligatoire, un contrat à deux moitiés (le service .NET et l'écran) versionnées
  ensemble.
- **Avant d'appliquer une mise à jour** de l'application, on contrôle et on informe ; l'utilisateur accepte,
  ou refuse et l'application se lance dans la version où elle est.

### Deux étapes de construction

- **Étape 1** : tout ce que décrit cette spec, **sauf** les sections « Dépendances entre plugins » et
  « Mise à jour de l'application ». Les contrats **requis** d'un plugin sont alors le **contrat de base**
  (`plugin`, ADR-0017) et ceux que fournit l'application ; aucune exigence n'existe entre plugins. Un plugin
  qui requiert un autre contrat est écarté avec « contrat non pris en charge » (version inconnue : il lui faut
  une version de Voyager qui gère les dépendances), jamais chargé sans son prérequis. La compatibilité par contrat (section « Compatibilité ») en fait partie : elle remplace la version du SDK. Les
  critères d'un contrat **déprécié** se testent avec un contrat d'essai déclaré dans les tests, tant qu'aucun
  vrai contrat n'a évolué.
- **Étape 2, dépendances** : construites au **premier plugin qui en consomme un autre** (ADR-0017,
  décision 8).
- **Étape 2, mise à jour de l'application** : construite à la **première obsolescence réelle d'un contrat** et
  **après l'amendement de l'ADR-0014** (encore proposée, donc modifiable sans nouvel ADR). L'ADR-0017 la
  range parmi ce qu'on fait « maintenant » ; la spec la repousse, puisque sans version de contrat retirée
  aucun plugin n'est jamais touché : **à confirmer** (questions ouvertes).

## Règles métier

### Ce que la vue montre

1. Un écran **« Plugins »** liste tous les plugins connus de l'application. Pour chacun : son identifiant,
   sa version, son **origine**, sa **vérification** et son **état**. La page « À propos » reste une liste
   en lecture seule de ce que la spec de la preuve a défini : les plugins **chargés ou écartés**, avec leurs
   faits. Un plugin désactivé, suspendu ou en attente de redémarrage n'y figure pas ; l'écran « Plugins »
   les montre. S'ils sont tous désactivés, « À propos » affiche le message « aucun plugin ».
2. **Origine** : « Livré avec Voyager » (dans l'application) ou « Installé par vous » (depuis un fichier).
3. **Vérification** : un plugin livré est « Garanti par l'application » ; un plugin installé depuis un
   fichier est **« Non vérifié »**. Aucun paquet n'est « Vérifié » tant qu'aucune clé de signature de
   production n'est décidée (question Q31 du backlog).
4. **État** : chargé ; écarté, avec la raison ; désactivé ; suspendu après un plantage ; en attente de
   redémarrage (une demande est enregistrée et pas encore appliquée) ; **demande en échec**, avec sa raison.
   (« En erreur » garde son sens de la spec de la preuve : un plugin qui échoue pendant un appel.)
   **Raisons d'écartement** : manifeste invalide ; identifiant en double ; **contrat non pris en charge** (un
   contrat que le plugin fournit, ou son contrat de base, est dans une version que l'application ne prend pas
   en charge : retirée, ou inconnue d'elle) ; échec du chargement ; inscription non déclarée ; et, à l'étape 2,
   **prérequis non fourni**, **dépendance circulaire** et **autre fournisseur actif** (cette dernière est une
   proposition).
5. Le détail d'un plugin dit aussi **ce qu'il apporte** (une partie d'interface, du code .NET, ou les deux) et
   les **contrats** qu'il fournit et ceux qu'il requiert, avec leur version et leur état (voir
   « Compatibilité »). Pour un plugin installé, il montre sa **date d'installation** et le **nom et
   l'empreinte du paquet d'origine**.
6. Les textes sont en **français et en anglais**. Tout texte venu d'un paquet (identifiant, version,
   description, nom de fichier) est **nettoyé** partout où il est montré : longueur limitée, sans retours à
   la ligne ni caractères de contrôle ou d'écriture bidirectionnelle. Aucun chemin du disque, nom de compte
   ni nom de machine n'apparaît dans les listes, les raisons ou les journaux.
7. L'**interface** d'un plugin écarté, désactivé ou suspendu n'est jamais affichée.
8. L'écran « Plugins » et la gestion **ne dépendent d'aucun plugin** : ils restent disponibles quand tous
   les plugins sont désactivés.

### Livrés et installés

1. Un plugin **livré** ne peut ni être désinstallé ni être modifié ; il peut être **désactivé**.
2. Un plugin **installé** est rangé dans les données de l'utilisateur : il **reste** après une mise à jour
   de l'application, qui n'efface que ce qu'elle a livré.
3. Un identifiant ne peut être actif qu'une fois : les plugins livrés sont lus d'abord, puis les installés.
   Un plugin installé qui porte l'identifiant d'un plugin livré (installé avant que l'application le
   livre, ou importé tel quel) est **écarté** avec la raison « identifiant en double : déjà livré avec
   l'application », que la vue explique. **L'utilisateur choisit lequel garder** : en **désactivant le
   plugin livré**, il laisse la place à l'installé après le redémarrage.
4. À l'installation d'un paquet dont l'identifiant est déjà livré, la boîte de confirmation **prévient** de
   ce doublon (il n'est pas refusé).

### Installer depuis un fichier

1. Un plugin se transmet sous forme d'un **paquet de plugin** : un fichier `.voyagerplugin`.
2. Deux façons de le fournir : le bouton **« Ajouter un paquet… »** (on choisit un fichier, dont l'extension
   et la taille sont vérifiées avant qu'il soit copié dans la **boîte de dépôt**) ou le dépôt à la main dans
   la boîte de dépôt, qu'un bouton **ouvre**. La vue liste les paquets de la boîte de dépôt (nom nettoyé,
   taille, date) ; les fichiers qui n'ont pas l'extension d'un paquet sont ignorés.
3. **Aucun réglage n'est nécessaire** pour importer : l'avertissement ci-dessous en tient lieu.
4. Quand l'utilisateur choisit **« Installer »** sur un paquet, Voyager en fait une **copie de
   préparation**, privée, et c'est cette copie qui est **examinée**, puis appliquée : le fichier de la boîte
   peut ensuite changer ou disparaître sans effet. Rien n'est installé à l'examen.
5. L'examen **refuse** le paquet, avec sa raison, si : l'archive est illisible ; le manifeste est invalide ou ne
   déclare aucune partie (ni .NET ni interface) ; l'identifiant est mal formé ou réservé (les noms de
   périphériques de Windows) ; un contrat qu'il fournit, ou son contrat de base, est dans une version que
   l'application ne prend pas en charge ; il contient un fichier ou un nom de fichier non autorisé ;
   l'espace disque disponible ne suffit pas à l'extraire ; sa signature ne correspond pas à son contenu pour
   une clé que l'application connaît. Une signature dont la clé est inconnue ne fait pas refuser le paquet :
   il reste « Non vérifié ». Un contrat **déprécié** ne le fait pas refuser : la boîte le signale (voir
   « Compatibilité »).
6. Un paquet accepté est présenté dans une **boîte de dialogue de confirmation**, propre au système et non à
   la page : le **nom du fichier**, son **empreinte** (SHA-256), l'identifiant, la version, les contrats qu'il
   fournit et ceux qu'il requiert, ce qu'il apporte, sa vérification, l'éventuel doublon avec un plugin
   livré, et, **avant** la case à cocher et à **chaque** installation, l'**avertissement** : *un plugin .NET
   s'exécute avec vos droits sur cet ordinateur ; un plugin d'interface peut parler au service de Voyager et
   lire vos voyages ; l'auteur de ce plugin n'est pas vérifié*.
7. Le bouton par défaut est **« Annuler »**. Pour installer ou remplacer un plugin « Non vérifié »,
   « Installer » n'est actif qu'une fois la case **« Je comprends »** cochée. Annuler n'enregistre rien et
   **supprime la copie de préparation**.
8. Confirmer **enregistre une demande** (en entier ou pas du tout) ; le plugin passe à l'état « en attente
   de redémarrage ». Il ne s'exécute qu'après le redémarrage (voir « Appliquer les changements »).
9. Installer un plugin dont l'identifiant est **déjà installé** le **remplace** : la confirmation dit
   « remplace la version X par la version Y », et, pour un plugin non signé, que **rien ne prouve que l'auteur
   de la nouvelle version soit celui de l'ancienne**. Une version **plus ancienne** est une opération à
   part, avertie (« installer une version plus ancienne ? »). La **même version** est proposée comme une
   réinstallation. Un remplacement **garde l'état activé ou désactivé** du plugin ; il **lève** sa
   suspension, puisque le code a changé (questions ouvertes).

### Désinstaller, activer, désactiver

1. **Désinstaller** ne concerne que les plugins installés ; elle demande une confirmation (même boîte de
   dialogue) et s'applique au redémarrage. Les fichiers du plugin sont supprimés. **Désinstaller ou
   désactiver ne supprime jamais de données de voyage** (question Q34 pour les données propres aux plugins).
2. **Désactiver** (plugin livré ou installé) n'a pas besoin de confirmation : cela réduit ce qui s'exécute,
   sauf si d'autres plugins en dépendent (étape 2, « Dépendances entre plugins »).
   Le plugin n'est plus chargé au démarrage suivant ; il reste dans la liste et on le réactive quand on
   veut. Tant qu'on n'a pas redémarré, **rien ne change** : l'interface d'un plugin affichée à cet instant
   le reste jusqu'au redémarrage.
3. **Activer** ou **réactiver** demande la confirmation de la boîte de dialogue (sans la case « Je
   comprends », que seule l'installation d'un plugin « Non vérifié » exige).
4. Par plugin, **la dernière demande remplace la précédente** (proposition, voir les questions ouvertes) :
   désactiver puis activer annule les deux ; installer puis désinstaller annule l'installation ; désinstaller
   puis installer est un remplacement ; installer deux versions retient la seconde. Remplacer une demande
   d'installation **supprime sa copie de préparation**. Une demande **en attente** peut aussi être
   **annulée** tant que le redémarrage n'a pas eu lieu.

### Appliquer les changements

1. Rien ne change tant qu'on ne **redémarre** pas : la vue le dit (« n demandes en attente ») et propose
   **« Redémarrer pour appliquer »**, qui relance le service local et recharge la page ; l'application ne se
   ferme pas. Les écrans ouverts sont remplacés (question Q23). **Fermer puis rouvrir** l'application
   applique aussi les demandes, qui survivent à la fermeture. Une seule instance de l'application gère les
   plugins (en développement comme installée).
2. Au démarrage, les demandes sont appliquées **avant que le moindre plugin soit chargé**, puis les plugins
   activés sont chargés. Rien n'est posé sans avoir été revérifié : l'**empreinte** de la copie de
   préparation doit encore être celle de la confirmation.
3. Poser ou remplacer un plugin est **tout ou rien** : un arrêt au milieu est terminé ou annulé au
   démarrage suivant, jamais laissé à moitié. Une demande qui échoue donne l'état **« demande en échec »**
   avec sa raison ; les autres demandes et les autres plugins ne sont pas gênés. Les copies de préparation
   orphelines (annulation, arrêt avant confirmation, demande remplacée) sont nettoyées.

### Compatibilité

1. Un **contrat** a des **versions** (`1`, `2`…). L'application en prend en charge certaines, directement ou
   par un **adaptateur**. Une version est **prise en charge**, **dépréciée** (elle fonctionne, et la vue
   avertit qu'elle sera retirée) ou **obsolète** (retirée). « **Non pris en charge** » désigne une version
   obsolète ou inconnue de cette application. Le contrat de base d'un plugin (`plugin`, ADR-0017) est un
   contrat comme les autres. Un contrat est **une seule chose à deux moitiés**, le service .NET et l'écran :
   elles se versionnent ensemble. L'**état** de chaque version (prise en charge, dépréciée, obsolète) est
   **déclaré dans le SDK**.
2. Un plugin installé dont un contrat **qu'il fournit**, ou son contrat de base, n'est pas pris en charge
   (par exemple après une mise à jour de l'application) est **écarté** avec la raison **« contrat non pris en
   charge »**, qui nomme tous les contrats concernés et leur version. Un seul suffit : le plugin entier est
   écarté et **aucun** de ses contrats n'est plus fourni. Il **reste installé** : on peut le désinstaller, ou
   le remplacer par une version qui utilise un contrat pris en charge. La vue **l'explique**, avec une ligne de
   détail qui dit **quoi faire** : « version inconnue » (le plugin est trop récent : mettre à jour Voyager) ou
   « obsolète » (le plugin est trop ancien : le mettre à jour). Il n'y a **qu'une raison**, et non deux comme
   dans l'ADR-0017 : l'utilisateur a la même information, et la liste des raisons reste courte.
3. Un plugin qui utilise un contrat **déprécié** fonctionne : la vue montre « ce plugin utilise un contrat
   qui sera retiré », et la boîte de confirmation d'une installation le signale.
4. Pour un plugin **livré**, un contrat obsolète est un défaut de l'application, corrigé par le projet :
   l'avertissement vise les plugins installés.
5. Un contrat **fourni par l'application** est toujours présent et ne dépend d'aucun plugin : il ne peut être
   qu'obsolète ou inconnu, jamais « absent ».
6. Un contrat **requis** est satisfait par un fournisseur dans **la même version**, ou dans une autre version
   quand l'application sait l'adapter ; sinon il n'est pas fourni (étape 2). « Contrat non pris en charge »
   vise ce que le plugin **fournit** et son contrat de base ; « prérequis non fourni » vise ce qu'il
   **requiert**.

### Dépendances entre plugins (étape 2)

Un plugin peut **requérir** un contrat qu'un autre plugin (ou l'application) **fournit**. Aucun plugin livré
n'en requiert un autre aujourd'hui ; les règles s'appliquent dès le premier.

1. L'écran « Plugins » montre, pour chaque plugin, les contrats qu'il **fournit** et ceux qu'il **requiert**,
   et, pour un plugin qui en fournit, **les plugins qui en dépendent**.
2. Un plugin n'est chargé qu'**après** ceux dont il requiert un contrat (l'ordre est celui des dépendances,
   non « livrés d'abord »). S'il requiert un contrat que **personne ne fournit** (absent, désactivé, écarté
   lui-même, ou fourni dans une version que l'application ne sait pas adapter), il est **écarté** avec la
   raison **« prérequis non fourni »**, qui nomme le contrat et sa version ; l'écartement **se propage** à
   ceux qui en dépendent. Un installé écarté pour « identifiant en double » ne fournit pas ses contrats :
   ses dépendants sont écartés de même. Deux plugins qui se requièrent l'un l'autre, ou un cycle plus long,
   sont écartés pour **« dépendance circulaire »** ; un plugin qui dépend d'un cycle sans en faire partie est
   écarté pour « prérequis non fourni ». Un plugin qui fournit et requiert le même contrat ne se reçoit pas
   lui-même (ce n'est ni un cycle ni une exigence satisfaite). Un plugin écarté pour un prérequis **revient
   seul** au redémarrage où le prérequis est de nouveau fourni.
3. **Désactiver, désinstaller ou remplacer** un plugin dont d'autres dépendent : la boîte de confirmation
   **liste les plugins qui seraient écartés**, c'est-à-dire ceux qui perdraient **leur seul fournisseur** d'un
   contrat (directement ou en cascade). Désactiver, qui n'en demande pas d'ordinaire, **demande une
   confirmation** dans ce cas, avec « Annuler » par défaut (proposition). Remplacer signale aussi une nouvelle
   version qui fournit un contrat dans une **autre version**, ou qui **ajoute** un prérequis.
4. **Installer** un plugin dont un prérequis manque n'est pas refusé (proposition) : la boîte prévient qu'il
   restera **écarté** tant que le contrat n'est pas fourni.
5. Ces avertissements se calculent sur l'**état projeté après les demandes en attente**, dans les deux sens
   (installer `B` quand la désinstallation de `A` est en attente ; désinstaller `A` quand l'installation de
   `B` est en attente). Toutes les demandes s'appliquent avant le chargement : leur ordre est sans effet. C'est
   le **processus principal**, et non la page, qui calcule les dépendants et ouvre la boîte.
6. **Plusieurs plugins** peuvent fournir le même contrat ; le consommateur les reçoit tous, et un fournisseur
   qui échoue est ignoré sans gêner les autres. Un contrat qui n'admet qu'**un seul** fournisseur laisse
   l'**utilisateur choisir** lequel est actif : c'est une **demande** comme les autres, confirmée par la boîte
   de dialogue du système et appliquée au redémarrage ; les autres fournisseurs sont écartés
   (« autre fournisseur actif »). Tant que l'utilisateur n'a **pas choisi**, ou si le fournisseur actif
   **disparaît** (désinstallé, écarté), **aucun** fournisseur n'est actif et les consommateurs sont écartés
   (« prérequis non fourni »).
7. **Rétablissement** : réactiver un plugin impose de réactiver **ceux dont il dépend, directement ou
   indirectement** (fermeture de dépendances) ; la boîte les liste et ils se réactivent ensemble, avec la
   confirmation renforcée si l'un d'eux est suspendu. Quand **plusieurs** plugins fournissent un contrat
   requis, l'utilisateur **choisit dans la boîte** lequel réactiver. Un plugin dont le prérequis est suspendu ou désactivé
   apparaît **« écarté »**, pas « suspendu », avec la raison « prérequis non fourni » et le prérequis nommé.
8. Toute exigence est **obligatoire** : il n'y a pas de dépendance facultative.
9. Un plugin **livré** ne requiert que des contrats **fournis par l'application ou par d'autres plugins
   livrés**, jamais par un plugin installé : le rétablissement, qui garde les livrés, ne les écarte ainsi
   jamais. Un plugin installé peut requérir n'importe quel contrat. Un test d'architecture le contrôle.

### Mise à jour de l'application (étape 2)

Cette section **suppose l'amendement de l'ADR-0014** (la mise à jour n'est plus imposée quand des plugins sont
touchés) ; elle ne s'applique pas tant qu'il n'est pas fait. Le contrôle se construit **quand le premier
contrat en version 2 est créé** : avant cela, aucun plugin n'est jamais touché par une mise à jour.

1. Quand l'application **trouve une mise à jour**, avant de l'appliquer, elle **contrôle** si des plugins
   installés seraient écartés parce qu'un contrat qu'ils **fournissent** devient obsolète, ou, en cascade,
   parce qu'un prérequis cesse d'être fourni. S'il y en a, elle **informe** l'utilisateur : la liste des
   plugins touchés et la raison de chacun. Hors connexion, il n'y a pas de mise à jour et le contrôle est sans
   objet.
2. S'il **accepte**, la mise à jour s'applique ; les plugins touchés sont alors écartés (« contrat non pris en
   charge », ou « prérequis non fourni » pour leurs dépendants). S'il **refuse**, l'application **se lance dans
   la version où elle est** et rien ne change. Au démarrage suivant, quand l'application retrouve la même mise
   à jour, la **question est reposée** ; un refus ne bloque jamais le démarrage.
3. Quand **aucun** plugin n'est touché, la mise à jour s'applique comme aujourd'hui, sans question.
4. Le contrôle dit si un plugin **serait accepté** ; il ne prouve pas que son comportement reste correct, et
   le message le dit.

### Quand Voyager plante à cause d'un plugin

1. Au redémarrage du service après un plantage, un plugin est **désigné** quand il était **en cours de
   chargement** (certain), ou quand un **appel** à lui était en cours et qu'il était **le seul** dans ce cas.
   Un appel qui dépasse son délai (spec de la preuve) est signalé « en erreur » et n'est pas un plantage.
2. Quand **un seul** plugin est désigné, il est **suspendu** (non chargé au démarrage suivant, sans quoi le
   plantage recommencerait) et la vue **propose** de le désactiver pour de bon ou de le réactiver : « Ce
   plugin est soupçonné d'avoir fait planter Voyager ». La suspension **dure** jusqu'à ce que l'utilisateur
   décide.
3. Quand **plusieurs** plugins sont désignés, **aucun**, ou que Voyager n'arrive plus à démarrer (l'écran
   d'échec), rien n'est suspendu seul : l'écran propose, en plus de « Réessayer », **« Démarrer sans les
   plugins installés »**. Tous les plugins installés passent alors à l'état « désactivé », avec la mention
   « désactivé pour le rétablissement » ; les plugins livrés restent chargés ; l'écran « Plugins » est
   utilisable, et l'utilisateur **réactive les plugins installés un à un**, en redémarrant à chaque fois,
   jusqu'à retrouver celui qui fait planter (à l'étape 2, avec les plugins dont ils dépendent).
4. **Réactiver un plugin suspendu** demande une **confirmation renforcée** : la boîte rappelle le soupçon,
   la case « Je comprends » est obligatoire et le bouton par défaut est « Annuler ».
5. La vue dit clairement **ce qui change** quand un plugin est suspendu ou désactivé (ce qu'il apportait
   n'est plus disponible). L'avertissement avant d'enregistrer des données qui dépendent d'un plugin absent
   viendra avec le métier (question Q30).
6. Un **arrêt normal** (l'utilisateur ferme l'application, la mise à jour de l'application la relance) n'est
   pas un plantage : aucun plugin n'est suspendu pour cela. Un arrêt brutal (coupure de courant) peut faire
   désigner un plugin à tort : la vue parle d'un **soupçon**, jamais d'une certitude.
7. Cette aide protège des **accidents**, pas de la malveillance : un plugin installé garde les droits de
   l'utilisateur et peut faire planter Voyager pendant qu'un autre est appelé. La vue ne la présente pas
   comme une sécurité.
8. Un plantage qui survient chez un fournisseur pendant l'appel d'un consommateur reste attribué au plugin
   que **l'application** appelait ; la réactivation un à un est le filet (étape 2).

### Garanties de sécurité

1. **Étendue de la garantie.** Les garanties ci-dessous valent **contre la page et le module d'interface**
   d'un plugin. Elles **ne protègent pas** d'un plugin .NET déjà installé, qui a les droits de
   l'utilisateur sur son poste. Un module peut encore **demander** l'ouverture d'une confirmation ; désactiver un
   plugin **sans dépendant** ou annuler une demande n'en demande pas.
2. **Aucun geste qui ajoute du code ou le rend actif** (installer, remplacer, désinstaller, activer, choisir le fournisseur actif) n'a
   d'effet sans la confirmation de la boîte de dialogue du système : le module d'un plugin ne peut ni la
   simuler ni la confirmer à la place de l'utilisateur. Seule une confirmation de l'utilisateur crée une
   demande.
3. La page ne désigne **jamais un chemin** du disque : elle nomme un paquet de la boîte de dépôt.
4. Un paquet est **extrait dans un dossier temporaire**. Sont acceptés : `plugin.json` ; les assemblies .NET
   (`.dll`, avec leurs fichiers `.pdb`, `.deps.json` et `.xml`), jamais les bibliothèques natives
   (question Q35) ; dans `ui/`, des fichiers `.js`, `.mjs`, `.css`, `.json`, `.svg` et `.png` ; dans
   `i18n/`, des fichiers `.ftl` ; et dans **`data/`**, des fichiers de données **de tout type** : la plupart des
   plugins apporteront des données, que Voyager ne sert jamais à l'interface et n'exécute jamais. Est refusé
   tout chemin qui sort du dossier du plugin, tout nom propre à Windows qui désigne autre chose qu'un
   fichier (périphériques, flux), tout doublon qui ne diffère que par la casse et tout lien.
5. **Aucune limite de taille fixe** (ni pour le paquet, ni pour le contenu extrait, ni pour le nombre de
   fichiers) : des données volumineuses sont attendues. Voyager **vérifie l'espace disque disponible** avant
   d'extraire et **s'arrête proprement** si le disque se remplit : le dossier temporaire et la copie de
   préparation sont alors supprimés et la demande est « en échec ». Ce qui reste, c'est l'avertissement : un
   plugin importé peut aussi faire du mal à l'ordinateur par son code, bien plus qu'en le remplissant.
6. Les journaux notent l'identifiant, la version et l'empreinte d'un paquet, **jamais son chemin ni son
   nom de fichier**.

### Fabriquer un paquet

1. Une commande **fabrique** un fichier `<identifiant>-<version>.voyagerplugin` à partir d'un plugin
   construit, et affiche son **empreinte**. Un paquet fabriqué puis installé donne un plugin qui
   fonctionne comme celui d'origine. La commande refuse un plugin qui n'est pas construit, et ne met dans
   le paquet que ce que la règle 4 des garanties accepte. La signature, facultative, n'est pas utilisée
   tant que la clé de production n'est pas décidée.
2. Un **paquet corrompu** (chemins hostiles, archive qui explose à la décompression) ne figure jamais dans
   l'application : il sert seulement à vérifier les refus de la règle 5 de « Installer depuis un fichier ».

## Critères d'acceptation

### Voir

- **Étant donné** l'application installée avec ses deux plugins livrés, **quand** j'ouvre l'écran
  « Plugins », **alors** je vois `diagnostics-runtime` et `diagnostics-clock`, chacun « Livré avec
  Voyager » et « Garanti par l'application », avec sa version et l'état « chargé », sans action
  « Désinstaller ».
- **Étant donné** un plugin écarté (par exemple un contrat non pris en charge), **quand** j'ouvre l'écran, **alors**
  il apparaît avec sa raison, dans la langue de l'application, la vue explique la raison, et les autres
  plugins sont chargés.
- **Étant donné** tous les plugins désactivés, **quand** j'ouvre la page « À propos », **alors** elle
  affiche le message « aucun plugin » et l'écran « Plugins » reste utilisable.
- **Étant donné** un plugin installé, **quand** j'ouvre son détail, **alors** je vois ce qu'il apporte, sa
  date d'installation, et le nom et l'empreinte de son paquet d'origine.
- **Étant donné** des textes de manifeste très longs ou piégés (retours à la ligne, écriture
  bidirectionnelle), **quand** l'écran ou la boîte de dialogue les montrent, **alors** ils sont tronqués à
  100 caractères au plus par champ et nettoyés.

### Désactiver et activer

- **Étant donné** `diagnostics-clock` chargé, **quand** je le désactive, **alors** aucune confirmation
  n'est demandée, il est « en attente de redémarrage », son interface reste affichée jusque-là, et après
  « Redémarrer pour appliquer » il apparaît « désactivé », son interface n'est plus affichée, il ne figure
  plus dans la page « À propos » où l'autre plugin reste, et mes données de voyage sont intactes.
- **Étant donné** un plugin désactivé, **quand** je demande de l'activer, **alors** la boîte de dialogue
  du système me demande confirmation ; refusée, rien ne change ; confirmée, après le redémarrage il est de
  nouveau « chargé ».
- **Étant donné** une demande en attente, **quand** je l'annule avant de redémarrer, **alors** le plugin
  reprend son état d'avant et aucun redémarrage n'est nécessaire.
- **Étant donné** un plugin installé qui porte l'identifiant d'un plugin livré, **quand** je démarre,
  **alors** il est « écarté : identifiant en double, déjà livré » avec l'explication ; **quand** je
  désactive le plugin livré et que je redémarre, **alors** l'installé est chargé.

### Installer

- **Étant donné** un paquet valide dans la boîte de dépôt, **quand** je choisis de l'installer, **alors**
  la boîte de dialogue montre, dans cet ordre, l'avertissement, puis le nom du fichier, l'empreinte,
  l'identifiant, la version, les contrats fournis et requis, ce que le plugin apporte et « Non vérifié » ; le bouton par défaut est
  « Annuler » et « Installer » attend la case « Je comprends ». L'avertissement existe en français et en
  anglais.
- **Étant donné** cette boîte, **quand** je clique « Annuler », **alors** aucune demande n'est enregistrée
  et la copie de préparation est supprimée.
- **Étant donné** cette boîte, **quand** je confirme, **alors** le plugin est « en attente de
  redémarrage » ; après « Redémarrer pour appliquer », il est « chargé », « Installé par vous » et
  « Non vérifié », et, s'il en fournit, ses faits apparaissent dans la page « À propos ».
- **Étant donné** que je retire le paquet de la boîte de dépôt après avoir confirmé, **quand** je
  redémarre, **alors** le plugin est quand même installé (la copie de préparation sert).
- **Étant donné** un paquet qui contient un chemin sortant du dossier (`..`), un nom réservé de Windows,
  un fichier hors de la liste autorisée, une bibliothèque native ou un doublon de casse, **quand** je
  l'examine, **alors** il est refusé avec sa raison et rien n'est posé.
- **Étant donné** un paquet dont l'identifiant est mal formé ou réservé, qui ne déclare aucune partie, ou
  dont l'archive est illisible, **quand** je l'examine, **alors** il est refusé avec sa raison.
- **Étant donné** un paquet volumineux (plusieurs centaines de Mo de données dans `data/`) et un disque qui
  a la place, **quand** je l'installe, **alors** il est accepté sans limite de taille.
- **Étant donné** un paquet que le disque ne peut pas contenir, **quand** je l'examine ou que je
  l'applique, **alors** l'opération s'arrête proprement, le dossier temporaire et la copie de préparation
  sont supprimés, et le plugin est en « demande en échec » avec la raison « espace disque insuffisant ».
- **Étant donné** un fichier de données de tout type dans `data/`, **quand** j'installe le paquet, **alors**
  il est accepté ; **étant donné** un fichier du même type dans `ui/`, hors de la liste, **alors** le paquet
  est refusé.
- **Étant donné** un paquet dont un contrat est dans une version que l'application ne prend pas en charge,
  **quand** je l'examine, **alors** il est refusé avec « contrat non pris en charge » (le contrat et la version
  sont nommés).
- **Étant donné** un paquet qui utilise un contrat déprécié, **quand** je l'examine, **alors** il est accepté
  et la boîte signale que ce contrat sera retiré.
- **Étant donné** un paquet signé avec une clé que l'application connaît (une clé d'essai, dans les tests),
  dont le contenu a été modifié après la signature, **quand** je l'examine, **alors** il est refusé ;
  **étant donné** un paquet signé avec une clé inconnue, **alors** il est accepté et reste « Non vérifié ».
- **Étant donné** un paquet dont l'identifiant est celui d'un plugin livré, **quand** je l'examine,
  **alors** la boîte me prévient du doublon sans refuser le paquet.
- **Étant donné** le paquet déjà installé en version 1, **quand** j'installe la version 2, **alors** la
  confirmation dit qu'elle remplace la version 1 par la version 2 et que l'auteur n'est pas vérifié ; pour
  une version plus ancienne, elle demande une confirmation distincte ; pour la même version, elle propose
  une réinstallation.
- **Étant donné** un plugin installé désactivé, **quand** je le remplace, **alors** il reste désactivé.

### Désinstaller et appliquer

- **Étant donné** un plugin installé, **quand** je le désinstalle et que je redémarre, **alors** il a
  disparu de la liste et du disque, et la base de données de mes voyages n'a pas été touchée.
- **Étant donné** une demande d'installation enregistrée, **quand** la copie de préparation est modifiée
  avant le redémarrage, **alors** son empreinte ne correspond plus, rien n'est posé et le plugin est en
  « demande en échec ».
- **Étant donné** un arrêt du service pendant la pose d'un plugin, **quand** Voyager redémarre, **alors**
  l'opération est terminée ou annulée et jamais laissée à moitié.
- **Étant donné** plusieurs demandes en attente (installer A, désactiver B, désinstaller C), **quand** je
  redémarre, **alors** toutes sont appliquées avant le chargement du moindre plugin, et l'échec d'une
  n'empêche pas les autres.
- **Étant donné** des demandes successives sur un même plugin (installer la version 2 puis la version 3),
  **quand** je redémarre, **alors** seule la dernière est appliquée et la copie de préparation de la
  première a été supprimée.
- **Étant donné** des demandes en attente, **quand** je ferme puis rouvre l'application, **alors** elles
  sont appliquées au démarrage.

### Compatibilité

- **Étant donné** un plugin installé qui fournit le contrat `X` en version `1`, **quand** l'application ne prend
  plus en charge cette version, **alors** il est « écarté : contrat non pris en charge » (`X`, version `1`), la
  vue l'explique, il reste installé, et je peux le désinstaller.
- **Étant donné** un plugin qui fournit deux contrats dont un seul n'est plus pris en charge, **quand** je
  démarre, **alors** le plugin entier est écarté, la raison nomme le contrat concerné, et aucun de ses
  contrats n'est plus fourni.
- **Étant donné** un plugin installé qui utilise un contrat déprécié, **quand** j'ouvre l'écran « Plugins »,
  **alors** le plugin fonctionne et la vue m'avertit que son contrat sera retiré.
- **Étant donné** un paquet qui fournit un contrat dans une version inconnue de l'application, **quand** je
  l'examine, **alors** il est refusé avec « contrat non pris en charge ».

### Dépendances entre plugins (étape 2)

- **Étant donné** un plugin `B` qui requiert un contrat fourni par `A`, **quand** je démarre, **alors** `A`
  est chargé avant `B`, et l'écran « Plugins » montre `B` parmi les plugins qui dépendent de `A`.
- **Étant donné** `A` désactivé, **quand** je redémarre, **alors** `B` est « écarté : prérequis non fourni »
  (le contrat et la version sont nommés), et un plugin `C` qui dépend de `B` l'est aussi ; **quand**
  j'active `A` et que je redémarre, **alors** `B` et `C` reviennent seuls.
- **Étant donné** `B` qui requiert un contrat fourni par `A1` et par `A2`, **quand** je désactive `A1`,
  **alors** `B` reste chargé et la boîte ne le liste pas.
- **Étant donné** deux plugins qui se requièrent l'un l'autre, ou trois en cycle, **quand** je démarre,
  **alors** tous sont « écartés : dépendance circulaire », et un plugin qui dépend d'un des trois est
  « écarté : prérequis non fourni ».
- **Étant donné** `A` dont `B` dépend, **quand** je le désactive, **alors** une boîte de confirmation liste
  `B`, « Annuler » est le bouton par défaut, un refus ne change rien, et une confirmation met `A` « en attente
  de redémarrage » ; **quand** je le désinstalle ou le remplace par une version qui ne fournit plus le
  contrat, **alors** la boîte liste `B` de même.
- **Étant donné** l'installation de `B` (qui requiert `A`) en attente, **quand** je demande de désinstaller
  `A`, **alors** la boîte liste `B` ; et **étant donné** la désinstallation de `A` en attente, **quand** j'installe
  `B`, **alors** la boîte prévient que `B` restera écarté.
- **Étant donné** un paquet dont le prérequis n'est fourni par personne, **quand** je l'installe, **alors** la
  boîte prévient qu'il restera écarté ; il est installé, puis « écarté : prérequis non fourni » après le
  redémarrage.
- **Étant donné** deux plugins qui fournissent un contrat qui admet plusieurs fournisseurs, **quand** un
  consommateur démarre, **alors** il reçoit les deux, et si l'un échoue l'autre fonctionne ; **étant donné** un
  contrat à fournisseur unique, **alors** l'écran « Plugins » me laisse choisir le fournisseur actif (une
  demande confirmée par la boîte du système, appliquée au redémarrage) et écarte l'autre.
- **Étant donné** le rétablissement, `B` qui dépend de `A` et `C` qui dépend de `B`, **quand** je réactive `C`,
  **alors** la boîte liste `B` et `A` et les trois se réactivent ensemble ; **étant donné** `A` suspendu,
  **alors** la confirmation est renforcée et `B` apparaît « écarté », pas « suspendu ».
- **Étant donné** un contrat requis que fournissent deux plugins installés, **quand** je réactive son
  consommateur pendant le rétablissement, **alors** la boîte me fait **choisir** le fournisseur à réactiver.
- **Étant donné** un contrat à fournisseur unique dont je n'ai pas choisi le fournisseur, ou dont le fournisseur
  actif est désinstallé, **quand** je redémarre, **alors** aucun fournisseur n'est actif et ses consommateurs
  sont « écartés : prérequis non fourni ».
- **Étant donné** un plugin livré qui requiert un contrat fourni seulement par un plugin installé, **quand**
  la CI s'exécute, **alors** le test d'architecture échoue.

### Mise à jour de l'application (étape 2)

- **Étant donné** une mise à jour qui rend obsolète un contrat fourni par un plugin installé, **quand**
  l'application trouve cette mise à jour et avant de l'appliquer, **alors** elle m'informe avec la liste des
  plugins touchés (dépendants compris) et la raison de chacun ; si j'**accepte**, la mise à jour s'applique ; si
  je **refuse**, l'application se lance dans la version actuelle et rien n'a changé ; au démarrage suivant, la
  question est **reposée**.
- **Étant donné** une mise à jour qui ne touche aucun plugin installé, **quand** l'application la trouve,
  **alors** elle s'applique sans question.
- **Étant donné** le message d'information, **quand** je le lis, **alors** il précise que le contrôle ne
  garantit pas le bon fonctionnement des plugins.

### Plantages

- **Étant donné** un plugin installé qui plante le service au chargement, **quand** Voyager redémarre,
  **alors** ce plugin est suspendu, l'application démarre et la vue me propose de le désactiver ou de le
  réactiver ; il reste suspendu aux démarrages suivants tant que je n'ai pas décidé.
- **Étant donné** une trace qui désigne deux plugins, ou une application qui n'arrive plus à démarrer,
  **quand** l'écran d'échec s'affiche, **alors** il propose « Démarrer sans les plugins installés » ; les
  plugins installés sont « désactivés pour le rétablissement », seul le livré est chargé, et je réactive les
  autres un à un.
- **Étant donné** un plugin suspendu, **quand** je le réactive, **alors** la confirmation est renforcée :
  elle rappelle le soupçon, la case « Je comprends » est obligatoire et « Annuler » est le bouton par défaut.
- **Étant donné** que je ferme l'application ou qu'une mise à jour la relance pendant qu'un plugin répond,
  **quand** je la rouvre, **alors** aucun plugin n'est suspendu.

### Sécurité

- **Étant donné** le module d'un plugin, **quand** il tente d'installer, de remplacer, de désinstaller ou
  d'activer un plugin sans confirmation de l'utilisateur, **alors** aucune demande n'est créée.
- **Étant donné** que j'ajoute, examine et installe des paquets, **quand** je lis les journaux, **alors** ils
  contiennent l'identifiant, la version et l'empreinte, et jamais un chemin ni un nom de fichier.
- **Étant donné** la page, **quand** elle demande d'installer un paquet de la boîte, **alors** elle le
  désigne par son nom et jamais par un chemin du disque.

### Fabriquer un paquet

- **Étant donné** un plugin construit, **quand** je lance la commande, **alors** j'obtiens
  `<identifiant>-<version>.voyagerplugin` et son empreinte, et l'installer donne un plugin qui fonctionne
  comme l'original.
- **Étant donné** un plugin qui n'est pas construit, **quand** je lance la commande, **alors** elle
  s'arrête avec un message clair et ne crée aucun fichier.
- **Étant donné** un dossier de plugin qui contient un fichier hors de la liste autorisée, **quand** je
  lance la commande, **alors** ce fichier n'est pas mis dans le paquet.

## Cas limites

- **Hors-ligne** : toute la gestion est locale ; aucun réseau n'est nécessaire.
- **Données personnelles** : un fichier reçu peut porter un nom personnel ; son nom est montré (nettoyé) à
  l'utilisateur, mais n'est jamais journalisé ; aucun chemin n'est journalisé. Le manifeste vient d'un
  inconnu : tout ce qui en est montré est nettoyé.
- **Droits et disque** : un dossier non accessible en écriture, un disque plein, un antivirus qui verrouille
  un fichier donnent un plugin en « demande en échec » avec sa raison, sans rien poser à moitié.
- **Boîte de dépôt** : vide, ou pleine de fichiers qui ne sont pas des paquets (ignorés). Un paquet illisible
  n'apparaît « refusé » qu'à l'examen, quand on choisit de l'installer. Si le paquet a disparu de la boîte
  avant ce choix, la vue le signale et rien n'est installé ; après ce choix la copie de préparation sert.
- **Un plugin sans partie utile** (ni .NET ni interface) est refusé à l'examen.
- **Développement** : en développement, les dossiers des plugins de l'utilisateur sont sous `artifacts/`,
  jamais ceux de l'application installée, que le développeur partagerait sinon avec elle.
- **Un plugin livré dans une version ultérieure** : si une nouvelle version de l'application livre un
  plugin que l'utilisateur avait installé, la version installée est écartée (« déjà livré ») ; elle reste
  sur le disque jusqu'à ce qu'on la désinstalle ou que l'on désactive le livré.
- **Redémarrage pendant l'enregistrement d'une demande** : une demande est enregistrée en entier ou pas du
  tout.
- **Versions** : l'écran montre les versions telles que calculées (chaque plugin a la sienne,
  [ADR-0007](../adr/0007-git-flow-et-versions-gitversion.md)) ; aucune n'est écrite à la main.

- **Plugin qui fournit plusieurs contrats** (étape 2) : les listes de dépendants (désactivation,
  désinstallation, remplacement) portent sur **chacun** de ses contrats, et ne retiennent que les plugins qui
  perdraient leur seul fournisseur.
- **Contrat à fournisseur unique** (étape 2) : s'il n'y en a aucun de choisi, ou si l'actif disparaît, aucun
  fournisseur n'est actif et les consommateurs sont écartés (« prérequis non fourni ») ; la proposition est
  à confirmer (questions ouvertes).
- **Plugin livré qui dépend d'un installé** (étape 2) : à régler (questions ouvertes) ; en attendant, un
  plugin livré ne requiert que des contrats de l'application ou d'autres livrés.

## Hors périmètre

- **Les adaptateurs de contrat eux-mêmes** et ce que le manifeste devra dire de plus pour les **dépendances
  entre plugins** : ils se construisent au premier plugin consommateur d'un autre ou à la première évolution
  réelle d'un contrat (ADR-0017, décision 8). Les sections de l'étape 2 décrivent ce que l'utilisateur en voit,
  pas leur construction. Les **dépendances facultatives** n'existent pas.
- **Les plugins d'un groupe** (le propriétaire installe sur le serveur, les applications des membres les
  téléchargent) : décision 8 de l'ADR-0016, après l'authentification et le serveur du groupe (questions Q33
  à Q35).
- **Le catalogue en ligne**, la **clé de signature de production** (qui la garde, comment on la change, où
  l'héberger) et l'affichage « signature de développement » : question Q31, à l'entrée en production réelle.
- **Les plugins de tiers d'un catalogue ouvert**, l'**isolation** des plugins et leurs permissions.
- **Le rechargement à chaud** : un changement demande toujours un redémarrage.
- **La mise à jour automatique** des plugins installés (sans catalogue, elle n'a pas de source).
- **Les données propres aux plugins** à la désinstallation (question Q34), les **dépendances natives**
  d'un paquet (question Q35) et l'**avertissement avant d'enregistrer des données qui dépendent d'un
  plugin absent** (question Q30, avec le métier).
- **L'apparence** de l'écran « Plugins » (disposition, icônes, place dans les menus) : elle vient avec le
  design ; la spec décide des comportements.
- **Désigner avec certitude** le plugin qui plante pendant des appels concurrents : la spec fixe l'aide au
  rétablissement, pas une attribution certaine (question Q26, mécanisme proposé par l'ADR-0016).

## Questions ouvertes

Aucune ne bloque le début de l'implémentation de l'étape 1.

- **Le contrôle avant une mise à jour** (question Q37) : le principe et le refus sont décidés (si
  l'utilisateur refuse, pas de mise à jour, et la question revient au démarrage suivant). Reste à fixer, quand
  le premier contrat en version 2 sera créé : qui compare (le processus principal avec une liste de contrats
  publiée avec la release, ou la nouvelle version démarrée en mode vérification), avant ou après le
  téléchargement (le service local n'est pas démarré à ce moment, ADR-0014), et où la nouvelle version publie
  les contrats qu'elle prend en charge. Cela demande d'**amender l'ADR-0014**, encore proposée donc modifiable
  sans nouvel ADR.
- **Les adaptateurs de la moitié d'interface** : à fixer avec le premier plugin qui en dépend (ADR-0017). Pour
  l'étape 1, la forme du manifeste est fixée : `provides` et `requires` listent des objets
  `{ "contract": "<identifiant>", "version": <n> }`, et `requires` contient toujours le contrat de base
  `plugin`, qui remplace l'ancien champ `sdk`.
- **Les formats de données** que les plugins mettront dans `data/` (une base SQLite, des fichiers CSV ou
  JSON…) et leur lien avec les données stockées par le cœur (ADR-0004, question Q34) : hors de cette spec,
  à décider avec le premier plugin de données.
