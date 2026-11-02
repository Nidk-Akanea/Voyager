# Spec — Gestion des plugins

- **Statut** : Brouillon
- **Épic** : E9 — Plateforme modulaire (transverse)
- **Date** : 2026-10-30

## Objectif

Aujourd'hui les plugins sont livrés avec l'application et l'utilisateur ne peut rien en faire. Cette
fonctionnalité lui donne la main : **voir** ses plugins, en **installer** depuis un fichier, les
**remplacer**, les **désinstaller**, les **activer** et les **désactiver**, et **retrouver** celui qui fait
planter Voyager sans perdre l'accès à son voyage. Elle applique à l'application de bureau le mécanisme
décidé par l'[ADR-0016](../adr/0016-installation-et-gestion-des-plugins.md) et **complète** la
[spec de la preuve des plugins](plateforme-preuve-des-plugins.md), dont la règle « il n'y a pas de dossier
de plugins de l'utilisateur » n'est plus vraie. Les plugins d'un groupe, le catalogue en ligne et la clé de
signature de production n'en font pas partie (voir « Hors périmètre »).

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
  à « désactivé pour le rétablissement » et se réactivent un à un).

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
5. Le détail d'un plugin dit aussi **ce qu'il apporte** (une partie d'interface, du code .NET, ou les deux),
   et, pour un plugin installé, sa **date d'installation** et le **nom et l'empreinte du paquet d'origine**.
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
5. L'examen **refuse** le paquet, avec sa raison, si : l'archive est illisible ; le manifeste est invalide
   ou ne déclare aucune partie (ni .NET ni interface) ; l'identifiant est mal formé ou réservé (les noms de
   périphériques de Windows) ; il est écrit pour une version du SDK que l'application n'accepte pas ; il
   contient un fichier ou un nom de fichier non autorisé ; l'espace disque disponible ne suffit pas à
   l'extraire ; sa signature ne correspond pas à son contenu pour une clé que l'application connaît. Une signature dont la clé est
   inconnue ne fait pas refuser le paquet : il reste « Non vérifié ».
6. Un paquet accepté est présenté dans une **boîte de dialogue de confirmation**, propre au système et non à
   la page : le **nom du fichier**, son **empreinte** (SHA-256), l'identifiant, la version, la version du
   SDK pour laquelle il est écrit, ce qu'il apporte, sa vérification, l'éventuel doublon avec un plugin
   livré, et, **avant** la case à cocher et à **chaque** installation, l'**avertissement** : *un plugin .NET
   s'exécute avec vos droits sur cet ordinateur ; un plugin d'interface peut parler au service de Voyager
   et lire vos voyages ; l'auteur de ce plugin n'est pas vérifié*.
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
2. **Désactiver** (plugin livré ou installé) n'a pas besoin de confirmation : cela réduit ce qui s'exécute.
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

1. Un plugin installé pour une version du SDK que l'application ne reconnaît plus (par exemple après une
   mise à jour de l'application, tant que le SDK est en `0.x`) est **écarté** avec « version du SDK
   incompatible ». Il **reste installé** : on peut le désinstaller ou le remplacer par une version écrite
   pour le nouveau SDK. La vue **l'explique**.

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
   jusqu'à retrouver celui qui fait planter.
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

### Garanties de sécurité

1. **Étendue de la garantie.** Les garanties ci-dessous valent **contre la page et le module d'interface**
   d'un plugin. Elles **ne protègent pas** d'un plugin .NET déjà installé, qui a les droits de
   l'utilisateur sur son poste. Un module peut encore **demander** l'ouverture d'une confirmation, et
   désactiver un plugin ou annuler une demande ne passe pas par elle.
2. **Aucun geste qui ajoute du code ou le rend actif** (installer, remplacer, désinstaller, activer) n'a
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
- **Étant donné** un plugin écarté (par exemple un SDK incompatible), **quand** j'ouvre l'écran, **alors**
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
  l'identifiant, la version, le SDK, ce que le plugin apporte et « Non vérifié » ; le bouton par défaut est
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
- **Étant donné** un paquet écrit pour un SDK non accepté, **quand** je l'examine, **alors** il est refusé
  avec « version du SDK incompatible ».
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

- **Étant donné** un plugin installé écrit pour le SDK `0.1`, **quand** l'application passe à un SDK
  `0.2`, **alors** il est « écarté : version du SDK incompatible », la vue l'explique, il reste installé, et
  je peux le désinstaller.

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

## Hors périmètre

- **Les dépendances entre plugins** : la gestion décrite ici suppose qu'un plugin ne dépend d'aucun autre ;
  elles sont décidées par contrat (ADR-0017, proposée) et entreront dans cette spec avec elle.
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

Aucune ne bloque le début de l'implémentation, **sauf peut-être la première**, que l'utilisateur a soulevée.

- **Les dépendances entre plugins et la compatibilité par contrat** (ADR-0017, proposée, et questions Q27,
  Q36 et Q37). L'utilisateur a décidé que les dépendances se font **par contrat** et que la compatibilité
  se juge **contrat par contrat**, avec des adaptateurs. Quand l'ADR sera acceptée, cette spec change :
  « version du SDK incompatible » devient « contrat non pris en charge » ; les dépendances ne sont plus hors
  périmètre (prévenir en **désactivant** ou **désinstallant** un plugin dont d'autres ont besoin, prévenir à
  l'**installation** d'un plugin dont le prérequis manque, écarter un plugin dont le prérequis est absent ou
  désactivé, refuser les cycles, et dire ce que devient la réactivation « un à un » du rétablissement quand
  réactiver A impose B) ; et l'**avertissement avant une mise à jour** de l'application, pour les plugins
  dont un contrat serait retiré, devient une règle.
- **Les formats de données** que les plugins mettront dans `data/` (une base SQLite, des fichiers CSV ou
  JSON…) et leur lien avec les données stockées par le cœur (ADR-0004, question Q34) : hors de cette spec,
  à décider avec le premier plugin de données.
