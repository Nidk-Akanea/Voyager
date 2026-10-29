# ADR-0016 — Installation et gestion des plugins

- **Statut** : Proposée
- **Date** : 2026-10-29
- **Remplace** : — (complète [ADR-0015](0015-mecanisme-de-plugins.md) et en **lève deux limites** : la
  phrase « il n'y a pas de dossier de plugins de l'utilisateur » de la décision 9, et, dans la décision 10,
  le « pas de signature » ; **l'absence d'isolation reste**, et l'import d'un fichier accepte du code de
  tiers aux risques de l'utilisateur (décision 7) ; précise la décision 7 de
  [ADR-0009](0009-installation-et-mises-a-jour-velopack.md))

## Contexte

La preuve des plugins ([ADR-0015](0015-mecanisme-de-plugins.md)) livre les plugins **avec l'application** :
ils sont dans `resources/plugins`, le service local lit ce seul dossier, et un plugin se met à jour avec
l'application. L'utilisateur veut aller plus loin :

- **placer un paquet dans un répertoire, puis, depuis une vue, choisir ce qu'il installe, désinstalle,
  active ou désactive** ;
- **signer les plugins du projet, et pouvoir en importer d'autres, avec un avertissement** ;
- que **celui qui administre un serveur installe un plugin une fois et que tous les membres connectés
  l'aient** (c'est déjà ce que dit le [schéma fonctionnel](../schemas/fonctionnel.md) : le propriétaire
  de l'instance installe les plugins) ;
- à terme, **mettre des plugins en ligne** pour les distribuer.

Ce qui existe et ce qui gêne :

- **Un seul dossier de plugins.** `PluginCatalog.Load`, l'option `PluginsDirectory` du service local et le
  protocole `voyager://app` (qui sert les modules d'interface) n'en connaissent qu'un. Le processus
  principal le pose ; dans l'application installée il n'est jamais lu de l'environnement, en
  développement un réglage (`VOYAGER_PLUGINS_DIRECTORY`) peut le déplacer.
- **Le dossier de l'application est remplacé à chaque mise à jour** (Velopack) : un plugin installé dedans
  disparaîtrait. Les données de l'utilisateur vivent dans `%LOCALAPPDATA%\Voyager`
  ([ADR-0013](0013-assemblage-du-paquet-de-l-application-de-bureau.md)) et survivent aux mises à jour.
- **Windows verrouille les assemblies d'un plugin chargé** : on ne peut ni le supprimer ni le remplacer
  pendant que le service tourne. Un plugin ajouté n'apparaît qu'au lancement suivant : l'utilisateur a
  déjà accepté que redémarrer suffit.
- **Le hors-ligne est un principe du produit** : consulter le programme et saisir des dépenses sans
  réseau. Ce qu'un plugin calcule (types de prestation, bases de calcul, règles de répartition) doit donc
  s'exécuter **sur le poste** ; ce qui a besoin du réseau (un connecteur qui interroge une API avec les clés
  payantes du propriétaire) peut rester **sur le serveur**.
- **Aucune isolation** ([ADR-0015](0015-mecanisme-de-plugins.md), décision 10) : un plugin .NET s'exécute
  avec les droits de l'utilisateur sur son poste, et le module d'interface d'un plugin avec ceux de la
  page (jeton du service local, canaux IPC). Acceptable pour des plugins de l'équipe ; l'ADR-0015 prévoit
  de revoir cette limite **avant** d'accepter du code venu d'ailleurs (question Q11). **Le présent ADR est
  cette revue** pour le code installé depuis un fichier ou reçu d'un groupe.
- **Charger du code d'un dossier que l'utilisateur ou un autre programme peut choisir ou modifier** est le
  risque que l'[ADR-0014](0014-ecran-de-demarrage-et-mise-a-jour-au-demarrage.md) (proposée) écarte pour
  la source des mises à jour, et que l'ADR-0015 (décision 9, acceptée) écarte pour les plugins.
- **Un plugin qui plante le service** : le superviseur le relance trois fois en soixante secondes, puis
  affiche un écran d'échec avec « Réessayer », qui recommence. Désactiver le plugin fautif est le principe
  noté en question Q26 ; le mécanisme n'est pas décidé.
- **Les utilisateurs sont un petit groupe d'amis**, rarement des développeurs : « installe ce plugin »
  peut venir d'un message d'un ami. Un fichier reçu est du code venu d'ailleurs, même sans catalogue.
- Pas de dessin d'écran encore : le design vient plus tard, juste avant le métier. Cet ADR décide les
  **comportements**, pas l'apparence.

## Options

### Où vivent les plugins installés

**Option A — Le dossier de données de l'utilisateur** (`%LOCALAPPDATA%\Voyager\plugins\`), à côté des
plugins livrés.

- Avantages : survit aux mises à jour ; sans droits d'administrateur ; sépare « livré » (non
  désinstallable) et « installé » ; un seul endroit où chercher.
- Inconvénients : le service local et le protocole doivent connaître **plusieurs** racines ; un autre
  programme de l'utilisateur peut y écrire (il peut déjà écrire dans le dossier de l'application, installé
  lui aussi dans `%LOCALAPPDATA%` : le risque n'est pas nouveau, mais il n'est pas moindre).

**Option B — Le dossier de l'application** (`resources/plugins`).

- Avantages : aucun changement de code.
- Inconvénients : effacé à chaque mise à jour ; mélange livré et installé ; l'application ne devrait pas
  modifier son propre dossier.

**Option C — Un dossier choisi par l'utilisateur** (réglage), dont le service charge le code.

- Avantages : souple (clé USB, dossier partagé du groupe).
- Inconvénients : c'est le dossier configurable que l'ADR-0014 écarte ; un volume réseau ou amovible peut
  disparaître pendant le démarrage ; ce qui s'y trouve est chargé sans contrôle. À distinguer de
  « Ajouter un paquet… » (décision 3) : là, le fichier choisi est **copié** puis vérifié, et le service ne
  charge jamais de code depuis l'endroit où l'utilisateur l'a trouvé.

### Le paquet d'un plugin

**Option A — Une archive zip `.voyagerplugin`** : le contenu du dossier du plugin, `plugin.json` à la
racine de l'archive, et une signature facultative.

- Avantages : un seul fichier, donc téléchargeable, copiable, vérifiable (empreinte SHA-256) et
  signable ; examiné **avant** d'être posé ; sans dépendance (zip et la signature ECDSA sont dans .NET).
- Inconvénients : un format de plus à documenter ; il faut écrire une extraction sûre.

**Option B — Un dossier copié à la main**, sans archive.

- Avantages : aucun code d'extraction ; c'est ce qu'on fait déjà en développement.
- Inconvénients : copie partielle possible, pas d'empreinte ni de signature, pas de vérification avant la
  pose, et rien qui se télécharge proprement.

**Option C — Un paquet NuGet `.nupkg`** (c'est aussi un zip).

- Avantages : format connu des développeurs .NET, outils de publication existants.
- Inconvénients : ses métadonnées (`.nuspec`) décrivent un paquet .NET et pas notre manifeste ni la
  partie d'interface ; la signature NuGet suppose nuget.org ; il faudrait quand même vérifier et extraire
  nous-mêmes.

### Quand un changement prend effet

**Option A — Au prochain démarrage du service local** : les demandes (installer, désinstaller, activer,
désactiver) sont enregistrées et le service les applique **avant de charger un seul plugin**. L'application
relance le service et recharge la page ; elle ne se ferme pas.

- Avantages : aucun fichier verrouillé (rien n'est encore chargé) ; cohérent avec le choix déjà fait
  (« redémarrer suffit ») ; aucun déchargement à écrire ; l'état est celui d'un démarrage normal.
- Inconvénients : les écrans ouverts sont remplacés pendant la relance (question Q23) ; un redémarrage à
  chaque série de changements.

**Option B — À chaud** (décharger et recharger un plugin).

- Avantages : immédiat.
- Inconvénients : l'hôte garde des références (conteneur d'injection, services gRPC, registres) :
  l'ADR-0015 l'a écarté ; beaucoup de cas limites pour un gain mince.

### Jusqu'où va la confiance

**Option A — Les plugins du projet sont signés ; on peut en importer d'autres depuis un fichier, avec un
avertissement.** La signature est dans le format dès le départ ; l'application la vérifie avec des clés
publiques qu'elle contient.

- Avantages : « Vérifié » (signé par le projet) et « Non vérifié » (importé) se distinguent dans la vue ;
  l'utilisateur garde la main sur ce qu'il importe ; aucun réseau nécessaire.
- Inconvénients : **importer un fichier, c'est accepter du code de tiers sans isolation** : un ami peut
  envoyer un fichier, et un utilisateur qui clique n'a que l'avertissement et la confirmation comme
  protection ; du code non isolé et non vérifié s'exécute avec ses droits. Le choix est celui de
  l'utilisateur ; ce qui reste refusé, c'est la **distribution** par un catalogue ouvert (option C).

**Option A′ — Comme A, mais l'import est derrière un réglage explicite** (« Autoriser l'installation de
plugins depuis un fichier », désactivé par défaut).

- Avantages : un utilisateur qui ne l'a pas activé ne peut pas être amené à importer un fichier reçu par
  un clic distrait.
- Inconvénients : un écran de réglage de plus ; ne protège pas qui l'a activé. **Non retenue : l'utilisateur
  préfère l'import avec avertissement.**

**Option B — Un catalogue en ligne du projet** : un index (versions, empreintes, compatibilité SDK) et des
paquets signés par le projet.

- Avantages : « installer depuis le catalogue » et des mises à jour de plugins indépendantes de celles de
  l'application.
- Inconvénients : une clé de signature à garder et à pouvoir révoquer ; un hébergement et un index à
  maintenir ; l'application contacte un serveur de plus (son adresse IP est visible, comme pour les mises
  à jour).

**Option C — Un catalogue ouvert aux plugins de tiers.**

- Avantages : l'écosystème que vise l'architecture modulaire.
- Inconvénients : du code inconnu s'exécute avec les droits de l'application sur des données de voyage
  sensibles : il faut d'abord une **isolation** (un processus par plugin, option B de l'ADR-0015), des
  permissions, une relecture et un moyen de retirer un plugin malveillant, et y réfléchir au regard du
  RGPD. Démesuré tant qu'il n'y a pas de vrais plugins.

### Les plugins d'un groupe

**Option A — Pas de plugin « du groupe » : chaque membre installe les siens.**

- Avantages : le plus simple ; aucune confiance déléguée au propriétaire.
- Inconvénients : deux membres n'ont pas les mêmes plugins, donc les mêmes données ne s'affichent ou ne se
  calculent pas pareil (question Q30) ; le propriétaire ne peut pas « installer pour le groupe », ce que
  dit pourtant le schéma fonctionnel.

**Option B — Quelqu'un installe le plugin sur le serveur, pour un groupe ; l'application de chaque membre
télécharge la partie locale du plugin et la garde sur son poste.**

- Avantages : tout le groupe a les mêmes plugins ; le hors-ligne est conservé (le plugin tourne sur le
  poste une fois téléchargé) ; ce qui a besoin du réseau (connecteurs, clés d'API) reste sur le serveur ;
  c'est le même paquet et la même vérification que pour une installation locale.
- Inconvénients : du code arrive sur le poste de chaque membre **depuis le serveur** : si le serveur est
  piraté ou son administrateur négligent, il arrive chez tous ; il faut un stockage de paquets sur le
  serveur, une synchronisation, un accord des membres, et la compatibilité de chaque poste avec le SDK du
  plugin ; un membre de plusieurs groupes reçoit plusieurs listes ; **sur un serveur qui accueille
  plusieurs groupes (ADR-0010), un plugin .NET non isolé qui s'exécute sur le serveur partagerait le
  processus des données de tous les groupes**.

**Option C — Les plugins ne tournent que sur le serveur, rien n'est téléchargé.**

- Avantages : aucun code poussé sur les postes.
- Inconvénients : un plugin ne fonctionne plus sans réseau, ce qui contredit le principe hors-ligne dès
  qu'il calcule quelque chose ; suffisant seulement pour un plugin qui n'a besoin que du réseau.

### Que faire d'un plugin qui plante

**Option A — Un marqueur** qui note les plugins en cours de chargement ou d'appel.

- Avantages : désigne un ou des suspects sans intervention de l'utilisateur.
- Inconvénients : les appels sont concurrents (plusieurs plugins en cours à la fois) ; un arrêt normal, une
  coupure de courant ou un plantage causé par l'hôte laissent la même trace (faux positifs) ; une tâche
  qui dépasse son délai continue peut-être de tourner et planter plus tard ; coût d'une écriture par appel.
  Ces défauts se limitent si le marqueur ne désigne qu'**un** suspect et que l'utilisateur voit ce qui
  change.

**Option B — Un mode « sans les plugins installés »** proposé quand le superviseur passe à l'état
« échec » : le service démarre sans les charger, et on les réactive un à un jusqu'à retrouver le fautif.

- Avantages : aucun faux positif ; fonctionne quand le marqueur ne désigne personne ou plusieurs plugins ;
  l'état « échec » existe déjà.
- Inconvénients : plusieurs redémarrages pour une chaîne de plugins ; l'utilisateur est mis à contribution.

**Option C — Désactiver seulement à la demande de l'utilisateur**, sans aide au rétablissement.

- Avantages : rien à construire.
- Inconvénients : un plugin qui empêche de démarrer enferme l'utilisateur dehors.

## Décision

**A, A, A** pour les trois premières questions (dossier de données, archive `.voyagerplugin`, effet au
prochain démarrage du service local). Pour la confiance : **A** (plugins du projet signés, import avec
avertissement), puis **B** (catalogue du projet) **à l'entrée en production réelle**, **C** seulement par
un nouvel ADR. Pour les groupes : **B**, plus tard, quand le serveur du groupe et l'authentification
existeront. Pour les plantages : **A comme indice et B comme filet**.

1. **Une racine par origine, un seul hôte.** Les plugins **livrés** (dossier de l'application) peuvent
   être désactivés mais pas désinstallés par l'application, qui n'écrit pas dans son propre dossier. Les
   plugins **installés** sont dans le dossier de données de l'utilisateur
   (`%LOCALAPPDATA%\Voyager\plugins\installed\<id>\`). Les plugins **d'un groupe** ont une racine par
   groupe (décision 8). Les racines sont posées par le processus principal et ne sont jamais lues de
   l'environnement ni d'un réglage dans l'application installée ; en développement, elles vivent sous
   `artifacts/` (jamais dans le dossier de données de l'application installée, que le développeur
   partagerait sinon avec elle). `Voyager.PluginHost` reçoit la liste des racines. Un identifiant ne peut
   être actif qu'une fois : les livrés d'abord, puis les installés, puis ceux des groupes ; un doublon est
   **écarté** avec une raison propre, sans masquer le premier, et **la vue l'explique et laisse
   l'utilisateur choisir** lequel garder (ce qu'il a installé lui-même ne disparaît pas sans qu'il le
   sache ; le conflit d'un membre de plusieurs groupes se traite de même, question Q33). **Le protocole
   `voyager://app` choisit la racine d'après la liste que le service local fait des plugins chargés** (le
   processus principal la lit une fois le service prêt), jamais d'après le seul identifiant de l'adresse :
   l'interface d'un plugin écarté n'est pas servie. Les identifiants refusent aussi les noms réservés de
   Windows, puisqu'ils nomment des dossiers.
2. **Le paquet** `.voyagerplugin` est une archive zip : `plugin.json` et le reste du plugin à la **racine de
   l'archive**, plus une **signature** facultative. La signature couvre **les noms et les contenus de
   toutes les entrées**, manifeste compris (une liste d'empreintes signée), pour qu'on ne puisse ni
   ajouter, ni retirer, ni permuter un fichier ; elle est vérifiée avec les clés publiques que
   l'application contient. L'extraction est **sûre** et se fait dans un dossier temporaire : liste blanche
   de contenu (le manifeste, les assemblies et leurs fichiers associés, `ui/`, `i18n/`), noms de fichiers
   refusés (sortie du dossier, chemins absolus ou avec lecteur, noms et flux propres à Windows, doublons
   de casse, liens), limites sur la taille **réellement décompressée**, le nombre de fichiers, le rapport
   de compression et la profondeur. La liste exacte et les valeurs reviennent à la spec, avec un plugin
   corrompu pour les tester. Le manifeste, l'identifiant, la compatibilité avec le SDK et la signature
   sont vérifiés **avant** de poser le plugin. Poser ou remplacer un plugin est **journalisé et
   reprenable** : l'ancien dossier est renommé, le nouveau posé, l'ancien supprimé, et un démarrage
   interrompu au milieu termine ou annule l'opération ; un échec laisse le plugin « en erreur », visible.
   Installer un identifiant déjà installé **remplace** le plugin : la confirmation dit « remplace la
   version X par la version Y » et traite une version plus ancienne comme une opération à part, avertie ;
   sans signature, aucune continuité d'auteur n'est vérifiée. **Désinstaller ou désactiver un plugin ne
   supprime jamais de données de voyage** (question Q34).
3. **Une boîte de dépôt** (un dossier de `%LOCALAPPDATA%\Voyager\plugins\`, noms indicatifs) où l'utilisateur dépose des paquets à la
   main (un bouton ouvre le dossier) ou par « Ajouter un paquet… » (boîte de dialogue native du processus
   principal, qui vérifie l'extension et la taille puis copie le fichier). **La page ne donne jamais un
   chemin** : elle désigne un paquet de la boîte par son nom, que le processus principal valide comme un
   nom de fichier. Pour installer, le processus principal **copie le paquet dans un dossier de préparation**
   (`plugins/staging/`) et en calcule l'empreinte SHA-256 : c'est cette copie, et non le fichier de la
   boîte, qui sera examinée et appliquée.
4. **Le processus principal est le seul à écrire les demandes ; le service local est le seul à écrire
   l'état.** Les demandes (installer tel paquet préparé, avec son empreinte ; désinstaller ; activer ;
   désactiver) vont dans `plugins/requests.json`, écrit par le processus principal **après la confirmation
   native** (décision 5). Le service local lit les demandes et les applique **au démarrage, avant tout
   chargement**, en revérifiant l'empreinte du paquet préparé, puis charge les plugins activés. Il écrit
   le résultat dans `plugins/state.json` : par plugin, origine, activé, date et paquet d'origine
   (**source et date** gardées), état d'application, vérification (signature), et les suspicions de
   plantage. L'interface ne fait que **lire** (lister les plugins, examiner un paquet préparé) par
   `voyager.pluginhost.v1` : aucune opération de gestion n'est un appel gRPC que la page, donc un module de
   plugin, pourrait faire seul.
5. **La vue** (un écran « Plugins », dessiné avec le design ; la page « À propos » reste une liste en
   lecture seule) montre pour chaque plugin son identifiant, sa version, son origine (« Livré avec
   Voyager », « Installé par vous », « Du groupe »), sa **vérification** (« Vérifié » si la signature est
   celle du projet, « Non vérifié » sinon), son état (chargé, écarté avec la raison, désactivé, suspendu
   après un plantage, en attente de redémarrage) et ses actions. La **confirmation** d'installer, de
   remplacer, de désinstaller et d'activer (y compris de réactiver un plugin suspendu) passe par une
   **boîte de dialogue native du processus principal**, que le module d'un plugin ne peut pas confirmer à
   la place de l'utilisateur. Désactiver n'en demande pas (cela réduit ce qui s'exécute). La boîte montre
   le **nom du fichier, son empreinte et le manifeste nettoyé** (longueur limitée, sans retours à la ligne
   ni caractères de contrôle ou d'écriture bidirectionnelle, car il vient de l'auteur du plugin), ce que
   le plugin apporte (interface seule, code .NET, ou les deux) et, pour un plugin « Non vérifié »,
   l'**avertissement** sans détour : **un plugin .NET s'exécute avec vos droits sur cet ordinateur ; un
   plugin d'interface peut parler au service de Voyager et lire vos voyages ; son auteur n'est pas
   vérifié**. Le bouton par défaut est « Annuler » ; pour un plugin « Non vérifié », la confirmation demande en
   plus de cocher « Je comprends ». Un module d'interface installé peut encore demander ces gestes ;
   sans isolation, c'est la confirmation humaine qui s'interpose, et rien d'autre.
6. **Plantages (Q26).** Le service local note dans un marqueur les plugins **en cours de chargement**
   (chargés l'un après l'autre : un plantage à ce moment désigne son plugin à coup sûr) et **en cours
   d'appel**. Si le service redémarre avec une trace qui ne désigne qu'**un** plugin, il est **suspendu**
   (non chargé au démarrage suivant, sinon le plantage recommence) et la vue **propose** de le
   désactiver pour de bon ou de le réactiver : « Ce plugin est soupçonné d'avoir fait planter Voyager ».
   Quand la trace désigne **plusieurs plugins ou aucun**, ou que le superviseur passe à l'état
   « échec », rien n'est suspendu seul : l'écran propose, en plus de « Réessayer », de **démarrer sans les
   plugins installés ni ceux des groupes** (les livrés restent chargés et désactivables par la vue) ;
   l'utilisateur réactive les autres **au fur et à mesure**, un à un, jusqu'à retrouver celui qui fait
   planter. La vue dit clairement **ce qui change** quand un plugin est suspendu ou désactivé, et
   l'application **prévient avant d'enregistrer** des données qui dépendent d'un plugin absent
   (question Q30). Un arrêt demandé par l'application efface le marqueur. Ce mécanisme protège des
   **accidents**, pas de la malveillance : sans isolation, un plugin peut écrire ces fichiers ou faire
   planter le service pendant qu'un autre est appelé ; la mécanique précise (appels concurrents, délais)
   revient à la spec.
7. **Confiance.** Les plugins du projet sont **signés** : le format le prévoit dès le départ. Tant
   qu'**aucune clé de production n'est décidée** (question Q31), aucun paquet n'est « Vérifié » : les
   plugins livrés le sont par la signature du paquet de l'application, les imports sont « Non vérifiés »,
   une clé de développement éventuelle s'affiche « signature de développement », et **la clé privée
   n'entre jamais dans le dépôt** (il est public). Un plugin **importé** depuis un fichier est « Non
   vérifié » avec l'avertissement de la décision 5 : **c'est du code de tiers sans isolation, accepté aux
   risques de l'utilisateur, par son choix**. Importer ne demande pas de réglage. Ce qui reste refusé,
   c'est la **distribution** : le catalogue du projet (B) viendra **à l'entrée en production réelle**, avec
   les réponses de la question Q31 (qui garde la clé, comment la changer, où héberger) ; **un catalogue
   ouvert à des tiers (C) n'est pas accepté** avant un ADR qui traite l'isolation, les permissions et la
   relecture.
8. **Plugins du groupe (direction, à détailler quand le serveur du groupe et l'authentification
   existeront).**
   - **Qui, quelle portée.** Un plugin est installé **pour un groupe**, par celui que le groupe désigne :
     sur un serveur auto-hébergé, son propriétaire ; sur le serveur hébergé qui accueille plusieurs
     groupes ([ADR-0010](0010-hebergement-serveur-heberge-multi-groupes.md)), un rôle de groupe qui
     n'existe pas encore (question Q33). L'installation passe par la même vue et le même paquet, vérifiés
     de la même façon ; l'appel au serveur est fait par le processus principal avec le jeton de l'utilisateur
     (que la page n'a pas) et le serveur contrôle le rôle.
   - **Ce que fait le poste d'un membre.** Son service local lit la liste des plugins du groupe (avec les
     empreintes et signatures). **Le processus principal** demande l'accord du membre par une boîte native
     et écrit la demande ; le service **télécharge** le paquet dans la zone de préparation, vérifie, et
     l'applique au démarrage suivant comme n'importe quelle installation. Le service ne télécharge ni
     n'installe rien qui n'ait été confirmé.
   - **Accord.** À l'adhésion (« ce groupe utilise ces plugins ; vous faites confiance à son
     administrateur »), puis **avant l'activation de chaque plugin ajouté**. Une **mise à jour** d'un
     plugin du groupe **redemande l'accord**, sauf si elle porte la **signature du projet** avec la même
     clé ; une version plus ancienne et le **retrait** d'un plugin par l'administrateur sont des
     opérations averties (le poste désactive le plugin, sans rien supprimer). Un plugin « Non vérifié »
     du groupe demande une confirmation renforcée : **l'empreinte ne protège que le transfert** (un
     serveur compromis change le paquet et son empreinte) ; contre lui, seule la signature du projet
     compte, et pour un plugin non signé l'accord du membre est la seule barrière.
   - **Où s'exécute quoi.** Un **calcul** est toujours sur le poste : un résultat ne doit pas dépendre
     d'un aller-retour au serveur, pour rester identique en ligne et hors-ligne
     ([ADR-0004](0004-stockage-des-donnees.md)). Seule la **collecte de données externes** (un connecteur
     et les clés d'API de l'administrateur) peut rester sur le serveur ; en mode solo, un plugin n'a que sa
     partie locale (question Q12 pour les clés). Le manifeste devra dire où chaque partie s'exécute, ce
     qui complète celui de l'ADR-0015 (à préciser avec le serveur du groupe). **Aucune partie serveur d'un
     plugin ne s'exécute sur un serveur qui accueille plusieurs groupes tant que l'isolation n'est pas
     décidée** : un plugin .NET non isolé y verrait les données de tous les groupes.
   - **Plusieurs groupes.** Un membre de plusieurs groupes a une racine par groupe ; un doublon
     d'identifiant se traite comme à la décision 1 (question Q33). `state.json` garde pour un plugin du
     groupe le groupe d'origine et la date de l'accord (traçabilité).
9. **Compatibilité.** Un plugin (installé ou du groupe) écrit pour un SDK que l'application ne reconnaît
   plus (en `0.x`, la moindre mineure) est **écarté** avec « version du SDK incompatible » et reste
   installé : on peut le désinstaller ou le remplacer. Une mise à jour automatique de l'application peut
   donc écarter d'un coup tous les plugins installés ; pour un plugin du groupe, un membre dont
   l'application est trop ancienne est invité à la mettre à jour. La vue l'explique, et la spec voit s'il
   faut prévenir avant la mise à jour.

## Conséquences

- Positif : l'utilisateur gère ses plugins sans toucher aux fichiers de l'application, et ce qu'il
  installe survit aux mises à jour ; les fichiers verrouillés disparaissent du problème (rien n'est chargé
  quand on applique) ; aucune opération de gestion n'est à la portée d'un module de la page sans une
  confirmation que lui seul ne peut donner ; la signature est dans le format dès le départ, donc le
  catalogue et la vérification des plugins du groupe n'obligent pas à le changer ; un plugin qui plante
  est signalé et l'utilisateur garde un moyen de démarrer ; tout le groupe peut avoir les mêmes plugins
  sans perdre le hors-ligne.
- Négatif : **un plugin importé reste du code non isolé et non vérifié** : la protection est
  l'avertissement et une confirmation humaine. Un utilisateur qui clique « Installer » exécute le code
  d'un inconnu avec ses droits. Un plugin déjà installé garde ses droits, y compris celui de lire et
  d'écrire les fichiers de gestion (`requests.json`, `state.json`, marqueur) puisqu'il s'exécute avec ceux
  de l'utilisateur : la suspension après plantage et la vérification d'empreinte ne sont pas une défense
  contre lui. Avec les plugins du groupe, **le serveur devient un second canal de distribution
  de code** (à côté des mises à jour de l'application et des plugins livrés) : un administrateur négligent
  ou un serveur piraté peut faire arriver du code sur le poste de tous les membres. L'accord des membres
  (faible chez des non-informaticiens), la signature du projet et la confirmation renforcée limitent le
  risque ; l'empreinte seule ne protège que le transfert. Plusieurs racines
  compliquent un peu l'hôte et le protocole. Les plugins installés tombent en panne à chaque mineure du
  SDK tant qu'il est en `0.x`.
- À faire ensuite : une **spec** (`/new-spec`) de la gestion des plugins (valeurs des limites, parcours,
  essais de réactivation, avertissement avant une mise à jour), avec la vue à dessiner avec le design ;
  puis, dans le code existant, `PluginCatalog.Load`, `LocalServiceOptions` et `serveUserInterface` passent
  d'un dossier à une liste de racines, avec leurs tests ; la **validation des identifiants** reçoit les
  noms réservés ; le protocole vérifie qu'un fichier servi reste dans le dossier de son plugin **après
  résolution des liens** (la comparaison actuelle porte sur des chaînes) ; `PluginStore` (demandes, état,
  préparation, extraction sûre, vérification de la signature) dans `Voyager.PluginHost` ; la lecture de la
  liste des plugins et l'examen d'un paquet préparé dans `voyager.pluginhost.v1` ; le pont du processus
  principal (boîtes de dialogue natives, ouverture du dossier, préparation, écriture des demandes, relance
  du service) ; un outil `tools/pack-plugin.ps1` qui fabrique un `.voyagerplugin`, son empreinte et, plus
  tard, sa signature, publiés avec la version du plugin (`plugins/<id>/v*`,
  [ADR-0007](0007-git-flow-et-versions-gitversion.md)) ; un plugin d'essai **corrompu** (chemins
  hostiles, bombe de décompression) ; les tests d'architecture (aucune racine de plugins ne vient de
  l'environnement dans l'application installée). Pour les groupes : un ADR ou une spec du serveur du
  groupe (stockage des paquets, liste, accord des membres, lieu d'exécution dans le manifeste) après
  l'authentification et les invitations. Le glossaire gagne *paquet de plugin*, *boîte de dépôt*, *demande
  de plugin*, *plugin installé*, *plugin du groupe*, *partie locale* et *partie serveur* d'un plugin, et
  *propriétaire de l'instance* ; la définition de *Plugin* (« livré avec l'application ») et les schémas
  (`docs/schemas/`) sont mis à jour avec la spec.
- Questions du backlog : **Q11** (qui écrit les plugins, comment on s'en protège) est tranchée dans cette
  direction pour les plugins du projet signés et les imports avec avertissement, ouverte pour un
  catalogue de tiers ; **Q26** a son mécanisme ici ; **Q30** (un membre qui n'a pas un plugin) se réduit
  aux cas d'un membre qui refuse, qui n'a pas encore synchronisé ou dont l'application est trop ancienne ;
  **Q31** (hébergement, clé de signature, révocation) est reportée à l'entrée en production réelle ;
  **Q32** (le serveur du groupe et les plugins) est tranchée dans cette direction. Questions ouvertes
  ajoutées : le rôle et la portée d'un plugin du groupe, et le membre de plusieurs groupes (Q33) ; les
  données d'un plugin à sa désinstallation ou au départ d'un groupe (Q34) ; les plateformes et les
  dépendances natives d'un paquet, le poste étant Windows et le serveur un conteneur Linux (Q35).
