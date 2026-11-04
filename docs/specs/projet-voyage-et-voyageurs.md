# Spec — Le voyage et ses voyageurs

- **Statut** : Brouillon
- **À revoir** : depuis l'[ADR-0018](../adr/0018-client-serveur-et-copie-de-voyage.md) (client-serveur), repassée au statut « Brouillon » jusqu'à une nouvelle validation
- **Épic** : E1 — Projet de voyage et idéation
- **Date** : 2026-11-04

## Objectif

Poser la première brique du **socle** de Voyager : le **voyage** et les **voyageurs**, au strict minimum. Tout ce
qui viendra ensuite (devis, budget, lieux, vues, et les plugins) s'y rattachera. Cette brique doit donc rester
petite et stable : on n'y met que ce dont tout le reste a besoin, et rien de ce qu'une fonctionnalité ou un plugin
pourra apporter plus tard.

Le voyage et le voyageur sont **deux entités** distinctes : un voyageur est une **personne** qui fait des voyages,
et on la retrouve d'un voyage à l'autre.

## User stories

- En tant qu'utilisateur, je veux **créer mon profil de voyageur** au premier lancement, afin que Voyager sache
  qui je suis.
- En tant qu'utilisateur, je veux **déclarer les personnes avec qui je voyage** (nom, pseudo, date de naissance,
  avatar, description), une seule fois, afin de les retrouver d'un voyage à l'autre.
- En tant qu'utilisateur, je veux **créer un voyage** en lui donnant un nom, afin d'avoir un endroit où le
  préparer.
- En tant qu'utilisateur, je veux **retrouver mes voyages** dans une liste, et les **renommer** ou les
  **supprimer**.
- En tant qu'utilisateur, je veux **ajouter des voyageurs à un voyage** et les en **retirer**, afin de dire qui
  part.

## Règles métier

### Le voyageur

1. **Un voyageur** est une personne. Il a un **identifiant** propre, stable et jamais réutilisé, et il est
   **réutilisable** : le même voyageur peut faire plusieurs voyages.
2. Un voyageur a :
   - un **nom** : obligatoire, de 1 à 100 caractères ;
   - un **pseudo** : facultatif, de 1 à 50 caractères ; quand il existe, c'est lui que les écrans montrent ;
   - une **date de naissance** : facultative (principe « du générique au précis ») ; elle servira plus tard aux
     tarifs qui dépendent de l'âge (réductions) ; sans elle, aucune réduction liée à l'âge ne s'appliquera. L'âge
     n'est pas montré : celui qui compte pour un tarif est l'âge à la date du voyage, qui viendra avec les dates ;
   - un **avatar** : une image, facultative, au format PNG, JPEG ou WebP, de 2 Mo au plus, enregistrée telle quelle
     dans la base de données de Voyager sur le poste (pas de retouche) ; sans avatar, les écrans montrent les
     initiales ;
   - une **description** : un texte libre, facultatif, de 500 caractères au plus.

   Les textes sont nettoyés de leurs espaces au début et à la fin, et de tout caractère de contrôle ou d'écriture
   bidirectionnelle ; seule la description accepte les retours à la ligne. Deux voyageurs peuvent porter le même nom.
3. **L'utilisateur de Voyager est toujours un voyageur** : au premier lancement, Voyager lui demande de créer son
   **profil**, qui est un voyageur comme les autres, marqué comme étant le sien. Un voyageur, lui, n'est pas
   forcément un utilisateur (un enfant, un ami qui n'a pas Voyager).
4. Plus tard, un voyageur pourra être **associé à un membre** d'un groupe (une personne qui a accès au voyage
   dans Voyager) ; cette brique ne gère pas les membres, mais le modèle le permet sans changer.

### Le voyage

1. **Un voyage** a un **identifiant** propre, stable et jamais réutilisé, et un **nom** : obligatoire, de 1 à 100
   caractères, mêmes règles que le nom d'un voyageur. Deux voyages peuvent porter le même nom.
2. **Les voyageurs d'un voyage** : on ajoute à un voyage des voyageurs déjà déclarés, ou un nouveau voyageur créé
   à ce moment. Un voyageur figure au plus une fois dans un même voyage. Ils sont listés dans l'ordre où on les a
   ajoutés. L'utilisateur est **ajouté d'office** aux voyages qu'il crée, et peut s'en retirer : on peut organiser
   un voyage sans y partir.
3. Un voyage peut n'avoir **aucun voyageur** : on peut le créer avant de savoir qui part (principe « du générique
   au précis »). Rien ne bloque sur une information manquante.

### Supprimer

1. **Retirer un voyageur d'un voyage** ne supprime pas le voyageur : il reste disponible pour les autres voyages.
2. **Supprimer un voyage** est **définitif**, après un **avertissement** qui le dit. Les voyageurs du voyage ne sont
   pas supprimés.
3. **Supprimer un voyageur** est **définitif**, après un **avertissement** qui le dit et qui nomme les voyages où il
    figure ; il en est retiré. Le profil de l'utilisateur ne peut pas être supprimé.
4. Ce que d'autres fonctionnalités rattacheront plus tard à un voyage ou à un voyageur suivra des règles écrites
    avec elles.

### Partout

 1. **Tout se fait sur le poste, sans réseau** : aucune action de cette brique ne demande une connexion.
 2. Le voyage et le voyageur sont des **entités du cœur** (`Voyager.Core`) : aucune autre partie ne les définit ni
    ne fait de calcul sur elles. L'interface ne fait que saisir et afficher.

## Critères d'acceptation

- **Étant donné** un premier lancement, **quand** j'ouvre Voyager, **alors** il me demande de créer mon profil de
  voyageur (au moins un nom), et ce profil apparaît ensuite parmi les voyageurs, marqué comme étant le mien.
- **Étant donné** mon profil, **quand** je déclare « Traveler A », pseudo « TA », né le 1990-03-15, puis « Child C »
  sans date de naissance, **alors** les deux apparaissent parmi les voyageurs ; « Traveler A » est montré sous son
  pseudo « TA », et un voyageur sans avatar l'est avec ses initiales.
- **Étant donné** un nom vide, fait d'espaces ou trop long, un pseudo trop long, ou une description de plus de 500
  caractères, **quand** j'enregistre un voyageur, **alors** l'enregistrement est refusé avec la raison, et rien ne
  change.
- **Étant donné** aucun voyage, **quand** je crée le voyage « Été en Bretagne », **alors** il apparaît dans la
  liste de mes voyages, avec moi pour seul voyageur ; **quand** je m'en retire, **alors** il n'a plus de voyageur.
- **Étant donné** une image PNG de 1 Mo, **quand** j'en fais l'avatar d'un voyageur, **alors** elle est montrée
  pour ce voyageur ; **étant donné** un fichier texte, ou une image de 3 Mo, **alors** il est refusé avec la
  raison, et l'avatar ne change pas.
- **Étant donné** le voyage « Été en Bretagne », **quand** j'y ajoute « Traveler A » puis « Child C », **alors**
  ils y figurent dans cet ordre, après moi ; **quand** j'essaie d'ajouter de nouveau « Traveler A », **alors** il n'y figure
  toujours qu'une fois.
- **Étant donné** « Traveler A » dans deux voyages, **quand** je change son pseudo, **alors** le nouveau pseudo
  apparaît dans les deux voyages.
- **Étant donné** « Traveler A » dans un voyage, **quand** je l'en retire, **alors** il n'y figure plus, et il est
  toujours parmi les voyageurs.
- **Étant donné** un voyage avec deux voyageurs, **quand** je le supprime, **alors** un avertissement dit que c'est
  définitif ; si je confirme, le voyage disparaît et les deux voyageurs restent ; si j'annule, rien ne change.
- **Étant donné** « Traveler A » dans deux voyages, **quand** je le supprime, **alors** l'avertissement dit que
  c'est définitif et nomme les deux voyages ; si je confirme, il disparaît des voyageurs et des deux voyages.
- **Étant donné** mon profil, **quand** je cherche à le supprimer, **alors** ce n'est pas proposé.
- **Étant donné** le voyage « Été en Bretagne », **quand** je le renomme « Bretagne 2027 », **alors** il garde son
  identifiant et ses voyageurs.
- **Étant donné** le poste sans réseau, **quand** je fais n'importe laquelle de ces actions, **alors** tout
  fonctionne.
- **Étant donné** que je ferme puis rouvre Voyager, **quand** j'ouvre la liste, **alors** mes voyages, mes
  voyageurs et leurs avatars sont toujours là.

## Cas limites

- **Données personnelles** : nom, pseudo, date de naissance, avatar et description sont des données personnelles.
  Ils restent sur le poste, n'apparaissent jamais dans les journaux, et ne font jamais partie des données de
  référence partagées. Les tests et les exemples n'utilisent que des personnes fictives (« Traveler A »,
  « Child C ») et des images d'essai.
- **Date de naissance** : une date de calendrier, sans heure ni fuseau ; une date dans le futur est refusée.
- **Avatar** : une image choisie par l'utilisateur ; un fichier qui n'est pas une image PNG, JPEG ou WebP, ou qui
  dépasse 2 Mo, est refusé avec la raison. Le format se reconnaît au contenu du fichier, pas à son extension.
- **Texte saisi** : un texte avec des caractères de contrôle ou d'écriture bidirectionnelle est nettoyé avant
  d'être enregistré.
- **Hors-ligne** : tout est local ; la synchronisation avec un groupe viendra avec la collaboration (E6).
- **Voyage sans voyageur** et **voyageur sans voyage** : permis, à tout moment.

## Hors périmètre

- Les **membres**, les comptes, les invitations, le partage et les droits (E6), et l'association d'un voyageur à
  un membre.
- Les **dates** du voyage, sa **destination**, les **lieux** (E2).
- Les **catégories de voyageur** (adulte, enfant) et les tarifs qui dépendent de l'âge.
- Les **envies**, les **devis**, le **budget**, les **devises** (E1 à E4).
- Le **contrat** par lequel les plugins liront le voyage et les voyageurs : aucun tant qu'aucun plugin n'en a besoin
  (ADR-0002 : pas de point d'extension sans au moins deux implémentations connues ou prévues). Les identifiants
  sont stables dès maintenant pour qu'il soit simple à ajouter, et un contrat n'exposera jamais plus de données
  personnelles que nécessaire.
- L'**apparence** des écrans (disposition, place dans la navigation) : elle vient avec le design ; la spec décide
  des comportements.

## Questions ouvertes

Aucune : les réponses de l'utilisateur sont reportées dans les règles et le hors périmètre.
