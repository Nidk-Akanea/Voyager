# Spec — Preuve des plugins

- **Statut** : Validée
- **Épic** : E9 — Plateforme modulaire (transverse), étape 3 du squelette (E0)
- **Date** : 2026-10-26

## Objectif

Montrer, avant d'écrire du métier, que Voyager accepte des plugins (aussi appelés *add-ins*) : un
plugin s'ajoute sans toucher au cœur, apporte ses données et, s'il le souhaite, sa propre partie
d'interface, et un plugin défaillant n'empêche ni l'application de démarrer ni les autres plugins de
fonctionner. La preuve se fait sur un point d'extension **technique**, sans rapport avec les voyages :
les faits de diagnostic affichés dans la page « À propos ». Elle applique le mécanisme décidé par
l'[ADR-0015](../adr/0015-mecanisme-de-plugins.md).

## User stories

- En tant que **membre**, je veux voir dans la page « À propos » quels plugins sont installés, leur
  version et s'ils fonctionnent, afin de savoir ce qui tourne dans mon application.
- En tant que **membre**, je veux voir sous chaque plugin les faits qu'il fournit sur le service, afin
  de vérifier que le plugin fait son travail.
- En tant que **membre**, je veux que Voyager démarre et reste utilisable quand un plugin est cassé, et
  savoir lequel est écarté et pourquoi, afin de ne pas perdre mon voyage à cause d'un plugin.
- En tant qu'**auteur de plugin**, je veux ajouter un plugin sans modifier le cœur de Voyager, afin de
  prouver que les contrats suffisent.
- En tant qu'**auteur de plugin**, je veux créer un plugin neuf avec une commande qui respecte déjà les
  règles (manifeste, dépendances, textes, tests), afin de ne pas les réapprendre à chaque plugin.
- En tant que **responsable de la publication**, je veux que le SDK et chaque plugin aient leur propre
  numéro de version calculé, afin qu'une mise à jour d'un plugin ne change pas celle des autres.

## Règles métier

### Ce qu'est un plugin

1. Un plugin est un dossier qui porte un **manifeste** : son identifiant (unique, en minuscules et
   tirets), sa version, la version du SDK pour laquelle il est écrit, ce qu'il apporte (une partie qui
   tourne dans le service local, une partie d'interface, ou les deux) et les points d'extension qu'il
   fournit.
2. Les plugins sont **livrés avec l'application** : ils sont dans le paquet installé, et un plugin se
   met à jour avec l'application. Il n'y a pas, à cette étape, de dossier de plugins de l'utilisateur.
3. Les plugins sont chargés **au démarrage**. Un plugin ajouté n'apparaît qu'au lancement suivant ;
   retirer ou mettre à jour un plugin demande aussi de redémarrer.
4. En développement, on **reconstruit** le plugin à chaque modification : pas de rechargement à chaud.

### Le point d'extension de la preuve

1. Le point d'extension s'appelle **source de faits de diagnostic**. Une source fournit une liste de
   **faits** : un identifiant de libellé, traduisible, et une valeur (texte, nombre ou instant).
2. Le service local est lui-même une source : les faits actuels de la page « À propos » (version,
   moteur, mode, démarrage, état de la base) viennent de lui, et restent affichés comme aujourd'hui.
3. La page « À propos » montre, **sous le service**, la liste des plugins trouvés puis, pour chaque
   plugin chargé, ses faits. Un plugin qui apporte son **propre panneau d'interface** voit ses faits
   présentés par ce panneau ; sinon ils sont présentés par une liste standard.
4. Les valeurs de type instant s'affichent dans la **langue et le fuseau de l'utilisateur** ; le cœur
   fournit un instant, jamais un texte déjà formaté.
5. Les faits et les raisons d'écartement **ne contiennent aucune donnée personnelle** : ni nom de
   compte, ni nom de machine, ni chemin du disque.

### Quand un plugin est refusé ou plante

 1. Un plugin est **écarté** si : son manifeste est illisible ou incomplet ; son identifiant existe déjà ;
    il est écrit pour une version du SDK que l'application n'accepte pas ; son chargement échoue ; ou il
    enregistre quelque chose que son manifeste n'annonce pas.
 2. Un plugin écarté **ne gêne ni le démarrage ni les autres plugins**. Il apparaît dans la liste avec la
    raison de son écartement, dans la langue de l'utilisateur.
 3. Un plugin qui échoue ou tarde trop **pendant un appel** (la demande de ses faits) est signalé « en
    erreur » et ses faits sont omis ; les autres sources s'affichent. Le délai est de **5 secondes** par
    plugin. Un module d'interface qui ne se charge pas, ou qui plante en s'affichant, est traité de même :
    le reste de la page s'affiche.
    Un plugin fournit **50 faits au plus** : au-delà, les premiers sont gardés, les autres omis, et la
    section du plugin le dit.

### Langues et versions

 1. Un plugin fournit ses textes en **français et en anglais**. Un texte manquant en anglais retombe
    sur le français ; un test vérifie que les deux langues ont les mêmes identifiants.
 2. Compatibilité : un plugin écrit pour le SDK `0.1` est accepté par un SDK `0.1.x` ; en `0.x`, toute
    autre version mineure le refuse.
 3. Le **SDK** et **chaque plugin** ont leur numéro de version calculé de leur propre historique, jamais
    écrit à la main (étiquettes `sdk/v…` et `plugins/<id>/v…`). La page « À propos » affiche la version
    de chaque plugin.

### Garanties vérifiées automatiquement

 1. Un plugin ne dépend que du SDK, des types partagés du cœur, de NodaTime, des deux abstractions
    Microsoft (injection, journaux) et de la bibliothèque standard. Un test le vérifie sur les assemblies
    des plugins.
 2. Un plugin d'interface n'importe que le SDK d'interface, React, Fluent UI et la localisation de
    l'hôte. Une règle de lint le vérifie.
 3. **React n'existe qu'en une seule copie** dans la page, plugins compris.
 4. Un plugin ne peut ni remplacer un service de l'hôte, ni faire échouer la construction du conteneur de
    l'hôte.

## Les deux plugins de démonstration

Ils servent à prouver le mécanisme ; ils seront remplacés par les premiers vrais plugins. Les noms et les
faits ont été **retenus par l'utilisateur** sur ma proposition (voir « Décisions prises »).

| Plugin                | Ce qu'il apporte                                                                            | Partie d'interface               |
| --------------------- | ------------------------------------------------------------------------------------------- | -------------------------------- |
| `diagnostics-runtime` | Faits sur l'exécution du service : nombre de processeurs, mémoire utilisée par le processus | Non : la liste standard          |
| `diagnostics-clock`   | Faits sur le temps : l'instant courant du service et la durée depuis son démarrage          | Oui : un panneau avec Actualiser |

Un **plugin cassé** existe seulement dans les tests, jamais dans le paquet : il sert à vérifier les règles
de la section « Quand un plugin est refusé ou plante ».

## Critères d'acceptation

- **Étant donné** l'application installée avec ses deux plugins, **quand** j'ouvre la page « À propos »,
  **alors** je vois les faits du service, puis la liste des plugins avec leur version et l'état
  « chargé », puis les faits de chacun.
- **Étant donné** le plugin `diagnostics-clock`, **quand** j'ouvre la page « À propos », **alors** ses
  faits sont présentés par son propre panneau, avec un bouton « Actualiser » qui met l'instant à jour.
- **Étant donné** le plugin `diagnostics-runtime`, **quand** j'ouvre la page « À propos », **alors** ses
  faits sont présentés par la liste standard.
- **Étant donné** un plugin dont le manifeste est invalide, **quand** l'application démarre, **alors**
  elle démarre, les autres plugins sont chargés, et ce plugin apparaît « écarté » avec sa raison.
- **Étant donné** un plugin écrit pour une version du SDK non acceptée, **quand** l'application démarre,
  **alors** il est écarté avec la raison « version du SDK incompatible ».
- **Étant donné** deux plugins qui portent le même identifiant, **quand** l'application démarre, **alors**
  le second est écarté et le premier reste chargé.
- **Étant donné** un plugin qui lève une exception en fournissant ses faits, **quand** j'ouvre la page,
  **alors** il est marqué « en erreur », ses faits sont absents, et tout le reste s'affiche.
- **Étant donné** un plugin qui n'a pas fini de fournir ses faits en 5 secondes, **quand** j'ouvre la
  page, **alors** il est traité comme en erreur et la page ne reste pas bloquée.
- **Étant donné** un plugin qui fournit plus de 50 faits, **quand** j'ouvre la page, **alors** les 50
  premiers s'affichent et sa section indique que les autres sont omis.
- **Étant donné** un plugin qui enregistre un point d'extension absent de son manifeste, **quand** il
  se charge, **alors** cette inscription est refusée et le plugin est écarté.
- **Étant donné** la page « À propos » avec plusieurs plugins d'interface, **quand** elle s'affiche,
  **alors** une seule copie de React est chargée.
- **Étant donné** la langue anglaise, **quand** j'ouvre la page, **alors** les libellés des plugins et
  les raisons d'écartement sont en anglais ; sans traduction anglaise, ils sont en français.
- **Étant donné** un plugin ajouté au paquet, **quand** l'application est déjà ouverte, **alors** il
  n'apparaît pas ; **quand** je la relance, **alors** il apparaît.
- **Étant donné** l'application installée par `Setup.exe`, **quand** elle démarre, **alors** les deux
  plugins se chargent depuis le paquet, sans aucun réglage de ma machine.
- **Étant donné** un plugin qui dépend de l'infrastructure (par exemple l'accès à la base), **quand**
  les tests d'architecture tournent, **alors** ils échouent.
- **Étant donné** une modification du seul code de `diagnostics-clock`, **quand** les versions sont
  calculées, **alors** seule la version de ce plugin change.

## Cas limites

- **Fuseaux horaires** : l'instant du service est affiché dans le fuseau de l'utilisateur ; la durée
  depuis le démarrage ne dépend d'aucun fuseau.
- **Plugin sans partie d'interface**, ou sans partie dans le service : les deux sont permis ; un plugin
  doit avoir au moins l'une des deux.
- **Aucun plugin trouvé** (dossier vide ou absent) : l'application démarre et la page dit qu'aucun
  plugin n'est installé.
- **Plugin sans fait** : il est chargé, listé, et sa section dit qu'il n'a rien à montrer.
- **Langue** : un libellé de plugin manquant dans la langue de l'utilisateur retombe sur le français ; un
  libellé absent des deux langues affiche son identifiant plutôt que rien.
- **Hors-ligne** : sans effet, les plugins sont locaux.
- **Données personnelles** : voir la règle sur les faits sans donnée personnelle (section « Le point d'extension de la preuve ») ; les journaux du chargement des plugins suivent la même
  règle.
- **Service local relancé** par le superviseur après un plantage : les plugins sont rechargés comme au
  démarrage.

## Hors périmètre

- Installer un plugin depuis l'extérieur du paquet (dossier de l'utilisateur, téléchargement, boutique).
- Plugins de tiers : isolation, signature, permissions (question Q11 pour la suite).
- Rechargement à chaud, retrait ou mise à jour d'un plugin sans redémarrer.
- Services gRPC propres à un plugin et données d'un plugin en base.
- Chargement des plugins par le serveur du groupe.
- Les points d'extension métier (types de prestation, bases de calcul, règles de répartition,
  connecteurs, vues, exports).
- Désactiver un plugin qui fait planter le service en boucle (question Q26).
- Le design des écrans : ils sont sobres, le design vient avant le métier.

## Décisions prises

Sur proposition de Claude, retenues par l'utilisateur :

- **Plugins de démonstration** : `diagnostics-runtime` et `diagnostics-clock`, avec les faits du tableau
  ci-dessus.
- **Délai maximal** pour fournir ses faits : 5 secondes par plugin (section « Quand un plugin est refusé ou plante »).
- **Nombre maximal de faits** : 50 par plugin, les suivants omis (même section).
- **Liste des plugins** : dans la page « À propos » pour la preuve ; une page « Plugins » à part pourra
  venir avec le design.
- **Plugins de démonstration après les vrais plugins** : gardés dans le dépôt comme exemples pour les
  auteurs ; leur présence dans le paquet sera revue quand les premiers vrais plugins arriveront.
- **Squelette de `/new-plugin`** : un plugin minimal (manifeste, partie .NET, partie d'interface,
  textes, test). Pas de modèle de `.proto` : les services gRPC d'un plugin sont hors périmètre.

## Renvoyé au plan d'implémentation

- **Chemin de développement des plugins d'interface avec Vite** (question Q25) : on reconstruit à chaque
  modification (décidé) ; la façon dont la page servie par Vite charge le module est un point technique,
  à fixer dans le plan d'implémentation et non ici.

## Questions ouvertes

Aucune ne bloque la validation.
