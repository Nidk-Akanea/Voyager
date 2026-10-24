# Glossaire

Termes métier (français, utilisés dans la documentation) et leur nom dans le code (anglais).
Un terme ajouté ici doit être utilisé tel quel partout.

| Terme métier     | Nom de code    | Définition                                                                                    |
| ---------------- | -------------- | --------------------------------------------------------------------------------------------- |
| Voyage           | `Trip`         | Un déplacement daté, avec un ou plusieurs voyageurs ; racine de tout le reste.                |
| Voyageur         | `Traveler`     | Personne qui participe à un voyage.                                                           |
| Itinéraire       | `Itinerary`    | La suite ordonnée des étapes d'un voyage.                                                     |
| Étape            | `Stop`         | Un séjour en un lieu sur une période (ex. « 3 nuits à Lisbonne »).                            |
| Trajet           | `Leg`          | Le déplacement entre deux étapes (mode de transport, horaires).                               |
| Lieu             | `Place`        | Un point géographique nommé (ville, hôtel, musée…), avec coordonnées WGS 84.                  |
| Réservation      | `Booking`      | Une réservation faite ailleurs (transport, hébergement, activité) et enregistrée ici.         |
| Dépense          | `Expense`      | Un montant dépensé ou prévu, avec sa devise ISO 4217.                                         |
| Budget           | `Budget`       | L'enveloppe prévue pour un voyage, comparée aux dépenses.                                     |
| Carnet           | `Journal`      | Le récit d'un voyage : l'ensemble de ses entrées.                                             |
| Entrée de carnet | `JournalEntry` | Un texte daté, rattaché à un jour, une étape ou un lieu, pouvant contenir des photos.         |
| Photo            | `Photo`        | Une image rattachée à une entrée, avec date et position issues de ses métadonnées si connues. |
