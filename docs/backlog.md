# Backlog

Liste vivante des épics, idées et questions. Une fonctionnalité prête à être construite obtient une
spec dans [specs/](specs/) ; une décision technique obtient un ADR dans [adr/](adr/).

## Priorités

1. **Remplacer le tableur** : E1 à E5 — c'est le MVP.
2. **Travailler à plusieurs** : E6 — mais le modèle doit prévoir le multi-membres dès le départ.
3. **Alimenter les données** : E7 — sources externes, API, extraction ciblée.
4. **Raconter le voyage** : E8 — le carnet, après la réussite du reste.

E0 (cadrage) et E9 (plateforme modulaire) sont transverses et commencent tout de suite.

## Épics

### E0 — Cadrage

- [ ] Valider la [vision produit](vision.md) v1
- [ ] Calibrer avec des voyages réels — **fait par l'utilisateur, de son côté**, à partir de ses
  voyages passés : il confronte le modèle à ce qu'il a vécu et remonte ses remarques sous forme de
  questions ou de corrections. Ses tableurs ne sont pas partagés, pour ne pas orienter la conception
- [x] Trancher les questions ouvertes bloquantes pour la stack (Q1, Q2 ; Q4 reportée à l'ADR d'hébergement)
- [ ] Définir le périmètre exact du MVP
- [x] ADR : architecture modulaire par contrats et plugins ([ADR-0002](adr/0002-architecture-modulaire-par-contrats-et-plugins.md), acceptée)
- [x] ADR : choix de la stack ([ADR-0003](adr/0003-choix-de-la-stack.md), acceptée)
- [ ] ADR : hébergement de l'instance du groupe (Q4)
- [x] ADR : stockage des données ([ADR-0004](adr/0004-stockage-des-donnees.md), acceptée)
- [x] ADR : architecture du code ([ADR-0005](adr/0005-architecture-du-code-hexagonale-par-domaine.md), acceptée)
- [x] Conventions de code ([docs/conventions/](conventions/README.md))
- [x] ADR : dépendances principales ([ADR-0006](adr/0006-dependances-principales.md), acceptée)
- [ ] ADR : authentification des membres sur le serveur du groupe
- [ ] ADR : synchronisation entre le service local et le serveur du groupe
- [ ] ADR : format d'échange des données de référence (après la calibration, voir Q16)
- [x] Outils de Claude : règles par langage, agents de relecture et de tests, skills `/ship`,
  `/after-merge`, `/backlog`, hook de lint Markdown
- [x] ADR : Git Flow et versions GitVersion ([ADR-0007](adr/0007-git-flow-et-versions-gitversion.md), acceptée)
- [ ] Une fois la stack choisie : squelette de solution, build, tests, CI ; injecter la version
  GitVersion dans les assemblies, les `package.json` et les `plugin.json` ; mettre au point le calcul
  par composant (SDK, plugins)
- [ ] Avec le squelette : hook de formatage du code (`dotnet format`, Prettier) et skills
  `/feature`, `/new-use-case`, `/new-plugin`

### E1 — Projet de voyage et idéation

- [ ] Créer un voyage, inviter des membres, déclarer les voyageurs (avec leur catégorie)
- [ ] Noter des envies : thèmes, destinations, activités
- [ ] Trier, préciser, regrouper les envies (affinage)

### E2 — Catalogue, lieux et tarifs

- [ ] Gérer la hiérarchie des lieux (pays › région › ville › lieu précis)
- [ ] Ajouter une prestation au catalogue et la rattacher à ses lieux
- [ ] Lui donner un ou plusieurs tarifs indicatifs : base de calcul, devise, catégorie de voyageur
- [ ] Gérer les moyens de transport
- [ ] Saisir une ligne de transport du générique au précis (prix seul → arrêts → horaires)
- [ ] Garder la source et la date de chaque donnée
- [ ] Séparer données de référence partagées et données du voyage

### E3 — Devis et simulation

- [ ] Créer un devis à partir des envies : programme jour par jour
- [ ] Placer des prestations du catalogue dans les jours (et créneaux) du devis
- [ ] Ajuster le prix d'une ligne pour ce voyage sans modifier la donnée de référence
- [ ] Classer chaque coût : direct, induit, indirect
- [ ] Calculer le prix du voyage et l'argent à avoir sur place
- [ ] Dupliquer un devis pour tester une variante
- [ ] Comparer plusieurs devis côte à côte
- [ ] Vérifier la faisabilité d'une journée (distances, transports, horaires, créneaux)
- [ ] Voter : chaque membre donne des points dégressifs aux devis
- [ ] Classer les devis par score
- [ ] Retenir un devis d'un clic (n'importe quel membre)
- [ ] Changer de devis retenu en cours de route

### E4 — Réservations, budget et répartition

- [ ] Enregistrer une réservation : référence, jour, créneau, conditions d'annulation
- [ ] Transformer réservations et paiements en engagements
- [ ] Signaler les devis incompatibles avec les engagements, ou y intégrer le coût d'abandon
- [ ] Répartir chaque coût entre voyageurs selon une règle de répartition
- [ ] Afficher la part de chaque voyageur
- [ ] Suivre prévu vs payé, ligne par ligne
- [ ] Enregistrer qui a payé quoi
- [ ] Calculer les remboursements qui équilibrent les comptes

### E5 — Vues

- [ ] Timeline
- [ ] Planning (calendrier, avec les créneaux)
- [ ] Carte (lieux, trajets, distances)
- [ ] Programme lisible et partageable (export)
- [ ] Graphiques (budget, comparaison de devis)

### E6 — Collaboration

- [ ] Plusieurs membres modifient le même voyage
- [ ] Chaque membre crée ses propres devis
- [ ] Rôles et droits
- [ ] Partage en lecture seule (programme)

### E7 — Sources de données

- [ ] Contrat de connecteur (voir E9)
- [ ] Extraction ciblée : coller l'adresse d'une page (fiche d'hôtel…) et récupérer les informations
  utiles
- [ ] Connecteurs API : vols, hébergements, activités
- [ ] Configuration des clés d'API par le propriétaire de l'instance
- [ ] Cache des résultats d'API pour limiter les coûts
- [ ] Spécifier le format d'échange (schéma documenté, versionné, extensible par plugin)
- [ ] Exporter et importer des paquets de données
- [ ] Importer les standards existants : GTFS (transports), GeoJSON / OpenStreetMap (lieux)
- [ ] Import (fichiers, mails de confirmation…)
- [ ] Rafraîchir une donnée et voir son ancienneté

### E8 — Carnet de voyage (plus tard)

- [ ] Écrire une entrée de carnet (jour / étape / lieu)
- [ ] Ajouter et taguer des photos
- [ ] Mesurer les kilomètres parcourus
- [ ] Lieux visités sur la carte : prévu vs réel
- [ ] Noter des idées pour plus tard (réutilisables comme envies d'un prochain voyage)

### E9 — Plateforme modulaire (transverse)

- [ ] Identifier les points d'extension : types de prestation, bases de calcul, règles de répartition,
  connecteurs, vues, exports
- [ ] Définir un contrat par point d'extension
- [ ] Mécanisme d'installation et de chargement des plugins (dépend de la stack)
- [ ] Les fonctionnalités de base sont elles-mêmes livrées comme plugins

## Questions ouvertes

| #   | Question                                                                                                                        | Impact                           | Statut                                                                                                                                                         |
| --- | ------------------------------------------------------------------------------------------------------------------------------- | -------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Q1  | Quelles plateformes ?                                                                                                           | Choix de stack                   | **Tranchée** : une application de bureau « intelligente », comme Teams ou le client Claude pour Windows ; téléphone pendant le voyage *(à confirmer)*          |
| Q2  | Quel niveau de hors-ligne ?                                                                                                     | Stack, stockage, synchro         | **Tranchée** : consulter le programme et saisir les dépenses sans réseau ; synchronisation au retour du réseau                                                 |
| Q3  | Mono-utilisateur ou voyages partagés ?                                                                                          | Comptes, droits, synchro         | **Tranchée** : plusieurs membres modifient et font leurs propres devis                                                                                         |
| Q4  | Où tourne le serveur du groupe ?                                                                                                | Stockage, confidentialité, coûts | En partie tranchée : en local (sur un poste ou un NAS) **ou** en ligne, au choix du groupe. Options et coûts à comparer dans un ADR d'hébergement              |
| Q5  | Quel fournisseur de cartes, d'itinéraires et de recherche de lieux (coût, licence) ?                                            | Dépendances, coûts               | Ouverte                                                                                                                                                        |
| Q6  | Partage du carnet : lien privé, export PDF, page publique ?                                                                     | Périmètre E8                     | Ouverte (plus tard)                                                                                                                                            |
| Q7  | Qui retient le devis final ?                                                                                                    | Droits, E3, E4                   | **Tranchée** : vote du groupe ; choix révisable ; les réservations et paiements deviennent des engagements qui contraignent les devis                          |
| Q8  | Le catalogue est-il propre à chaque voyage ou partagé ?                                                                         | Modèle de données, hébergement   | **Tranchée** (proposition à valider) : types génériques communs ; données de référence partagées et rattachées aux lieux ; prix concrets dans le voyage        |
| Q9  | Extraction depuis des sites web : quel cadre ?                                                                                  | Légal, E7                        | **Tranchée** : extraction ciblée d'une page donnée, pas d'aspiration. Reste à choisir comment (voir Q15)                                                       |
| Q10 | Plusieurs devises : quel taux de change, celui du devis ou celui du paiement ?                                                  | E3, E4                           | Ouverte                                                                                                                                                        |
| Q11 | Plugins : qui peut en écrire (vous, des tiers) ? Comment sont-ils distribués et isolés (sécurité) ?                             | Architecture, ADR-0002           | Ouverte                                                                                                                                                        |
| Q12 | API payantes : qui paie ?                                                                                                       | Modèle économique, E7            | **Tranchée** : le propriétaire de l'instance ; il configure les clés                                                                                           |
| Q13 | Règles du vote ?                                                                                                                | E3, E6                           | **Tranchée** : points dégressifs, classement par score, puis un membre retient le devis d'un clic ; réversible                                                 |
| Q14 | Qui peut créer et corriger les données de référence partagées ?                                                                 | Qualité des données, E2          | **Tranchée** : outil pour une famille ou des amis, pas une base communautaire ; les membres sont responsables de leurs saisies. Score de crédibilité plus tard |
| Q15 | Extraction ciblée : côté serveur, via une extension de navigateur, ou par copier-coller du contenu de la page ?                 | E7, choix de stack               | Reportée : à décider après la calibration avec des voyages réels                                                                                               |
| Q16 | Format d'échange : XML (schéma XSD, espaces de noms pour les extensions) ou JSON (JSON Schema, plus léger, natif des API web) ? | Format, plugins, E7              | Ouverte : ADR après la calibration                                                                                                                             |
| Q17 | Paquets partagés par une communauté : sous quelle licence (ex. CC BY, ODbL) ?                                                   | Légal, E7                        | Ouverte (plus tard)                                                                                                                                            |
| Q18 | Coquille de bureau : Electron ou Tauri ?                                                                                        | ADR-0003                         | **Tranchée** : Electron                                                                                                                                        |
| Q19 | Téléphone pendant le voyage : avec quelle technologie (Capacitor, Tauri mobile…) ?                                              | Mobile, hors-ligne               | Reportée : à traiter quand le bureau sera en place                                                                                                             |
| Q20 | Protocole de communication ?                                                                                                    | Contrats, ADR-0002               | **Tranchée** : RPC, pas de REST ; gRPC proposé dans l'ADR-0003                                                                                                 |

## Idées en vrac

- Import automatique des réservations depuis les mails de confirmation.
- Météo prévue sur les jours du programme.
- Statistiques de fin de voyage (distance, pays, dépenses).
- Alertes de prix sur une prestation du catalogue.
- Score de crédibilité des données de référence, en les comparant aux données des API.
