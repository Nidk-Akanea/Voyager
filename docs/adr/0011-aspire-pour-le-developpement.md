# ADR-0011 — Aspire pour le développement

- **Statut** : Acceptée
- **Date** : 2026-10-25
- **Remplace** : —
- **Complète** : l'[ADR-0006](0006-dependances-principales.md) (ajout d'Aspire ; OpenTelemetry dès
  maintenant, sur les deux hôtes)

## Contexte

Pour développer Voyager, il faut faire tourner ensemble plusieurs éléments : PostgreSQL avec PostGIS
(dans Docker), le serveur du groupe, le service local, et bientôt l'interface et Electron. Jusqu'ici,
un fichier `deploy/compose.dev.yaml` lançait la base et le serveur, et le reste se démarrait à la main.

L'utilisateur propose **Aspire** (anciennement .NET Aspire), l'outil de Microsoft qui orchestre une
application distribuée en développement et en montre l'état dans un tableau de bord.

## Options

### Option A — Docker Compose et lancements à la main

- Avantages : aucune dépendance de plus ; proche de ce qu'on déploie.
- Inconvénients : plusieurs commandes à lancer ; les adresses et chaînes de connexion se recopient à
  la main ; aucune vue d'ensemble des journaux ni des traces.

### Option B — Aspire

Un projet `Voyager.AppHost` décrit en C# tout ce qui doit tourner ; un projet `Voyager.ServiceDefaults`
donne aux deux hôtes les mêmes réglages techniques (télémétrie, santé, résilience).

- Avantages :
  - **un seul F5** (ou `dotnet run`) démarre la base, les serveurs et, plus tard, l'interface ;
  - les connexions entre services (chaîne de connexion de la base…) sont transmises automatiquement ;
  - un **tableau de bord** : journaux centralisés, traces de bout en bout, métriques, état de santé ;
  - OpenTelemetry, prévu « plus tard » par l'ADR-0006, est en place dès le départ, sur les deux hôtes.
- Inconvénients :
  - deux projets et quelques dépendances de plus ;
  - en développement, c'est Aspire qui lance le service local, alors que chez l'utilisateur ce sera
    Electron : le démarrage réel doit être testé à part ;
  - Docker reste nécessaire pour PostgreSQL.

## Décision

**Option B, choisie par l'utilisateur.**

1. **`Voyager.AppHost`** (SDK `Aspire.AppHost.Sdk`) orchestre en développement :
   - PostgreSQL 18 avec PostGIS (image `postgis/postgis`) et sa base `voyager`, avec un volume de
     données persistant ;
   - le serveur du groupe, relié à la base ;
   - le service local ;
   - plus tard, l'interface React et Electron (`Aspire.Hosting.JavaScript`).
2. **`Voyager.ServiceDefaults`** est référencé par les deux hôtes. Il apporte :
   - la validation du conteneur d'injection au démarrage (ADR-0006) ;
   - OpenTelemetry (journaux, traces, métriques), exporté vers le tableau de bord **en développement
     seulement** ;
   - les points de santé `/health` et `/alive`, exposés en développement uniquement ;
   - la résilience des appels HTTP sortants (Polly, ADR-0006) et la découverte de services.
3. **Aspire ne sert qu'au développement.** Ce qu'on livre ne change pas : l'image Docker du serveur
   (ADR-0010) et l'application de bureau (ADR-0009, si elle est acceptée). `deploy/compose.dev.yaml`
   est supprimé ; `deploy/GroupServer.Dockerfile` reste.
4. **Adresses d'écoute** : sans configuration explicite, le service local écoute en boucle locale sur
   un port libre et le serveur du groupe sur `localhost:5180` ; Aspire, Docker ou Electron peuvent
   imposer d'autres adresses.
5. **Dépendances ajoutées** à la liste de l'ADR-0006 : `Aspire.AppHost.Sdk`,
   `Aspire.Hosting.PostgreSQL`, OpenTelemetry (`OpenTelemetry.Extensions.Hosting`, exportateur OTLP,
   instrumentations ASP.NET Core, HTTP et runtime), `Microsoft.Extensions.Http.Resilience`,
   `Microsoft.Extensions.ServiceDiscovery`. Aucune n'entre dans `Voyager.Core` ni `Voyager.Sdk`.

## Conséquences

- Pour développer : `Voyager.AppHost` comme projet de démarrage, F5 ; le tableau de bord s'ouvre dans
  le navigateur.
- Les tests d'architecture déclarent les deux nouveaux projets : `Voyager.AppHost` ne référence que les deux
  hôtes et **aucun projet ne le référence** (il ne doit jamais être livré) ; `Voyager.ServiceDefaults`
  ne dépend d'aucun projet de Voyager et n'utilise que des paquets techniques listés.
- La validation du conteneur d'injection (ADR-0006) est réglée une fois, dans `Voyager.ServiceDefaults`.
- Les points de santé n'existent qu'en développement : la mise en ligne (Q22) devra prévoir des
  sondes de santé pour l'image Docker.
- Les journaux et traces doivent rester sans donnée personnelle, même dans le tableau de bord.
- Pour la production, l'export de la télémétrie (vers quel service, avec quelle rétention) sera décidé
  avec la mise en ligne (Q22).
