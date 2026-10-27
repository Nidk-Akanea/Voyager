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
- [x] ADR : hébergement, un serveur hébergé pour plusieurs groupes ([ADR-0010](adr/0010-hebergement-serveur-heberge-multi-groupes.md), acceptée) ; serveur en développement sous Docker
- [x] ADR : Aspire pour le développement ([ADR-0011](adr/0011-aspire-pour-le-developpement.md), acceptée) ; `Voyager.AppHost` et `Voyager.ServiceDefaults` en place
- [ ] Première migration PostgreSQL : déclarer l'extension PostGIS dans le modèle EF
  (`HasPostgresExtension("postgis")`) — la base `voyager` créée par Aspire ne l'a pas, et la
  migration la créera aussi en production
- [x] ADR : stockage des données ([ADR-0004](adr/0004-stockage-des-donnees.md), acceptée)
- [x] ADR : architecture du code ([ADR-0005](adr/0005-architecture-du-code-hexagonale-par-domaine.md), acceptée)
- [x] Conventions de code ([docs/conventions/](conventions/README.md))
- [x] ADR : dépendances principales ([ADR-0006](adr/0006-dependances-principales.md), acceptée)
- [ ] ADR : authentification des membres et invitations (indispensable avant toute mise en ligne, ADR-0010)
- [ ] ADR : mise en ligne — choix de l'hébergeur, RGPD, sauvegardes, proxy HTTPS, sondes de santé
  du conteneur, export de la télémétrie (Q22)
- [ ] ADR : synchronisation entre le service local et le serveur du groupe
- [ ] ADR : format d'échange des données de référence (après la calibration, voir Q16)
- [x] Outils de Claude : règles par langage, agents de relecture et de tests, skills `/ship`,
  `/after-merge`, `/backlog`, hook de lint Markdown
- [x] ADR : Git Flow et versions GitVersion ([ADR-0007](adr/0007-git-flow-et-versions-gitversion.md), acceptée)
- [x] ADR : globalisation français / anglais avec Fluent et Linguini ([ADR-0008](adr/0008-globalisation-fluent-et-linguini.md), acceptée)
- [x] ADR : installation et mises à jour avec Velopack ([ADR-0009](adr/0009-installation-et-mises-a-jour-velopack.md), acceptée) ; script `tools/release.ps1` avec le paquet installable
- [x] Squelette, étape 1 — fondations : solution .NET, tests d'architecture, espace pnpm, ESLint,
  Prettier, Buf, CI, version GitVersion dans les assemblies
- [x] Squelette, étape 2 — tranche technique « À propos » de bout en bout (Electron → React →
  gRPC-Web → service local → SQLite), lancée par Aspire avec le serveur du groupe et PostgreSQL
- [x] Paquet installable (sorti de l'étape 2 du squelette, décision du 2026-10-25) : service local
  publié avec l'application, Velopack avec vérification des mises à jour au démarrage, script
  `tools/release.ps1`, version GitVersion (ADR-0009) ; journaux en fichiers sur le poste
  (electron-log, Serilog, ADR-0006) ; *fuses* Electron verrouillés
- [ ] Première release publiée : workflow CI qui lance `tools/release.ps1` à chaque tag, envoi du
  paquet sur la release GitHub (`vpk upload github`) et de l'image Docker (ADR-0009) ; première
  vérification de bout en bout de la mise à jour automatique, impossible tant qu'aucune release n'existe ;
  essai d'installation, de désinstallation et de réinstallation avec `Setup.exe` : la base
  (`%LOCALAPPDATA%\Voyager\voyager.db`) doit rester intacte ([ADR-0013](adr/0013-assemblage-du-paquet-de-l-application-de-bureau.md))
- [x] Écran de démarrage et écran de mise à jour (recherche, téléchargement avec progression,
  installation, redémarrage), source de mises à jour choisie à la fabrication
  ([ADR-0014](adr/0014-ecran-de-demarrage-et-mise-a-jour-au-demarrage.md), proposée)
- [ ] Design de l'écran de démarrage et de l'écran de mise à jour (aujourd'hui sobres), avec celui de
  l'application, avant de commencer le métier
- [ ] Essai d'installation puis de mise à jour avec `Setup.exe` et un serveur web (voir le README) ;
  vérifier au passage que Velopack refuse une redirection de `https` vers `http`
- [ ] Icône de l'application (installateur, fenêtre, raccourcis) et nom de l'éditeur dans l'installateur
- [ ] Signature de code du paquet, avant une diffusion hors du groupe (SmartScreen avertit sans elle)
- [x] Surveillance du service local : relancé (3 fois en 60 s au plus) s'il s'arrête pendant que
  l'application tourne, puis « Réessayer » quand il n'a pas pu démarrer ou redémarrer
- [ ] ADR du mécanisme de plugins : [ADR-0015](adr/0015-mecanisme-de-plugins.md) (proposée)
- [ ] Squelette, étape 3 — preuve des plugins, après l'ADR du mécanisme de plugins ; calcul de version
  par composant (SDK, plugins)
  - [x] Spec de la preuve : [plateforme-preuve-des-plugins](specs/plateforme-preuve-des-plugins.md)
    (validée)
  - [x] `Voyager.Sdk` (`IPlugin`, `IPluginBuilder`, le contrat de la source de faits), côté .NET
  - [x] `@voyager/plugin-sdk` (registre d'extensions, services de l'hôte), côté interface
  - [x] `Voyager.PluginHost` : manifeste, contexte de chargement par plugin, conteneur privé, plugin écarté
    sans gêner les autres ; service gRPC `voyager.pluginhost.v1` et service local comme source de faits
  - [x] Chargement côté interface : service qui liste les plugins, protocole `plugins/<id>/ui/`, *import
    map* et empreinte dans la CSP
  - [x] Deux plugins de démonstration, assemblés dans `artifacts/plugins/` (`tools/build-plugins.ps1`)
  - [x] Plugins dans le paquet (`resources/plugins`, `tools/release.ps1`) ; essai avec `Setup.exe` à faire avec celui
    de l'installation
  - [x] Tests d'architecture (Sdk, assemblies des plugins), règle de lint sur les imports d'un plugin
  - [ ] Calcul de version par composant (`sdk/v*`, `plugins/<id>/v*`)
  - [ ] Limites connues du chargement des plugins .NET, acceptées pour des plugins de l'équipe (ADR-0015,
    décision 10) mais à reprendre avant des plugins de tiers : pas de limite de durée pour le constructeur
    et `Configure` d'un plugin (un plugin qui boucle empêche le service de démarrer) ; pas de dépendances
    natives ni de `Microsoft.*` propres à un plugin ; DLL d'un plugin verrouillées tant que le service
    tourne ; deux plugins `a` et `a-b` peuvent entrer en collision de libellés ; erreur `NOT_FOUND` de
    `voyager.pluginhost.v1` sans `google.rpc.ErrorInfo` (le paquet n'est pas encore dans le dépôt)
- [x] Hook de formatage du code (`dotnet format`, Prettier), skills `/feature` et `/new-use-case`
- [ ] Avec la preuve des plugins : skill `/new-plugin`

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

| #   | Question                                                                                                                                                                                                                                                                                               | Impact                                | Statut                                                                                                                                                                                                                                       |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Q1  | Quelles plateformes ?                                                                                                                                                                                                                                                                                  | Choix de stack                        | **Tranchée** : une application de bureau « intelligente », comme Teams ou le client Claude pour Windows ; téléphone pendant le voyage *(à confirmer)*                                                                                        |
| Q2  | Quel niveau de hors-ligne ?                                                                                                                                                                                                                                                                            | Stack, stockage, synchro              | **Tranchée** : consulter le programme et saisir les dépenses sans réseau ; synchronisation au retour du réseau                                                                                                                               |
| Q3  | Mono-utilisateur ou voyages partagés ?                                                                                                                                                                                                                                                                 | Comptes, droits, synchro              | **Tranchée** : plusieurs membres modifient et font leurs propres devis                                                                                                                                                                       |
| Q4  | Où tourne le serveur du groupe ?                                                                                                                                                                                                                                                                       | Stockage, confidentialité, coûts      | **Tranchée** (ADR-0010) : un serveur hébergé en ligne accueille plusieurs groupes ; auto-hébergement Docker possible ; mode solo sans serveur                                                                                                |
| Q5  | Quel fournisseur de cartes, d'itinéraires et de recherche de lieux (coût, licence) ?                                                                                                                                                                                                                   | Dépendances, coûts                    | Ouverte                                                                                                                                                                                                                                      |
| Q6  | Partage du carnet : lien privé, export PDF, page publique ?                                                                                                                                                                                                                                            | Périmètre E8                          | Ouverte (plus tard)                                                                                                                                                                                                                          |
| Q7  | Qui retient le devis final ?                                                                                                                                                                                                                                                                           | Droits, E3, E4                        | **Tranchée** : vote du groupe ; choix révisable ; les réservations et paiements deviennent des engagements qui contraignent les devis                                                                                                        |
| Q8  | Le catalogue est-il propre à chaque voyage ou partagé ?                                                                                                                                                                                                                                                | Modèle de données, hébergement        | **Tranchée** (proposition à valider) : types génériques communs ; données de référence partagées et rattachées aux lieux ; prix concrets dans le voyage                                                                                      |
| Q9  | Extraction depuis des sites web : quel cadre ?                                                                                                                                                                                                                                                         | Légal, E7                             | **Tranchée** : extraction ciblée d'une page donnée, pas d'aspiration. Reste à choisir comment (voir Q15)                                                                                                                                     |
| Q10 | Plusieurs devises : quel taux de change, celui du devis ou celui du paiement ?                                                                                                                                                                                                                         | E3, E4                                | Ouverte                                                                                                                                                                                                                                      |
| Q11 | Plugins : qui peut en écrire (vous, des tiers) ? Comment sont-ils distribués et isolés (sécurité) ?                                                                                                                                                                                                    | Architecture, ADR-0002                | Partiellement tranchée par l'ADR-0015 (proposée) pour les plugins de confiance livrés avec l'application ; ouverte pour des tiers                                                                                                            |
| Q12 | API payantes : qui paie ?                                                                                                                                                                                                                                                                              | Modèle économique, E7                 | **Tranchée** : le propriétaire de l'instance ; il configure les clés                                                                                                                                                                         |
| Q13 | Règles du vote ?                                                                                                                                                                                                                                                                                       | E3, E6                                | **Tranchée** : points dégressifs, classement par score, puis un membre retient le devis d'un clic ; réversible                                                                                                                               |
| Q14 | Qui peut créer et corriger les données de référence partagées ?                                                                                                                                                                                                                                        | Qualité des données, E2               | **Tranchée** : outil pour une famille ou des amis, pas une base communautaire ; les membres sont responsables de leurs saisies. Score de crédibilité plus tard                                                                               |
| Q15 | Extraction ciblée : côté serveur, via une extension de navigateur, ou par copier-coller du contenu de la page ?                                                                                                                                                                                        | E7, choix de stack                    | Reportée : à décider après la calibration avec des voyages réels                                                                                                                                                                             |
| Q16 | Format d'échange : XML (schéma XSD, espaces de noms pour les extensions) ou JSON (JSON Schema, plus léger, natif des API web) ?                                                                                                                                                                        | Format, plugins, E7                   | Ouverte : ADR après la calibration                                                                                                                                                                                                           |
| Q17 | Paquets partagés par une communauté : sous quelle licence (ex. CC BY, ODbL) ?                                                                                                                                                                                                                          | Légal, E7                             | Ouverte (plus tard)                                                                                                                                                                                                                          |
| Q18 | Coquille de bureau : Electron ou Tauri ?                                                                                                                                                                                                                                                               | ADR-0003                              | **Tranchée** : Electron                                                                                                                                                                                                                      |
| Q19 | Téléphone pendant le voyage : avec quelle technologie (Capacitor, Tauri mobile…) ?                                                                                                                                                                                                                     | Mobile, hors-ligne                    | Reportée : à traiter quand le bureau sera en place                                                                                                                                                                                           |
| Q20 | Protocole de communication ?                                                                                                                                                                                                                                                                           | Contrats, ADR-0002                    | **Tranchée** : RPC, pas de REST ; gRPC proposé dans l'ADR-0003                                                                                                                                                                               |
| Q21 | Que réexpose le SDK de `Voyager.Core` aux plugins ? Le SDK peut-il exposer les abstractions `Microsoft.Extensions` (injection, journalisation) ? Le SDK suit-il ses propres tags `sdk/v…` dès l'étape 3 ?                                                                                              | ADR des plugins, tests d'architecture | Proposée dans l'ADR-0015 : abstractions Microsoft (injection, journaux) seulement ; versions `sdk/v…` dès l'étape 3                                                                                                                          |
| Q22 | Quel hébergeur pour le serveur en ligne (VPS en Europe, service de conteneurs managé…) ? Quel coût mensuel accepté ?                                                                                                                                                                                   | Mise en ligne, RGPD, coûts            | Ouverte (quand une version sera ouverte à un groupe)                                                                                                                                                                                         |
| Q23 | Quand le service local redémarre, l'interface remplace tous les écrans par un message (et perd une saisie en cours) : faut-il plutôt un bandeau au-dessus de l'écran, en gardant les écrans ouverts ?                                                                                                  | Saisie (E4), service local            | Ouverte (à traiter avec le premier formulaire de saisie)                                                                                                                                                                                     |
| Q24 | Un utilisateur de la version bêta reçoit-il la version stable ? Velopack lit un flux par canal : publier aussi les stables sur le canal bêta, ou laisser l'utilisateur changer de canal ?                                                                                                              | Mises à jour, ADR-0009, ADR-0013      | Ouverte (avant la première release publiée)                                                                                                                                                                                                  |
| Q25 | Comment l'interface de développement (servie par Vite) charge-t-elle les modules d'interface des plugins et partage-t-elle React avec eux, le chemin de référence étant voyager://app ?                                                                                                                | ADR-0015, développement               | **Tranchée** : on reconstruit le plugin à chaque modification, sans rechargement à chaud ; en développement, Vite sert les fichiers des plugins (`/plugins/<id>/…`) et les modules partagés comme l'application le fait avec `voyager://app` |
| Q26 | Un plugin qui fait planter le service local après son chargement le fait relancer en boucle par le superviseur : faut-il le désactiver après un plantage, et comment le montrer à l'utilisateur ?                                                                                                      | ADR-0015, service local               | **Tranchée dans le principe** : un plugin à l'origine d'un plantage est désactivé et signalé ; mécanisme à écrire avant des plugins de tiers                                                                                                 |
| Q27 | Un plugin peut-il dépendre d'un autre plugin ? Aujourd'hui non : il ne dépend que du SDK et chaque plugin est chargé seul, avec son propre conteneur. Si le besoin apparaît : déclaration dans le manifeste, vérification de version, ordre de chargement, comportement si le plugin requis est écarté | ADR-0015, plugins                     | **Ouverte** : à trancher par un ADR si le besoin apparaît ; d'ici là, une notion partagée entre plugins entre dans le cœur ou le SDK sous forme de contrat                                                                                   |

## Idées en vrac

- Import automatique des réservations depuis les mails de confirmation.
- Météo prévue sur les jours du programme.
- Statistiques de fin de voyage (distance, pays, dépenses).
- Alertes de prix sur une prestation du catalogue.
- Score de crédibilité des données de référence, en les comparant aux données des API.
