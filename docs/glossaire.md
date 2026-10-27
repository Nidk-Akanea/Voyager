# Glossaire

Termes métier (français, utilisés dans la documentation) et leur nom dans le code (anglais).
Un terme ajouté ici doit être utilisé tel quel partout.

## Le projet et le groupe

| Terme métier          | Nom de code        | Définition                                                                                                 |
| --------------------- | ------------------ | ---------------------------------------------------------------------------------------------------------- |
| Voyage                | `Trip`             | Le projet : un groupe, ses envies, ses devis, et à la fin le voyage réalisé. Racine des données du voyage. |
| Membre                | `Member`           | Personne qui a accès au voyage dans Voyager : consulte, modifie, crée des devis, vote.                     |
| Voyageur              | `Traveler`         | Personne qui part. Un voyageur n'est pas forcément membre (un enfant, par exemple).                        |
| Catégorie de voyageur | `TravelerCategory` | Classe utilisée par les tarifs : adulte, enfant…                                                           |
| Envie                 | `Wish`             | Une idée émise pendant l'idéation : un thème, une destination ou une activité qu'on aimerait faire.        |
| Thème                 | `Theme`            | Une orientation du voyage (nature, culture, plage…), utilisée pour classer les envies.                     |
| Destination           | `Destination`      | Un lieu envisagé pour le voyage, en général une zone (pays, région, ville).                                |

## Devis et programme

| Terme métier   | Nom de code                 | Définition                                                                                                                                   |
| -------------- | --------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| Devis          | `Quote`                     | Une simulation du voyage : un programme jour par jour et les prestations choisies, avec leur prix. Chaque membre peut créer les siens.       |
| Vote           | `Vote`                      | Le classement d'un membre : il donne des points dégressifs aux devis, le plus à son préféré.                                                 |
| Score          | `Score`                     | La somme des points reçus par un devis ; sert à classer les devis, pas à décider.                                                            |
| Devis retenu   | `Quote` (statut `Accepted`) | Le devis qu'un membre retient d'un clic, après le vote et la discussion. Révisable : on peut en retenir un autre plus tard.                  |
| Facture        | `Invoice`                   | Le voyage final : le devis retenu, complété par les montants réellement payés et par qui. Terme interne, par analogie avec un projet client. |
| Jour           | `Day`                       | Une journée du voyage ; unité de base du programme.                                                                                          |
| Créneau        | `TimeSlot`                  | Une plage horaire dans un jour (ex. le 12, de 10 h à 12 h), imposée par certaines réservations.                                              |
| Étape          | `Stop`                      | Un séjour en un lieu sur une période (ex. « 3 nuits à Lisbonne »).                                                                           |
| Trajet         | `Leg`                       | Le déplacement entre deux lieux : moyen de transport, durée, distance.                                                                       |
| Ligne de devis | `QuoteLine`                 | Une prestation placée dans un devis : quel jour et quel créneau, en quelle quantité, pour quels voyageurs, à quel prix.                      |
| Faisabilité    | `Feasibility`               | Le verdict sur une journée : le programme tient-il, vu les distances, les transports, les horaires et les créneaux ?                         |

## Réservations et engagements

| Terme métier            | Nom de code          | Définition                                                                                                                     |
| ----------------------- | -------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Réservation             | `Booking`            | Une prestation réservée ailleurs (transport, hébergement, activité) et enregistrée ici, avec sa référence.                     |
| Engagement              | `Commitment`         | Une réservation ou un paiement déjà fait. Il contraint les devis : on ne peut plus l'ignorer.                                  |
| Conditions d'annulation | `CancellationPolicy` | Ce que coûte l'abandon d'un engagement : remboursable, frais, non remboursable.                                                |
| Compatibilité           | `Compatibility`      | Le verdict sur un devis : respecte-t-il les engagements ? Sinon, il est incompatible ou doit intégrer le coût de leur abandon. |

## Catalogue et tarifs

| Terme métier       | Nom de code     | Définition                                                                                                                                                            |
| ------------------ | --------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Catalogue          | `Catalog`       | L'ensemble des prestations connues, dans lequel on pioche pour construire les devis.                                                                                  |
| Prestation         | `Offer`         | Un produit ou service qu'on peut payer : vol, nuit d'hôtel, activité, ligne de bus, visa… Rattachée à zéro, un ou plusieurs lieux. Son type est fourni par un plugin. |
| Type de prestation | `OfferType`     | Une catégorie générique de prestation (hôtel, bus, visa…), commune à tous et fournie par un plugin.                                                                   |
| Tarif              | `Rate`          | Un prix d'une prestation, avec sa base de calcul, sa devise et éventuellement la catégorie de voyageur visée. Indicatif dans les données de référence.                |
| Base de calcul     | `PricingBasis`  | Comment un tarif s'applique : par personne, par unité, au forfait, à l'heure, à la nuit, au jour.                                                                     |
| Type de coût       | `CostType`      | Direct (`Direct`), induit (`Induced`) ou indirect (`Indirect`).                                                                                                       |
| Coût direct        | `Direct`        | Ce qu'on achète pour le voyage : vol, hôtel, activité.                                                                                                                |
| Coût induit        | `Induced`       | Ce que le voyage oblige à payer : visa, assurance, vaccins.                                                                                                           |
| Coût indirect      | `Indirect`      | Ce qu'il faut pour vivre chaque jour sur place : repas, petites dépenses.                                                                                             |
| Lieu               | `Place`         | Un point ou une zone géographique nommé (pays, ville, hôtel, arrêt de bus…), avec coordonnées WGS 84. Les lieux forment une hiérarchie : un lieu a un lieu parent.    |
| Moyen de transport | `TransportMode` | À pied, voiture, bus, train, avion, bateau…                                                                                                                           |
| Ligne de transport | `TransitLine`   | Une ligne précise rattachée à un lieu (« bus ligne A » à Barcelone) avec, si on les connaît, ses arrêts, horaires et prix.                                            |

## Budget et répartition

| Terme métier         | Nom de code     | Définition                                                                                              |
| -------------------- | --------------- | ------------------------------------------------------------------------------------------------------- |
| Budget               | `Budget`        | Les montants d'un devis ou de la facture, ventilés par type de coût, par jour et par voyageur.          |
| Montant prévu        | `PlannedAmount` | Le prix attendu d'une ligne, issu du tarif.                                                             |
| Montant payé         | `PaidAmount`    | Ce qui a réellement été payé.                                                                           |
| Prix du voyage       | `TripPrice`     | Le total de ce qui se réserve ou se paie à l'avance.                                                    |
| Argent sur place     | `SpendingMoney` | Ce qu'il faut avoir pour vivre le voyage, jour par jour.                                                |
| Règle de répartition | `SplitRule`     | Comment un coût se partage entre voyageurs (à parts égales, au prorata, au réel, part réduite enfant…). |
| Part                 | `Share`         | Ce qu'un voyageur doit sur un coût, selon la règle de répartition.                                      |
| Paiement             | `Payment`       | Un montant payé par un membre, pour une ou plusieurs lignes.                                            |
| Remboursement        | `Settlement`    | Un virement entre membres pour équilibrer les comptes à la fin.                                         |
| Devise               | `Currency`      | Code ISO 4217 accompagnant tout montant.                                                                |

## Données et plateforme

| Terme métier                  | Nom de code             | Définition                                                                                                                                                                                             |
| ----------------------------- | ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Données de référence          | `ReferenceData`         | Les données partagées entre les voyages d'un même groupe (pas une base communautaire), rattachées à des lieux : prestations, lignes de transport, tarifs indicatifs. Jamais d'information personnelle. |
| Données du voyage             | `TripData`              | Ce qui appartient à un voyage : membres, envies, devis, prix concrets, paiements. Visible des seuls membres.                                                                                           |
| Source de données             | `DataSource`            | D'où vient une donnée : saisie manuelle, API, extraction ciblée, paquet de données, import. Chaque donnée garde sa source et sa date.                                                                  |
| Format d'échange              | `ExchangeFormat`        | Le format de fichier standardisé, documenté et versionné des données de référence. Extensible par les plugins.                                                                                         |
| Paquet de données             | `DataPackage`           | Un fichier au format d'échange qui regroupe des données de référence (ex. « Barcelone : transports et musées »), à importer ou partager. Jamais de données de voyage.                                  |
| Extraction ciblée             | `PageExtraction`        | Récupérer les informations utiles d'une page précise dont on donne l'adresse (la fiche d'un hôtel). Pas d'aspiration de sites entiers.                                                                 |
| Connecteur                    | `Connector`             | Un plugin qui va chercher des données dans une source (une API de vols, la page d'un site d'hôtels…).                                                                                                  |
| Contrat                       | `Contract`              | L'interface qu'un élément doit respecter pour être branché sur le cœur de Voyager.                                                                                                                     |
| Plugin                        | `Plugin`                | Un paquet (aussi appelé *add-in*), livré avec l'application, qui fournit une ou plusieurs implémentations de contrats : une partie .NET (logique) et, si besoin, un module d'interface React.          |
| Manifeste de plugin           | `PluginManifest`        | Le fichier `plugin.json` d'un plugin : identifiant, version, version du SDK pour laquelle il est écrit, points d'extension fournis.                                                                    |
| Point d'extension             | `ExtensionPoint`        | Un endroit où le cœur accepte des plugins : type de prestation, base de calcul, règle de répartition, connecteur, vue, export.                                                                         |
| Registre d'extensions         | `ExtensionRegistry`     | Là où les plugins déclarent ce qu'ils fournissent, côté service local et côté interface ; l'application n'affiche que ce que le registre contient.                                                     |
| État d'un plugin              | `PluginState`           | Chargé, ou écarté avec la raison, au démarrage ; visible dans la page « À propos ». Un plugin qui échoue pendant un appel est signalé « en erreur » pour cet appel seulement.                          |
| Catalogue de plugins          | `PluginCatalog`         | Les plugins trouvés au démarrage dans le dossier des plugins, chargés ou écartés, et ce qu'ils fournissent.                                                                                            |
| Raison d'écartement           | `PluginRejectionReason` | Pourquoi un plugin n'est pas utilisé : manifeste invalide, identifiant en double, SDK incompatible, échec du chargement, inscription non déclarée.                                                     |
| Source de faits de diagnostic | `DiagnosticFactSource`  | Le point d'extension de la preuve des plugins : ce qui fournit des faits de diagnostic. Le service local en est une, un plugin peut en être une autre.                                                 |
| Fait de diagnostic            | `DiagnosticFact`        | Une information sur le service fournie par une source : un libellé traduisible et une valeur (texte, nombre ou instant), affichée dans la page « À propos ».                                           |
| Service local                 | `LocalService`          | Le processus .NET embarqué dans l'application de bureau : logique métier, base locale, hors-ligne, synchronisation.                                                                                    |
| Serveur du groupe             | `GroupServer`           | Le serveur hébergé qui accueille plusieurs groupes : il centralise les données de chacun, séparément, et diffuse les changements à leurs membres.                                                      |
| Groupe                        | `Group`                 | Les personnes qui organisent leurs voyages ensemble dans Voyager ; un membre peut appartenir à plusieurs groupes, et ne voit que les données des siens.                                                |
| Invitation                    | `Invitation`            | Le lien qui permet à une personne de rejoindre un groupe comme membre.                                                                                                                                 |
| Mode solo                     | `SoloMode`              | L'utilisation de Voyager sans serveur, avec le seul service local.                                                                                                                                     |
| Vue                           | `View`                  | Une façon de présenter un voyage : timeline, planning, carte, programme, graphiques. Fournie par un plugin.                                                                                            |

## Carnet de voyage (plus tard)

| Terme métier        | Nom de code    | Définition                                                                            |
| ------------------- | -------------- | ------------------------------------------------------------------------------------- |
| Carnet              | `Journal`      | Le récit d'un voyage : l'ensemble de ses entrées.                                     |
| Entrée de carnet    | `JournalEntry` | Un texte daté, rattaché à un jour, une étape ou un lieu, pouvant contenir des photos. |
| Photo               | `Photo`        | Une image rattachée à une entrée, avec date, position et tags.                        |
| Idée pour plus tard | `Idea`         | Une envie notée pendant un voyage, à reprendre dans un prochain.                      |
