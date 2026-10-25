# ADR-0012 — Journaux de développement écrits dans Seq avec Serilog

- **Statut** : Acceptée
- **Date** : 2026-10-25
- **Remplace** : — (complète [ADR-0006](0006-dependances-principales.md), dont il élargit la ligne
  « Journalisation (écriture) », et [ADR-0011](0011-aspire-pour-le-developpement.md))

## Contexte

En développement, les journaux du service local et du serveur du groupe partent par OpenTelemetry vers
le tableau de bord Aspire ([ADR-0011](0011-aspire-pour-le-developpement.md)). Ce tableau de bord les
garde en mémoire : ils disparaissent à chaque arrêt d'Aspire, et la recherche y reste sommaire.

Le développeur a déjà un serveur [Seq](https://datalust.co/seq) sur son poste
(`http://localhost:5341`). Il veut y retrouver les journaux de Voyager, d'un lancement à l'autre, avec
les requêtes et les tableaux de Seq.

Les décisions déjà prises encadrent ce choix :

- [ADR-0006](0006-dependances-principales.md) : on journalise avec `ILogger<T>` partout, et **Serilog**
  écrit les journaux, dans les hôtes uniquement : fichiers tournants sur le poste, JSON sur le serveur.
  Ces écritures ne sont pas encore en place (backlog, « Paquet installable »).
- [ADR-0011](0011-aspire-pour-le-developpement.md) : `Voyager.ServiceDefaults`, partagé par les deux
  hôtes, apporte OpenTelemetry. **Rien ne quitte la machine hors développement** tant que l'export de
  la télémétrie en production n'est pas décidé (Q22).
- Le serveur du groupe sera hébergé ([ADR-0010](0010-hebergement-serveur-heberge-multi-groupes.md)) :
  OpenTelemetry reste la voie prévue pour l'observer en ligne, et ne doit pas être remplacé.
- Les journaux ne contiennent aucune donnée personnelle, même en développement.

## Options

### Option 0 — Statu quo : le tableau de bord Aspire seul

- Avantages : rien à ajouter ni à configurer.
- Inconvénients : les journaux disparaissent à chaque arrêt d'Aspire, et la recherche reste sommaire ;
  le besoin exprimé n'est pas couvert.

### Option A — Serilog écrit dans Seq, à côté d'OpenTelemetry

`Voyager.ServiceDefaults` ajoute un journal Serilog muni du puits `Serilog.Sinks.Seq`, comme fournisseur
supplémentaire de `Microsoft.Extensions.Logging`. La console et l'export OpenTelemetry vers Aspire
continuent tels quels.

- Avantages :
  - c'est Serilog, la bibliothèque d'écriture des journaux déjà retenue par l'ADR-0006 : le jour où
    elle écrira aussi les fichiers sur le poste et le JSON sur le serveur, ce sera le même outil ;
  - le format CLEF du puits Seq est son format natif : propriétés structurées, niveaux et modèles de
    messages arrivent tels quels ;
  - les filtres habituels (`Logging:LogLevel`) s'appliquent à Seq comme aux autres sorties ;
  - indépendant d'Aspire : un hôte lancé seul peut écrire dans Seq avec le seul réglage
    `Seq:ServerUrl`.
- Inconvénients :
  - quatre paquets de plus (`Serilog`, `Serilog.Extensions.Logging`, `Serilog.Sinks.Seq`, et
    `Serilog.Sinks.File` qu'il apporte), livrés avec les deux hôtes alors qu'ils n'y servent pas hors
    développement ; `Serilog.Sinks.File` servira de toute façon aux fichiers de l'ADR-0006 ;
  - Serilog entre dans `Voyager.ServiceDefaults`, qui n'est pas un hôte, même si seuls les hôtes le
    référencent ;
  - seuls les journaux vont à Seq, pas les traces ;
  - deux pièges, corrigés et couverts par des tests :
    - l'extension `AddSerilog()` ajoute sa propre règle de filtre, qui laissait passer tous les
      niveaux ;
    - les envois du puits passaient dans les traces HTTP d'OpenTelemetry ;
  - à l'arrêt, Serilog envoie ce qui reste en file : un Seq qui ne répond plus ralentit l'arrêt de
    quelques secondes.

### Option B — Un second exportateur OpenTelemetry vers Seq

Seq reçoit le protocole OTLP. On ajoute, en développement, un second exportateur OpenTelemetry vers
l'adresse OTLP de Seq, à côté de celui d'Aspire.

- Avantages :
  - aucune dépendance de plus : OpenTelemetry est déjà là ;
  - Seq reçoit aussi les traces, pas seulement les journaux ;
  - l'exportateur gère lui-même sa file bornée et ne trace pas ses propres envois.
- Inconvénients :
  - OpenTelemetry ne permet pas d'utiliser à la fois l'export commun (`UseOtlpExporter`) et un second
    exportateur par signal : il faut reconfigurer celui d'Aspire signal par signal ;
  - les journaux arrivent dans Seq au format OTLP : propriétés structurées et modèle du message
    passent, mais certains champs (niveau, exception) suivent les conventions OpenTelemetry plutôt que
    celles de Seq ;
  - Serilog reste à mettre en place à part pour les fichiers (ADR-0006) : deux chemins différents
    pour les journaux.

### Option C — Seq lancé par Aspire, en conteneur

L'AppHost démarre un conteneur Seq (`Aspire.Hosting.Seq`) et le relie aux hôtes, comme PostgreSQL.

- Avantages :
  - rien à installer : chaque développeur a son Seq au premier F5 ;
  - l'adresse et la clé sont passées par Aspire, sans réglage manuel.
- Inconvénients :
  - un second serveur Seq quand on en a déjà un sur le poste, avec ses propres données ;
  - Docker devient nécessaire pour les journaux, et pas seulement pour PostgreSQL ;
  - ne dit rien de la façon d'écrire : il faut tout de même choisir A ou B.

## Décision

**Option A**, choix de l'utilisateur : Serilog écrit dans Seq, et OpenTelemetry est conservé tel quel,
pour Aspire aujourd'hui et pour le serveur hébergé demain.

Règles :

1. **En développement seulement.** Hors de l'environnement Development, rien n'est écrit dans Seq,
   comme pour l'export OpenTelemetry (ADR-0011). L'application installée lance toujours son service
   local en Production, quel que soit l'environnement du poste.
2. **Désactivé par défaut.** Seq ne s'active que si `Seq:ServerUrl` est renseigné, dans les secrets
   utilisateur de `Voyager.AppHost` : rien dans le dépôt public. Une adresse mal écrite arrête le
   démarrage plutôt que d'échouer en silence.
3. **Clé d'API en secret.** `Seq:ApiKey`, si le serveur en demande une, est un paramètre secret
   d'Aspire, masqué dans le tableau de bord.
4. **Mêmes filtres que les autres sorties** (`Logging:LogLevel`). La file d'attente est bornée
   (10 000 journaux) quand Seq ne répond pas, une connexion qui n'aboutit pas abandonne au bout de
   5 secondes, et Serilog signale ses échecs sur la sortie d'erreur.
5. **Seq sur le poste, ou en HTTPS.** Les journaux et la clé ne circulent jamais en clair sur le réseau :
   une adresse `http` vers une autre machine arrête le démarrage.
6. **L'application installée est isolée.** Elle ne transmet à son service local que les variables
   système du poste (chemins, dossier temporaire…), jamais une variable qui pourrait le configurer.

## Conséquences

- Positif :
  - les journaux de développement restent consultables d'un lancement à l'autre, avec les outils de
    recherche de Seq ;
  - Serilog est en place : les fichiers sur le poste et le JSON sur le serveur (ADR-0006) s'y
    ajouteront.
- Négatif :
  - les paquets Seq sont livrés avec l'application et l'image Docker sans y servir ;
  - Seq garde les journaux sur disque, contrairement au tableau de bord Aspire. Le jour où la base de
    développement contiendra de vraies données, aucun journal ne devra en écrire, ce que la règle
    « aucune donnée personnelle dans les journaux » couvre déjà.
- La règle « seuls les hôtes connaissent Serilog » devient « seuls les hôtes et
  `Voyager.ServiceDefaults` », qui n'est référencé que par eux.
- À faire ensuite :
  - quand les fichiers de journaux arriveront, une seule configuration Serilog par hôte, dont le puits
    Seq ne sera qu'une sortie de plus ;
  - l'envoi des journaux du serveur hébergé vers un Seq, ou vers autre chose, se décidera avec
    l'export de la télémétrie en production (Q22) ;
  - si Seq sert un jour aussi pour les traces, revoir l'option B.
