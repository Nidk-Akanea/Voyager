# ADR-0020 — Les plugins sur le serveur

- **Statut** : Acceptée
- **Date** : 2026-11-04
- **Remplace** : —
- **Remplace en partie** :
  - [ADR-0016](0016-installation-et-gestion-des-plugins.md) : les décisions 1, 3 à 6 et 8 (racines, boîte de
    dépôt, demandes et état, vue, plantages, plugins du groupe), déjà remplacées en partie par l'ADR-0018, sont
    décidées ici pour le serveur ; la décision 2 (le paquet) en partie (la version précédente gardée, les
    limites, les clés publiques dans le serveur) ; la décision 7 (la confiance) en partie (plugins livrés
    garantis par l'image, réglage du serveur hébergé, avertissement réécrit pour le serveur) ; la décision 9
    (compatibilité), déjà amendée par l'ADR-0017 ;
- **Complète** : [ADR-0018](0018-client-serveur-et-copie-de-voyage.md) (décision 4), qui lui renvoie ce détail ;
  [ADR-0015](0015-mecanisme-de-plugins.md) (décision 4, prolongée par la suspension pour un groupe) ;
  [ADR-0017](0017-contrats-versionnes-et-dependances-entre-plugins.md) (décisions 3 et 7, précisées pour les
  groupes)

## Contexte

L'[ADR-0018](0018-client-serveur-et-copie-de-voyage.md) a placé les plugins **sur le serveur du groupe** :
il charge leur partie .NET, et chaque client télécharge leurs modules d'interface. Elle a fixé le cadre et
renvoyé le détail ici :

- un plugin est **installé pour tout le serveur** par son administrateur, et **chaque groupe active** ceux
  qu'il veut ; les plugins livrés sont dans l'image du serveur ;
- sur le serveur hébergé, seuls des plugins du **projet** s'installent (relus et signés), avec la même
  confiance que le serveur ; tant que la clé de production n'existe pas (question Q31), il n'a que les plugins
  livrés ; un serveur auto-hébergé installe ce qu'il veut, à ses risques ;
- **un plugin ne voit que les données du groupe qui l'appelle**, et l'isolation de chaque plugin dans son
  processus le garantira ; des plugins de tiers non relus par le projet l'attendent dans tous les cas ;
  **quand** la construire est à décider ici ;
- un plugin qui plante ou boucle est **désactivé pour le seul groupe en cause** (question Q51).

L'[ADR-0019](0019-authentification-et-invitations.md) a fixé les rôles : l'**administrateur du serveur**
(second facteur obligatoire) installe ; l'**administrateur du groupe** active. Une opération
d'administration passe par une confirmation native et une connexion récente vérifiée par le serveur ;
l'administrateur du serveur ne voit ni les données ni l'appartenance des groupes.

Ce qui reste valable de l'[ADR-0015](0015-mecanisme-de-plugins.md) et de l'ADR-0016 : le manifeste, un
`AssemblyLoadContext` et un conteneur privé par plugin, l'enregistrement par méthodes typées, l'appel entouré
d'une limite de durée, le paquet `.voyagerplugin` signable et son extraction sûre, la vérification « Vérifié »
ou « Non vérifié », les contrats versionnés de
l'[ADR-0017](0017-contrats-versionnes-et-dependances-entre-plugins.md). Une partie du code de
`Voyager.PluginHost` (chargement, examen d'un paquet, extraction, empreinte, signature, compatibilité par
contrat) se réutilise dans le serveur ; l'état (`PluginStore`), la trace des plantages (`PluginActivity`) et
la collecte (`DiagnosticFactCollector`) sont à reprendre pour les groupes.

Ce qui change avec le serveur : il accueille **plusieurs groupes à la fois**, il tourne en continu dans un
conteneur Linux, il a une base PostgreSQL, et un arrêt touche tout le monde.

## Options

### Où s'exécutent les plugins, et quand les isoler

- **Option A — Dans le processus du serveur** (comme aujourd'hui dans le service local), un contexte de
  chargement par plugin.
  - Avantages : déjà construit ; un appel à un plugin est un appel de méthode ; rien à surveiller de plus.
  - Inconvénients : aucune isolation. Un plugin voit la mémoire du serveur, sa chaîne de connexion et ses
    secrets ; seuls les ports du cœur, limités au groupe de l'appel, l'empêchent de lire les données d'un autre
    groupe, ce qui suffit contre une erreur dans un plugin relu, pas contre un plugin malveillant. Un plantage
    que .NET ne rattrape pas (débordement de pile, mémoire épuisée) arrête le serveur pour tous les groupes ;
    l'annulation d'un appel est coopérative, donc une boucle sans fin garde un fil et le processeur.
- **Option B — Dans un processus de calcul séparé**, commun à tous les plugins, que le serveur appelle.
  - Avantages : un plantage n'arrête que ce processus, que le serveur relance ; le serveur reste debout.
  - Inconvénients : chaque appel à un point d'extension traverse les processus, donc un contrat gRPC par point
    d'extension ; les plugins partagent toujours un même processus, donc aucune isolation entre eux.
- **Option C — Un processus par plugin** (l'option B de l'ADR-0015), qui ne reçoit que les données du groupe
  de l'appel.
  - Avantages : protège le serveur et les autres plugins : un plugin ne voit que ce qu'on lui envoie, et son
    plantage ne touche que lui. C'est le minimum pour des plugins de tiers.
  - Inconvénients : le coût de B, plus un processus à lancer, surveiller et limiter par plugin. Seul, il ne
    sépare pas encore les groupes entre eux à travers l'état du plugin, ni ne coupe son accès au réseau : il
    faut en plus un bac à sable du système, un processus par couple (plugin, groupe), ou un autre moteur
    (WebAssembly). L'ADR de l'isolation choisira.

### Quand un changement de plugin prend effet

- **Au redémarrage du serveur** : l'administrateur prépare ses changements, ils s'appliquent au démarrage
  suivant, comme dans l'ADR-0016. Simple et sûr ; mais un redémarrage coupe tous les groupes quelques
  secondes, et l'administrateur doit le déclencher.
- **À chaud** : charger et décharger un plugin sans redémarrer (contexte de chargement « déchargeable »).
  Aucune coupure ; mais un plugin déchargé reste en mémoire tant qu'une référence le retient, ce qui est
  difficile à garantir, et un état à moitié changé entre deux appels est délicat.

### Où ranger les données d'un plugin

- **Des données d'extension en JSONB**, rattachées à l'élément du cœur qu'elles complètent et à son groupe, avec
  la version de leur format. Aucune migration de base propre au plugin ; mais pas de clé étrangère vers ce
  qu'elles citent, des migrations de format quand même (dans le plugin), et des requêtes moins efficaces sur
  de gros volumes.
- **Des tables propres au plugin**, dans un schéma PostgreSQL à son nom, avec ses migrations appliquées par le
  serveur. Efficace pour de gros volumes (horaires GTFS) ; mais le serveur doit appliquer et vérifier les
  migrations d'un tiers, et garantir que chaque table porte le groupe.

## Décision

**Option A maintenant, C avant tout plugin non relu ni signé par le projet sur le serveur hébergé** ; les
changements **au redémarrage** ; les données en **JSONB** d'abord (recommandés).

1. **Où s'exécutent les plugins.**
   - La partie .NET d'un plugin s'exécute **dans le processus du serveur**, avec le mécanisme de l'ADR-0015 (un
     contexte de chargement et un conteneur privé par plugin). C'est acceptable pour des plugins relus et
     signés par le projet, qui ont la même confiance que le serveur (ADR-0018, décision 4).
   - **L'isolation par processus (option C)** devient obligatoire avant d'accepter sur le serveur hébergé un
     plugin **non relu ni signé par le projet** (question Q11). Elle fera l'objet d'un ADR à ce moment-là. Un
     serveur auto-hébergé peut installer de tels plugins sans elle, aux risques de son administrateur.
   - **Un réglage de l'hébergeur** dit si le serveur n'accepte que des plugins signés par le projet ; il est
     **activé par défaut** (sûr par défaut), et un auto-hébergeur peut le désactiver. Tant que la clé de
     production n'existe pas (question Q31), un serveur où il est activé n'a que ses plugins livrés.
   - Un paquet ne contient que du **code .NET géré** et du JavaScript : le serveur est un conteneur Linux (x64
     ou arm64), donc pas de dépendance native (question Q35, tranchée ainsi).
2. **La séparation des groupes, dans le processus.**
   - Les points d'extension sont de deux sortes : **du serveur** (un diagnostic, par exemple), appelés sans
     groupe ; **de groupe** (un type de prestation, une règle de répartition), appelés avec le **contexte du
     groupe** de l'appel, passé explicitement par le contrat.
   - Pour un point de groupe, les services du plugin sont créés **dans une portée propre au couple (groupe,
     appel)** de son conteneur privé ; un plugin n'accède aux données que par les ports du cœur, qui filtrent sur
     ce groupe ; il ne garde aucun état partagé entre groupes (pas de cache commun). L'hôte ne partage un appel
     en cours qu'entre demandes du **même groupe**.
   - Le SDK n'expose plus au plugin tout le conteneur de services, seulement des méthodes d'inscription qui
     choisissent la bonne durée de vie ; un test d'architecture contrôle les plugins du dépôt. Pour un plugin
     installé, cette règle repose sur la relecture et la signature du projet : c'est pourquoi l'isolation est
     exigée pour les autres.
   - La sécurité au niveau des lignes de PostgreSQL (une politique par table, filtrée sur le groupe posé dans
     chaque transaction) est une défense de plus contre une erreur de l'hôte, à étudier à la construction ; elle
     ne protège pas d'un plugin dans le processus, qui peut lire la chaîne de connexion.
3. **Les plugins livrés** sont dans l'image du serveur, à un chemin fixe (`/app/plugins`). Ils se mettent à jour
   avec l'image et ne se désinstallent pas.
   - Un plugin livré qui fournit un contrat dont l'**hôte exige un fournisseur** (déclaré dans le SDK, jamais
     par le cœur, qui ne dépend d'aucun plugin : ADR-0002) ne peut pas être désactivé par un groupe. S'il est
     suspendu ou écarté, la fonction qu'il porte est montrée « indisponible » au groupe, et l'administrateur du
     serveur est prévenu.
   - Un **nouveau plugin livré**, arrivé avec une nouvelle image, est activé pour tous les groupes, et leurs
     administrateurs en sont informés ; ils peuvent le désactiver s'il n'est pas exigé.
   - Un plugin installé ne peut pas porter l'identifiant d'un plugin livré : l'examen le refuse.
4. **Installer, mettre à jour, désinstaller** (administrateur du serveur).
   - L'administrateur choisit un paquet `.voyagerplugin` dans l'application de bureau (boîte de dialogue native
     du processus principal) ; le processus principal l'**envoie par morceaux** à un point d'entrée HTTP dédié
     du serveur (un flux montant n'est pas possible en gRPC-Web), après la confirmation native et la connexion
     récente de l'ADR-0019. Le serveur garde le paquet, l'**examine** (manifeste, contrats, signature avec les
     clés publiques qu'il contient, extraction sûre) et renvoie ce qu'il apporte avant la confirmation.
   - Pour un plugin « Non vérifié » (serveur auto-hébergé), la confirmation montre le **manifeste nettoyé**,
     l'empreinte, et l'avertissement sans détour : **ce plugin s'exécutera avec les droits du serveur ; il
     pourra voir les données, les comptes et les réglages de tous les groupes ; son auteur n'est pas vérifié**,
     avec la case « Je comprends ».
   - **Limites** : la taille du paquet (200 Mo par défaut), sa taille une fois décompressé, son nombre de
     fichiers et son rapport de compression sont limités par des réglages de l'hébergeur, en plus de la place
     libre du disque : sur un serveur partagé, un paquet piégé ne doit pas remplir le disque de tous (question
     Q46, tranchée ainsi pour le serveur ; la spec sera mise à jour).
   - Les changements (installer, remplacer, désinstaller, revenir à une version) sont des **demandes**
     enregistrées dans la base ; ils s'appliquent **au prochain démarrage du serveur**, que l'administrateur
     déclenche depuis l'application (« Redémarrer le serveur pour appliquer ») : le serveur s'arrête avec un
     code dédié, et son conteneur est relancé (politique `restart: unless-stopped`). Les membres connectés voient
     une coupure annoncée. En développement, Aspire ne relance pas un projet arrêté : on le relance depuis son
     tableau de bord. Le chargement à chaud est écarté pour l'instant.
   - Les plugins installés sont rangés dans le volume de données du serveur (`/data/plugins/<id>/<version>/`,
     chemin fixe), que l'utilisateur du conteneur peut écrire ; leur **état** (origine, version, date, paquet
     d'origine et son empreinte, vérification, activations, suspensions) est dans la base, par un **port** que
     l'adaptateur PostgreSQL implémente (`Voyager.PluginHost` ne dépend que du SDK). La pose est journalisée
     et reprenable ; la base et le volume se sauvegardent et se restaurent ensemble. En développement, une
     racine sous `artifacts/` est permise, gardée comme l'identité de développement de l'ADR-0019 (décision 9)
     et vérifiée par un test.
   - **Remplacer** garde la version précédente : l'administrateur peut y revenir. Revenir à une version qui ne
     sait pas lire le format des données d'extension écrites par la plus récente est refusé, avec la raison :
     un plugin ne fait évoluer ses formats que vers l'avant, et c'est lui qui les migre.
   - **Désinstaller** un plugin dont des groupes ont des données en prévient l'administrateur (le nombre de
     groupes, pas leur nom) ; les données restent (décision 7).
5. **Activer pour un groupe** (administrateur du groupe).
   - L'écran du groupe liste les plugins installés et compatibles ; l'administrateur du groupe active ou
     désactive ceux qu'il veut, avec la confirmation native. L'effet est **immédiat** pour le groupe.
   - **Le serveur applique l'activation** : il n'appelle un plugin que pour les groupes qui l'ont activé, et
     refuse, pour les autres, les appels aux services propres du plugin et les appels d'un autre plugin vers
     lui. Dans l'application, le module d'interface d'un plugin non activé n'est ni enregistré ni affiché dans
     la vue de ce groupe.
   - Un plugin **installé** est désactivé pour les groupes existants et les nouveaux, jusqu'à ce que leur
     administrateur l'active ; un nouveau groupe a les plugins livrés activés.
   - **Dépendances par contrat** (ADR-0017) : activer un plugin qui requiert un contrat propose d'activer en
     même temps un fournisseur ; désactiver le **dernier** fournisseur actif d'un contrat dont un plugin actif
     du groupe a besoin est refusé, avec la raison ; un contrat fourni par l'hôte n'exige rien. L'hôte ne remet
     à un consommateur que les fournisseurs **activés pour le groupe** de l'appel ; quand un fournisseur est
     suspendu ou retiré, ses consommateurs sont écartés pour ce groupe, avec la raison. Le choix d'un fournisseur
     unique, quand un contrat le demande, se fait par groupe. Rien de cela n'existe avant que `requires` soit
     construit (ADR-0017, décision 8).
6. **Un plugin qui échoue, plante ou boucle** (question Q51).
   - **Échecs** : chaque appel a une durée limite et un jeton d'annulation. Un appel qui la dépasse, ou qui lève
     une exception, est journalisé et sa contribution omise. Après plusieurs échecs rapprochés pour un même
     groupe, le plugin est **suspendu pour ce groupe pendant un temps** (une heure par défaut), puis réessayé : la
     panne passagère d'une API ne doit pas obliger à le réactiver à la main dans chaque groupe.
   - **Plantages** : le serveur note les appels en cours (plugin, groupe) dans une **trace** sur le volume de
     données (pas dans la base : une écriture par appel), qui survit à un arrêt brutal ; un appel qui a dépassé
     sa durée limite en sort. Un arrêt normal efface la trace : le délai d'arrêt du conteneur (`stop_grace_period`)
     et celui de l'hôte .NET sont accordés pour qu'un arrêt normal ne passe pas pour un plantage.
   - Au démarrage après un plantage, un couple (plugin, groupe) est **suspendu pour ce groupe**, jusqu'à ce que
     son administrateur le réactive, s'il était en cours lors de **deux plantages de suite, rapprochés**, et que
     peu de couples étaient en cours à ces moments-là ; le plantage est attribué au plugin appelé directement
     (ADR-0017, décision 7). Sinon rien n'est suspendu, et l'administrateur du serveur est prévenu d'un
     plantage sans suspect (question Q41, reprise ainsi pour le serveur).
   - Un plantage pendant le **chargement** d'un plugin le désigne à coup sûr : il est **suspendu pour tout le
     serveur**, et l'administrateur du serveur est prévenu.
   - Un plugin suspendu pour un groupe est montré à son administrateur, avec « Réactiver » (confirmation
     native). L'administrateur du serveur voit, par plugin, le **nombre** de groupes où il est suspendu, jamais
     lesquels. Une **nouvelle version** du plugin lève ses suspensions (question Q42, tranchée ainsi).
7. **Les données des plugins.**
   - Un plugin range ses données comme **données d'extension**, en JSONB, rattachées à l'élément du cœur
     qu'elles complètent, avec son groupe, l'identifiant du plugin et la version de leur format. Le cœur les
     garde sans les interpréter ; le plugin les **valide** à l'écriture, par un point d'extension prévu pour
     cela. Les montants et les dates y suivent les règles de l'ADR-0018 (décision 9), sérialisés par le SDK.
   - Des **tables propres à un plugin** (dans un schéma PostgreSQL à son nom, chaque ligne portant son groupe)
     ne seront ouvertes qu'au premier plugin qui en a besoin (par exemple des horaires GTFS), par un ADR.
   - **Désactiver, suspendre ou désinstaller un plugin ne supprime aucune donnée** : elles restent, inertes.
     L'élément concerné est montré en lecture seule, « fourni par un plugin indisponible » ; les montants que le
     cœur a enregistrés pour lui restent comptés dans les totaux (principe « du générique au précis »), et ce
     que seul le plugin calcule est montré « indisponible ». Tout reprend vie quand le plugin revient
     (questions Q34 et Q30, tranchées ainsi). Les données d'un groupe ou d'un compte supprimé suivent l'ADR-0019
     (décision 8).
8. **Les modules d'interface.** Le serveur publie, pour **chaque plugin qu'il a chargé**, livrés compris, son
   module d'interface avec sa version, son empreinte et sa signature. L'application de bureau utilise toujours
   le module **de la version que le serveur a chargée** : les modules livrés dans son paquet ne sont qu'un cache,
   rangé par (plugin, version), et un module d'une autre version est téléchargé, vérifié et servi comme le dit
   l'ADR-0018 (décision 5). Elle ne télécharge que ceux des plugins activés pour un de ses groupes sur ce
   serveur.
9. **Réglages d'un plugin.** Un plugin peut déclarer des réglages, fixés par l'administrateur du serveur (par
   exemple la clé d'une API payante, question Q12). Ils sont rangés **chiffrés** dans la base, avec une clé que
   fournit l'hébergeur (un secret, jamais dans la base ni dans l'image) et une finalité propre à chaque plugin ;
   le mécanisme (Data Protection avec des clés protégées et jamais purgées, ou un chiffrement AES-GCM) se choisit
   à la construction. Ils ne sont jamais renvoyés en clair à l'interface ni écrits dans les journaux. Des
   réglages par groupe viendront avec le premier plugin qui en a besoin.
10. **Compatibilité et mise à jour du serveur.** Un plugin dont un contrat requis n'est plus pris en charge par
    le serveur (ADR-0017) est **écarté** au démarrage, avec la raison, et reste installé ; les groupes qui
    l'avaient activé le voient « indisponible » (décision 7). Avant de mettre à jour l'image, l'administrateur
    lance la **nouvelle image** en mode vérification sur le volume de données monté en lecture seule
    (`check-plugins`) : elle liste les plugins installés qu'elle écarterait (question Q37, tranchée ainsi pour le
    serveur).
11. **Les écrans.** L'écran « Plugins » de l'application devient deux écrans : **les plugins du serveur**, pour
    son administrateur (installer, remplacer, désinstaller, revenir à une version, suspensions comptées, demandes
    en attente, redémarrage) ; **les plugins du groupe**, pour l'administrateur du groupe (activer, désactiver,
    réactiver après une suspension). Un membre voit la liste des plugins actifs de son groupe, en lecture seule.
    Ils seront dessinés avec le design, et décrits dans une spec.
12. **En développement**, Aspire construit les plugins (`tools/build-plugins.ps1`) avant de lancer le serveur,
    qui les charge depuis `artifacts/plugins/` comme plugins livrés (dérogation de la décision 4) ; le groupe de
    développement les a tous activés.

## Conséquences

- **Positif** :
  - tous les groupes d'un serveur ont la même version d'un plugin, et le serveur contrôle les données des
    plugins puisqu'il les fait tourner ;
  - activer un plugin pour un groupe est immédiat et sans effet sur les autres groupes ;
  - une partie de `Voyager.PluginHost` se réutilise ; seuls les modules d'interface sont rangés sur les postes ;
  - un plugin qui plante n'est retiré que du groupe où il plante.
- **Négatif** :
  - **sans isolation**, un plantage brutal arrête le serveur pour tous les groupes, quelques secondes, jusqu'au
    redémarrage du conteneur ; une fuite de mémoire fait tuer tout le conteneur ; une boucle sans fin garde un
    fil et le processeur, et peut ralentir tous les groupes, jusqu'au redémarrage. C'est accepté tant que seuls
    des plugins relus par le projet tournent sur le serveur hébergé ;
  - un plugin installé exécute son code de chargement même si aucun groupe ne l'a activé ;
  - l'isolation sera un chantier important (un contrat gRPC par point d'extension, des processus à surveiller) ;
    c'est elle qui ouvrira le serveur hébergé à d'autres plugins ;
  - installer ou mettre à jour un plugin demande un redémarrage du serveur, qui coupe tous les groupes quelques
    secondes ;
  - les données d'extension en JSONB se prêtent mal aux gros volumes et n'ont pas de clé étrangère, d'où les
    tables propres à venir.
- **Le code existant** :
  - **SDK** : `IPluginBuilder` n'expose plus tout le conteneur de services ; il reçoit le contexte du groupe et
    les réglages ;
  - **`Voyager.PluginHost`** : `PluginStore` (fichiers `requests.json` et `state.json`) est remplacé par un port
    vers la base ; `PluginActivity` trace des couples (plugin, groupe) ; `DiagnosticFactCollector` partage un
    appel par (source, groupe) ; l'extraction reçoit les limites de la décision 4 ;
  - **contrat** `voyager.pluginhost.v1` : la copie de préparation et la liste sans groupe ne conviennent plus ;
    un service d'administration et un service par groupe demandent une nouvelle version, contrôlée par
    `buf breaking` ;
  - **bureau** : la boîte de dépôt, la copie de préparation, `requests.json` et le démarrage de rétablissement
    sont retirés ; les confirmations natives et le choix d'un fichier restent, pour envoyer au serveur ; le
    service des modules (`servedPlugins.ts`, `appProtocol.ts`) se range par serveur et par version ;
  - **serveur et déploiement** : le volume `/data` et ses droits dans `deploy/GroupServer.Dockerfile`, un fichier
    Compose avec sa politique de redémarrage et son délai d'arrêt, les réglages de l'hébergeur ;
  - **Aspire** : la racine des plugins installés (`VOYAGER_USER_PLUGINS_DIRECTORY`,
    `Voyager__UserPluginsDirectory`) passe au serveur ;
  - **tests** : ceux de l'état, de la trace et des racines sont à réécrire.
- **Questions** : tranchées ici, Q30, Q34, Q35, Q37, Q42 et Q46 (pour le serveur) ; reprise pour le serveur, Q41
  (l'administrateur est prévenu d'un plantage sans suspect) ; sans objet, Q43 (la désactivation n'est plus une
  demande appliquée au redémarrage).
- **Glossaire** : Activation pour un groupe, Suspension pour un groupe, Données d'extension, Réglage d'un plugin.
- **Si l'ADR est accepté** :
  - ajouter « Remplacée en partie par ADR-0020 » à l'ADR-0016, et « Complétée par ADR-0020 » aux ADR 0015, 0017
    et 0018 ;
  - réécrire la spec de la gestion des plugins pour le serveur (les deux écrans, les demandes, le redémarrage,
    les suspensions, les limites), avant de la construire ;
  - marquer caducs dans le glossaire les termes de la boîte de dépôt, de la copie de préparation, des demandes,
    de « Redémarrer pour appliquer » et du démarrage de rétablissement, et préciser « Plugin suspendu »
    (suspendu pour tout le serveur) ; mettre le backlog à jour.
