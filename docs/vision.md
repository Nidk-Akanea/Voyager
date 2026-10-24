# Vision produit — Voyager

> Statut : **brouillon v1** — issu de l'expression de besoin du 2026-10-24, à relire et valider.
> Les passages marqués « *(à confirmer)* » sont des hypothèses, pas des décisions.

## D'où vient le besoin

Organiser un voyage de groupe avec un budget serré se fait aujourd'hui dans un tableur :

- un onglet par jour, avec les coûts de la journée ;
- un catalogue de produits (vols, hôtels, activités) pour tester des combinaisons : « un meilleur hôtel
  mais sans telle activité », ou l'inverse ;
- des calculs de répartition entre voyageurs, qui ne paient pas tous la même chose : une chambre coûte
  le même prix à 1 ou à 3, une activité se paie par personne, un enfant a parfois un tarif réduit.

Le tableur finit par donner un programme et un prix, mais il lui manque l'essentiel :

- une **vue planning** et une **vue carte** ;
- les **distances** et la vérification qu'une journée est faisable ;
- un **programme lisible** et des **graphiques** ;
- le **partage** avec les autres membres du groupe, qui veulent aussi proposer et modifier.

## La proposition

Voyager traite **un voyage comme un projet**, avec la même logique qu'un projet client :

| Projet client | Voyager                                                                          |
| ------------- | -------------------------------------------------------------------------------- |
| Le projet     | Le **voyage** : son groupe, ses envies, ses données                              |
| Les devis     | Les **simulations** : chacune combine des prestations et donne un prix           |
| La facture    | Le **voyage final** : le devis retenu, complété par ce qui a réellement été payé |

Chaque membre du groupe peut construire ses propres devis, les comparer à ceux des autres, et le
groupe en retient un **par vote**.

### Pour qui

Une **famille ou un groupe d'amis** qui organise ses voyages ensemble. Voyager n'est pas une base
communautaire centralisée : chaque groupe a sa propre instance, ses propres données, et la
responsabilité de leur exactitude. Les groupes peuvent en revanche **s'échanger des fichiers de
données** dans un format standard (voir [Les données](#les-données)).

### Le vote

1. Chaque membre donne des **points dégressifs** aux devis : le plus de points à son préféré, un peu
   moins au suivant, et ainsi de suite.
2. Voyager additionne les points et **classe les devis** par score.
3. Le classement éclaire la décision, il ne la prend pas : la discussion reste entre humains, puis un
   membre **retient** le devis choisi d'un clic.
4. On peut toujours revenir en arrière et retenir un autre devis.

### Un choix révisable, des engagements qui contraignent

Le devis retenu n'est pas gravé dans le marbre : on peut en changer plus tard. Mais dès qu'une
prestation est **réservée ou payée**, elle devient un **engagement** :

- certaines réservations portent sur **un jour et un créneau horaire** précis (une visite le 12 à
  10 h) ;
- un devis qui ne respecte pas un engagement devient **incompatible**, ou doit intégrer ce que coûte
  son abandon (montant non remboursable, frais d'annulation) ;
- Voyager signale ces incompatibilités quand on compare ou qu'on change de devis.

## Le cycle de vie d'un voyage

1. **Idéation** — on liste les envies : thèmes, destinations, activités qu'on aimerait faire.
2. **Affinage** — on trie, on précise, on ajoute des idées.
3. **Planification** — on construit des devis : un programme jour par jour, avec des prestations
   choisies dans le catalogue.
4. **Décision** — on compare les devis, chacun vote, et un membre retient le devis choisi. Ce choix
   reste révisable.
5. **Préparation** — on réserve (en dehors de Voyager) et on enregistre ce qui a été payé, et par qui.
   Chaque réservation devient un engagement qui contraint les devis.
6. **Voyage** — on suit les dépenses réelles ; plus tard, on tient le carnet.
7. **Bilan** — on solde les comptes entre voyageurs : qui doit combien à qui.

## Ce que Voyager calcule

### Trois types de coûts

| Type         | Exemples                               | Ce que c'est                         |
| ------------ | -------------------------------------- | ------------------------------------ |
| **Direct**   | billet d'avion, nuit d'hôtel, activité | ce qu'on achète pour le voyage       |
| **Induit**   | visa, assurance, vaccins               | ce que le voyage oblige à payer      |
| **Indirect** | repas, petites dépenses du quotidien   | ce qu'il faut pour vivre chaque jour |

### Des tarifs avec une base de calcul

Chaque prix est rattaché à une base : par personne, par unité quel que soit le nombre d'occupants
(une chambre), au forfait, à l'heure, à la nuit, au jour. Un tarif peut varier selon la catégorie
de voyageur (adulte, enfant).

### Deux montants clés

- **Le prix du voyage** : tout ce qui se réserve ou se paie à l'avance.
- **L'argent à avoir sur place** : ce qu'il faut, jour par jour, pour vivre le voyage pleinement.

### Prévu, payé, réparti

- Chaque coût a un **montant prévu** et un **montant payé**.
- Voyager calcule la **part de chaque voyageur** selon les règles de répartition.
- Il suit **qui a payé quoi** : souvent, l'un paie tous les vols et un autre tous les hôtels.
- Il propose les **remboursements** qui équilibrent les comptes.

### La faisabilité d'une journée

Est-ce que tout ce qui est prévu tient dans la journée, compte tenu des distances, des moyens de
transport disponibles et des créneaux réservés ? La réponse s'affine avec les données : d'une
estimation générique (distance, temps de trajet moyen) jusqu'au plus précis (ligne de bus, arrêts,
horaires, prix).

## Les vues

Les mêmes données se regardent de plusieurs façons :

- **Timeline** : le déroulé du voyage dans le temps.
- **Planning** : le calendrier, jour par jour.
- **Carte** : les lieux, les trajets, les distances.
- **Programme** : un document lisible à partager.
- **Graphiques** : répartition et évolution du budget, comparaison des devis.

## Les données

Un budget n'est juste que si ses données le sont. Voyager doit :

- permettre la **saisie manuelle** quand l'information n'existe nulle part (« bus ligne A, 2 €,
  départ toutes les 20 minutes ») ;
- **faciliter la récupération** des données :
  - API (y compris payantes) pour les vols, hôtels et activités ;
  - **extraction ciblée** : on colle l'adresse d'une page (la fiche d'un hôtel, par exemple) et Voyager
    en extrait les informations utiles — nom, adresse, prix, photos. Pas d'aspiration de sites entiers ;
  - import de fichiers ;
- garder pour chaque donnée **sa source et sa date**, pour savoir à quel point s'y fier.

### Trois niveaux de données *(proposition à valider)*

| Niveau                   | Exemples                                                  | Rattaché à                 | Partagé                     |
| ------------------------ | --------------------------------------------------------- | -------------------------- | --------------------------- |
| **Types génériques**     | « bus », « hôtel », « visa », base de calcul par personne | rien : fournis par plugins | par tous                    |
| **Données de référence** | la ligne A de Barcelone, un hôtel, un musée et ses tarifs | un ou plusieurs **lieux**  | entre les voyages du groupe |
| **Données du voyage**    | cet hôtel, ces 3 nuits, ce prix trouvé le 24/10, payé par | un **devis** d'un voyage   | avec les membres du voyage  |

- Les lieux forment une hiérarchie : Espagne › Catalogne › Barcelone › Sagrada Família. Une donnée
  se rattache au niveau qui lui correspond : un visa au pays, une ligne de bus à la ville, un musée à
  son adresse. Un vol relie deux lieux ; une assurance n'en a aucun.
- Un tarif de référence est **indicatif** (avec sa source et sa date). Le prix d'une ligne de devis est
  **concret** : pour ces dates, ces voyageurs. Un voyage peut ajuster un prix sans modifier la donnée
  de référence partagée.
- Une donnée de référence ne contient jamais d'information personnelle : ce qui concerne le groupe
  reste dans le voyage.
- Les membres sont responsables de ce qu'ils saisissent. Plus tard, quand des API fourniront des
  données de comparaison, un **score de crédibilité** pourra signaler les données douteuses.

### Un format d'échange ouvert, pas une base géante

Les données de référence s'exportent et s'importent sous forme de **paquets de données** : des
fichiers dans un format standardisé et documenté (par exemple « Barcelone : transports et musées »).

- Une communauté peut partager ces paquets comme on partage des fichiers (un dépôt Git, un site de
  téléchargement), sans serveur central ni base de données géante.
- Chaque groupe importe les paquets qui l'intéressent dans son instance, et peut publier les siens.
- Un paquet ne contient que des données de référence, jamais de données de voyage ni d'information
  personnelle.
- Le format est **versionné** et **extensible** : un plugin qui ajoute un type de prestation peut
  ajouter ses propres éléments au format.
- Là où un standard existe déjà, on l'importe plutôt que de le réinventer : **GTFS** pour les lignes
  de transport (arrêts, horaires), publié par beaucoup de villes ; **GeoJSON** ou OpenStreetMap pour
  les lieux.
- Le choix entre XML et JSON fera l'objet d'un ADR, une fois le modèle stabilisé par la calibration
  (Q16 du [backlog](backlog.md#questions-ouvertes)).

## Le carnet de voyage — plus tard

Une fois la préparation réussie, Voyager accompagnera aussi le voyage lui-même : photos taguées,
kilomètres parcourus, lieux visités, idées notées pour un prochain voyage. Ce volet sera conçu après
le premier.

## Principes

1. **Le voyage est un projet de groupe.** Plusieurs membres consultent, modifient et proposent ;
   chacun peut faire ses propres simulations ; le vote éclaire, les humains décident.
2. **Du générique au précis.** On peut commencer avec des estimations et affiner au fil des données
   trouvées, sans jamais être bloqué par une information manquante.
3. **Des données fiables et traçables.** Chaque prix, horaire ou distance sait d'où il vient.
4. **Entièrement modulaire.** Chaque élément (type de prestation, source de données, règle de calcul,
   vue…) répond à un **contrat** ; on ajoute, remplace ou améliore un élément en installant un
   **plugin**, sans toucher au cœur. Voir [ADR-0002](adr/0002-architecture-modulaire-par-contrats-et-plugins.md).
5. **Les données appartiennent au groupe.** Confidentialité par défaut, export possible.

## Hors périmètre (pour l'instant)

- **Réserver dans Voyager.** On cherche et on compare des prix (y compris via des API), mais la
  réservation se fait chez le fournisseur ; Voyager enregistre le résultat *(à confirmer)*.
- Réseau social public, ou base de données communautaire centralisée (le partage passe par des
  paquets de données).

## Questions ouvertes

Suivies dans [backlog.md](backlog.md#questions-ouvertes).
