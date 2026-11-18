# Spec — Le voyage et ses voyageurs

- **Statut** : Validée
- **Révision** : réécrite pour l'architecture client-serveur (ADR-0018, 0019 et 0020, et la bascule faite) ;
  validée par l'utilisateur le 2026-11-18
- **Épic** : E1 — Projet de voyage et idéation
- **Date** : 2026-11-18

## Objectif

Poser la première brique du **socle** de Voyager : le **voyage** et les **voyageurs**, au strict minimum. Tout ce
qui viendra ensuite (devis, budget, lieux, vues, et les plugins) s'y rattachera. Cette brique doit donc rester
petite et stable : on n'y met que ce dont tout le reste a besoin, et rien de ce qu'une fonctionnalité ou un plugin
pourra apporter plus tard.

Le voyage et le voyageur sont **deux entités** distinctes. Un **voyage** appartient à un **groupe** (une famille,
une bande d'amis) dont les membres le préparent ensemble. Un **voyageur** est une **personne** qui fait des
voyages : il appartient à l'utilisateur qui l'a déclaré, et on le retrouve d'un voyage à l'autre. Tout est gardé
par le **serveur du groupe**, qui est la seule source de vérité (ADR-0018).

## User stories

- En tant qu'utilisateur, je veux **créer mon profil de voyageur** à ma première connexion à un serveur, afin que
  Voyager sache qui je suis.
- En tant qu'utilisateur, je veux **déclarer les personnes avec qui je voyage** (nom, pseudo, date de naissance,
  avatar, description), une seule fois, afin de les retrouver d'un voyage à l'autre.
- En tant que membre d'un groupe, je veux **créer un voyage** en lui donnant un nom, afin que le groupe ait un
  endroit où le préparer.
- En tant que membre d'un groupe, je veux **retrouver les voyages de mon groupe** dans une liste, et les
  **renommer**.
- En tant que membre d'un groupe, je veux **ajouter des voyageurs à un voyage** et les en **retirer**, afin de
  dire qui part.
- En tant que membre qui a créé un voyage, je veux pouvoir le **supprimer**.

## Règles métier

### Le voyageur

1. **Un voyageur** est une personne. Il a un **identifiant** propre, stable et jamais réutilisé, et il est
   **réutilisable** : le même voyageur peut faire plusieurs voyages.
2. **Un voyageur appartient à l'utilisateur qui l'a déclaré** (question Q48, tranchée) : lui seul le modifie, le
   supprime, et le retrouve dans la liste de ses voyageurs. Il l'ajoute aux voyages de n'importe lequel de ses
   groupes.
3. Un voyageur a :
   - un **nom** : obligatoire, de 1 à 100 caractères ;
   - un **pseudo** : facultatif, de 1 à 50 caractères ; quand il existe, c'est lui que les écrans montrent ;
   - une **date de naissance** : facultative (principe « du générique au précis ») ; elle servira plus tard aux
     tarifs qui dépendent de l'âge (réductions) ; sans elle, aucune réduction liée à l'âge ne s'appliquera. L'âge
     n'est pas montré : celui qui compte pour un tarif est l'âge à la date du voyage, qui viendra avec les dates ;
   - un **avatar** : une image, facultative, au format PNG, JPEG ou WebP, de 2 Mo au plus, enregistrée telle
     quelle dans la base de données du serveur (question Q50, tranchée ; pas de retouche) ; sans avatar, les
     écrans montrent les initiales ;
   - une **description** : un texte libre, facultatif, de 500 caractères au plus.

   Les textes sont nettoyés de leurs espaces au début et à la fin, et de tout caractère de contrôle ou d'écriture
   bidirectionnelle ; seule la description accepte les retours à la ligne. Deux voyageurs peuvent porter le même nom.
4. **L'utilisateur est toujours un voyageur** : à sa première connexion à un serveur, Voyager lui demande de créer
   son **profil**, qui est un voyageur comme les autres, marqué comme étant le sien. Le nom du compte est proposé
   comme nom du profil, que l'utilisateur change à sa guise : les deux ne restent pas liés. Un voyageur, lui,
   n'est pas forcément un utilisateur (un enfant, un ami qui n'a pas Voyager). En développement, l'identité de
   développement tient lieu de compte (ADR-0019, décision 9).
5. **Ce que les autres membres en voient** (choix de l'utilisateur) : un voyageur qui figure dans un voyage d'un
   groupe est vu **en entier** (nom, pseudo, date de naissance, avatar, description) par tous les membres de ce
   groupe, pour y préparer le voyage. Un voyageur qui ne figure dans aucun voyage d'un groupe n'est vu que de
   son propriétaire. Un membre ne modifie ni ne supprime jamais le voyageur d'un autre.
6. Comme ces informations deviennent visibles du groupe, l'application le **dit** quand on déclare un voyageur
   et quand on l'ajoute à un voyage.
7. Plus tard, un voyageur pourra être **associé à un membre** d'un groupe ; cette brique ne gère pas les
   membres, mais le modèle le permet sans changer.

### Le voyage

1. **Un voyage** a un **identifiant** propre, stable et jamais réutilisé, un **nom** : obligatoire, de 1 à 100
   caractères, mêmes règles que le nom d'un voyageur (deux voyages peuvent porter le même nom), et son
   **créateur**, le membre qui l'a créé.
2. **Un voyage appartient à un groupe**, et seuls les membres de ce groupe le voient : le serveur le vérifie à
   chaque requête (ADR-0018, décision 1). Cette brique ne gère pas les groupes : l'utilisateur travaille dans
   **son seul groupe** (en développement, celui qu'Aspire fournit) ; choisir entre plusieurs groupes viendra avec
   leur spec.
3. **Tout membre du groupe** voit les voyages du groupe, les **renomme**, y **ajoute** des voyageurs et les en
   **retire** (ADR-0019 : un membre fait tout le reste ; les droits plus fins, voyage par voyage, viendront avec la
   collaboration, E6). **Seul le créateur** d'un voyage peut le **supprimer** (choix de l'utilisateur).
4. **Les voyageurs d'un voyage** : un membre y ajoute **ses** voyageurs déjà déclarés, ou un nouveau voyageur créé
   à ce moment ; il n'ajoute pas ceux d'un autre membre, mais peut retirer n'importe quel voyageur du voyage.
   Un voyageur figure au plus une fois dans un même voyage. Ils sont listés dans l'ordre où on les a ajoutés.
   L'utilisateur est **ajouté d'office** aux voyages qu'il crée, et peut s'en retirer : on peut organiser un
   voyage sans y partir. (S'il n'a pas encore de profil, ce qui n'arrive pas puisque l'application le demande
   avant tout, le voyage se crée sans voyageur.)
5. Un voyage peut n'avoir **aucun voyageur** : on peut le créer avant de savoir qui part (principe « du générique
   au précis »). Rien ne bloque sur une information manquante.
6. Si deux membres changent le même voyage en même temps (le nom, par exemple), **le dernier enregistrement
   l'emporte**. Deux membres qui ajoutent en même temps le même voyageur ne l'y mettent qu'une fois, et deux qui
   ajoutent chacun un voyageur différent les y mettent tous les deux : un changement n'en défait pas un autre.

### Supprimer

1. **Retirer un voyageur d'un voyage** ne supprime pas le voyageur : il reste disponible pour les autres voyages.
2. **Supprimer un voyage** est **définitif**, après un **avertissement** qui le dit ; seul son créateur le
   propose. Les voyageurs du voyage ne sont pas supprimés.
3. **Supprimer un voyageur** est **définitif**, après un **avertissement** qui le dit et qui nomme les voyages où
   il figure, parmi ceux des groupes de son propriétaire ; il est retiré de tous. Seul son propriétaire le supprime. Le profil de l'utilisateur ne peut pas être
   supprimé (sauf avec le compte, ci-dessous).
4. La **suppression du compte** (ADR-0019, décision 8) efface les voyageurs de l'utilisateur, sauf ceux qu'un
   voyage d'un groupe cite encore : ceux-là sont **anonymisés** (« voyageur supprimé », sans date de naissance,
   avatar ni description), pour ne pas casser les budgets des autres. Le voyage qu'il a créé reste au groupe, son
   créateur devenu « ancien membre ». Cette brique ne supprime pas de compte, mais son modèle doit permettre ces
   deux états : un créateur et un propriétaire ne sont que des identifiants, que rien n'oblige à retrouver dans les
   comptes, et un voyageur anonymisé est un voyageur dont il ne reste que le nom « voyageur supprimé ».
5. Ce que d'autres fonctionnalités rattacheront plus tard à un voyage ou à un voyageur suivra des règles écrites
   avec elles. Dans un voyage commencé, on **annule** au lieu de supprimer (ADR-0018, décision 7) : cela viendra
   avec les dates du voyage.

### Partout

1. **Le serveur du groupe est la seule source de vérité** : l'application de bureau affiche et saisit, et ne garde
   rien de ces données sur le poste. Quand le serveur est injoignable, elle le dit et **rien n'est mis en
   attente** (ADR-0018, décision 3) ; l'hors-ligne n'existe que sur le mobile, plus tard.
2. **Une création renvoyée deux fois** (une réponse perdue, par exemple) ne crée pas de doublon : le serveur
   reconnaît le même identifiant, le même auteur et le même contenu, et répond comme la première fois. Un
   identifiant déjà pris avec un autre contenu, ou dans un autre groupe, est refusé (ADR-0018, décision 2).
3. L'application montre ce que le serveur sait **au moment où elle le lit** (à l'ouverture d'un écran, ou quand
   on actualise). Voir les changements des autres membres **sans rien faire** viendra plus tard.
4. Le voyage et le voyageur sont des **entités du cœur** (`Voyager.Core`) : les contrôles et les règles sont dans
   le cœur, que le serveur exécute ; l'interface ne fait que saisir et afficher.

## Critères d'acceptation

- **Étant donné** ma première connexion à un serveur, **quand** j'ouvre Voyager, **alors** il me demande de créer
  mon profil de voyageur (au moins un nom, proposé d'après le nom de mon compte), et ce profil apparaît ensuite
  parmi mes voyageurs, marqué comme étant le mien.
- **Étant donné** mon profil, **quand** je déclare « Traveler A », pseudo « TA », né le 1990-03-15, puis « Child C »
  sans date de naissance, **alors** les deux apparaissent parmi mes voyageurs, après m'avoir dit que leurs
  informations seront visibles du groupe ; « Traveler A » est montré sous son pseudo « TA », et un voyageur sans
  avatar l'est avec ses initiales.
- **Étant donné** un nom vide, fait d'espaces ou trop long, un pseudo trop long, ou une description de plus de 500
  caractères, **quand** j'enregistre un voyageur, **alors** l'enregistrement est refusé avec la raison, et rien ne
  change.
- **Étant donné** une image PNG de 1 Mo, **quand** j'en fais l'avatar d'un voyageur, **alors** elle est montrée
  pour ce voyageur ; **étant donné** un fichier texte, ou une image de 3 Mo, **alors** il est refusé avec la
  raison, et l'avatar ne change pas.
- **Étant donné** mon groupe sans voyage, **quand** je crée le voyage « Été en Bretagne », **alors** il apparaît
  dans la liste des voyages du groupe, avec moi pour seul voyageur et moi pour créateur ; **quand** je m'en
  retire, **alors** il n'a plus de voyageur.
- **Étant donné** le voyage « Été en Bretagne », **quand** j'y ajoute « Traveler A » puis « Child C », **alors**
  ils y figurent dans cet ordre, après moi ; **quand** j'essaie d'ajouter de nouveau « Traveler A », **alors** il
  n'y figure toujours qu'une fois.
- **Étant donné** « Traveler A » dans deux voyages, **quand** je change son pseudo, **alors** le nouveau pseudo
  apparaît dans les deux voyages.
- **Étant donné** « Traveler A » dans un voyage, **quand** je l'en retire, **alors** il n'y figure plus, et il est
  toujours parmi mes voyageurs.
- **Étant donné** le voyage « Été en Bretagne », **quand** je le renomme « Bretagne 2027 », **alors** il garde son
  identifiant, son créateur et ses voyageurs.
- **Étant donné** un voyage avec deux voyageurs que j'ai créé, **quand** je le supprime, **alors** un avertissement
  dit que c'est définitif ; si je confirme, le voyage disparaît et les deux voyageurs restent ; si j'annule, rien
  ne change.
- **Étant donné** « Traveler A » dans deux voyages, **quand** je le supprime, **alors** l'avertissement dit que
  c'est définitif et nomme les deux voyages ; si je confirme, il disparaît de mes voyageurs et des deux voyages.
- **Étant donné** mon profil, **quand** je cherche à le supprimer, **alors** ce n'est pas proposé.
- **Étant donné** « Member B », membre de mon groupe, **quand** il ouvre la liste des voyages, **alors** il voit
  « Été en Bretagne » avec ses voyageurs en entier (nom, pseudo, date de naissance, avatar, description) ;
  **quand** il le renomme, **alors** je vois le nouveau nom à ma prochaine lecture de la liste.
- **Étant donné** « Member B » et le voyage « Été en Bretagne » que j'ai créé, **quand** il cherche à le supprimer,
  **alors** ce n'est pas proposé, et le serveur refuse la demande si elle lui parvient ; il ne peut ni modifier ni
  supprimer « Traveler A », qui est à moi, mais peut le retirer du voyage.
- **Étant donné** « Member B », **quand** il cherche à ajouter « Traveler A » à un de ses voyages, **alors** il ne
  le trouve pas parmi ses voyageurs : ce voyageur est à moi.
- **Étant donné** « Member C », membre d'un autre groupe, **quand** il ouvre ses voyages ou demande « Été en
  Bretagne » par son identifiant, **alors** il ne voit ni ce voyage ni mes voyageurs, et le serveur refuse
  l'accès ; un test le vérifie.
- **Étant donné** une création de voyage envoyée deux fois avec le même identifiant et le même contenu, **quand**
  le serveur la reçoit, **alors** il n'y a qu'un voyage ; **étant donné** le même identifiant avec un autre nom,
  **alors** la seconde est refusée.
- **Étant donné** le serveur injoignable, **quand** je fais une de ces actions, **alors** l'application dit que le
  serveur ne répond pas, rien n'est mis en attente ni perdu, et tout refonctionne quand il revient.
- **Étant donné** que je ferme puis rouvre Voyager, **quand** j'ouvre la liste, **alors** mes voyages, mes
  voyageurs et leurs avatars sont toujours là, puisqu'ils sont sur le serveur.

## Cas limites

- **Données personnelles** : nom, pseudo, date de naissance, avatar et description sont des données personnelles,
  parfois d'enfants. Elles sont sur le serveur, et **tous les membres d'un groupe voient en entier** les voyageurs
  de ses voyages : l'application le dit à l'utilisateur. Elles n'apparaissent jamais dans les journaux ni dans la
  télémétrie, et ne font jamais partie des données de référence partagées. Les tests et les exemples n'utilisent
  que des personnes fictives (« Traveler A », « Child C », « Member B ») et des images d'essai.
- **Date de naissance** : une date de calendrier, sans heure ni fuseau. Elle est refusée si elle est postérieure au
  lendemain de la date du serveur (en UTC) : la tolérance d'un jour évite de refuser à tort quelqu'un dont le
  fuseau est en avance.
- **Avatar** : une image choisie par l'utilisateur ; un fichier qui n'est pas une image PNG, JPEG ou WebP, ou qui
  dépasse 2 Mo, est refusé avec la raison. Le format se reconnaît au contenu du fichier, pas à son extension.
- **Texte saisi** : un texte avec des caractères de contrôle ou d'écriture bidirectionnelle est nettoyé avant
  d'être enregistré.
- **Voyage supprimé pendant qu'un autre membre l'a ouvert** : sa prochaine action dit que ce voyage n'existe plus,
  sans erreur technique.
- **Créateur parti** : si le créateur d'un voyage a quitté le groupe, le voyage reste au groupe avec « ancien
  membre » pour créateur, et **personne ne peut plus le supprimer** tant qu'une règle de groupe ne le prévoit pas
  (question ouverte).
- **Voyage sans voyageur** et **voyageur sans voyage** : permis, à tout moment.
- **Plusieurs serveurs** : chaque serveur a ses propres comptes, voyageurs et voyages, sans lien entre eux
  (ADR-0019, décision 1) ; l'application montre ceux du serveur affiché.

## Hors périmètre

- Les **groupes**, les **membres**, les **comptes**, les **invitations**, les **rôles** et le **choix entre
  plusieurs groupes** (ADR-0019, E6), et l'association d'un voyageur à un membre. L'identité de développement les
  remplace en attendant.
- La **suppression d'un compte** et l'anonymisation des voyageurs qui en découle : le modèle les permet, mais la
  brique ne les fait pas.
- Les changements des autres membres **poussés sans rien faire** (le flux unique de l'ADR-0018, décision 3).
- Les **dates** du voyage, sa **destination**, les **lieux** (E2), et l'**annulation** dans un voyage commencé.
- Les **catégories de voyageur** (adulte, enfant) et les tarifs qui dépendent de l'âge.
- Les **envies**, les **devis**, le **budget**, les **devises** (E1 à E4).
- La **copie de voyage** et le hors-ligne sur le mobile (ADR-0018, décision 7).
- Le **contrat** par lequel les plugins liront le voyage et les voyageurs : aucun tant qu'aucun plugin n'en a besoin
  (ADR-0002 : pas de point d'extension sans au moins deux implémentations connues ou prévues). Les identifiants
  sont stables dès maintenant pour qu'il soit simple à ajouter, et un contrat n'exposera jamais plus de données
  personnelles que nécessaire.
- L'**apparence** des écrans (disposition, place dans la navigation) : elle vient avec le design ; la spec décide
  des comportements.

## Questions ouvertes

- **Un voyage dont le créateur a quitté le groupe** (compte supprimé ou membre retiré) ne peut plus être supprimé
  avec la règle « seul le créateur supprime ». Qui peut alors le faire : un administrateur du groupe (piste)
  ? Elle ne bloque pas cette brique, qui n'a pas de groupes, mais elle se pose avec leur spec.
- **Les métadonnées d'un avatar** : un JPEG pris au téléphone contient souvent la position, la date et l'appareil ;
  gardé tel quel, il les livre à tous les membres du groupe. Les retirer avant de le garder (sans retoucher
  l'image) ? À trancher avant le stockage (question Q61).
- **Un voyageur dont le propriétaire a quitté le groupe** reste vu en entier dans les voyages du groupe, alors
  que l'anonymisation n'est prévue qu'à la suppression du compte : que faire au simple départ ? Elle se pose avec
  la spec des groupes (question Q62).
