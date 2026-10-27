# Voyager

Préparer un voyage de groupe avec un budget juste : Voyager réunit les envies, les devis jour par
jour, les réservations, la répartition des coûts entre voyageurs et, plus tard, le carnet de voyage.

> Projet en construction : les fondations techniques sont posées et un premier écran « À propos »
> traverse toute l'application ; pas encore de fonctionnalité.

## Documentation

- [Vision produit](docs/vision.md)
- [Backlog et questions ouvertes](docs/backlog.md)
- [Glossaire](docs/glossaire.md)
- [Schéma fonctionnel](docs/schemas/fonctionnel.md) et [schéma technique](docs/schemas/technique.md)
- [Conventions de code](docs/conventions/README.md)
- [Décisions d'architecture (ADR)](docs/adr/README.md)
- [Spécifications fonctionnelles](docs/specs/README.md)

## Développer

Prérequis : [.NET 10 SDK](https://dotnet.microsoft.com/download), Node.js 22 ou plus récent (voir
`.nvmrc`), et pnpm fourni par Corepack (`corepack enable pnpm`). Docker servira pour PostgreSQL.

```bash
pnpm install
```

```bash
dotnet build Voyager.slnx
```

```bash
dotnet test --solution Voyager.slnx
```

```bash
pnpm lint
```

Lancer tout ce qu'il faut pour développer avec [Aspire](docs/adr/0011-aspire-pour-le-developpement.md) :
PostgreSQL + PostGIS (Docker doit tourner), le serveur du groupe (`http://localhost:5180`), le
service local, l'interface servie par Vite, la fenêtre Electron qui l'affiche et le tableau de bord.
Lancez d'abord `pnpm install` une fois. Dans Visual Studio : `Voyager.AppHost` comme projet de
démarrage, puis F5. En ligne de commande :

```bash
dotnet run --project src/dotnet/Voyager.AppHost
```

Le tableau de bord s'ouvre sur `https://localhost:17180`, sans connexion (il n'écoute que sur votre
machine). La première fois, faites confiance au certificat HTTPS de développement de .NET, sans quoi
le navigateur refuse la page :

```bash
dotnet dev-certs https --trust
```

La base garde ses données d'un
lancement à l'autre (volume Docker `voyager-postgres-data`) ; son mot de passe est conservé dans vos
secrets utilisateur. Si la base refuse un jour son mot de passe (« password authentication failed »),
supprimez ce volume et le conteneur `postgres-…` dans Docker Desktop, puis relancez.

Si vous avez un serveur [Seq](https://datalust.co/seq), le service local et le serveur du groupe y
écrivent aussi leurs journaux en développement
([ADR-0012](docs/adr/0012-journaux-de-developpement-dans-seq-avec-serilog.md)). Activez-le dans les
secrets utilisateur de `Voyager.AppHost`, hors du dépôt :

```bash
dotnet user-secrets set "Seq:ServerUrl" "http://localhost:5341" --project src/dotnet/Voyager.AppHost
```

Si votre Seq demande une clé d'API, ajoutez-la de la même façon sous `Seq:ApiKey`. Dans Seq, filtrez
avec `Application = 'Voyager.LocalService'` ou `Application = 'Voyager.GroupServer'`. Utilisez un Seq
installé sur votre poste, ou joignable en HTTPS ; sous Docker, publiez son port sur `127.0.0.1`
seulement (`-p 127.0.0.1:5341:80`). Seq garde les journaux sur disque. L'application installée n'écrit
jamais dans Seq.

### Les plugins en développement

Les deux plugins de démonstration (`plugins/diagnostics-runtime` et `plugins/diagnostics-clock`) sont
construits dans `artifacts/plugins/` par `tools/build-plugins.ps1` : Aspire le lance avant de démarrer le
service local, qui les charge, et l'interface servie par Vite lit leurs modules dans le même dossier. Pour les
voir, ouvrez la page « À propos » : sous le service, la liste des plugins et leurs faits, et le panneau de
l'horloge avec son bouton « Actualiser ». Un plugin modifié se reconstruit en relançant Aspire (pas de
rechargement à chaud) : seuls les plugins dont les sources ont changé sont reconstruits, la version est fixe
(`0.0.0-dev`) et la construction est en Debug (on peut y poser des points d'arrêt), donc un lancement sans
changement ne perd qu'une seconde ou deux ; le paquet installable, lui, est construit en Release. Le service local garde les assemblies
chargées : pour reconstruire un plugin modifié, arrêtez-le d'abord. Sans Aspire :

```powershell
pwsh tools/build-plugins.ps1
```

Chaque plugin a sa propre version, calculée de son historique : `pwsh tools/get-version.ps1 -All` donne celle
de l'application, du SDK et de chaque plugin. Pour créer un plugin, demandez `/new-plugin` à Claude.

Les autres commandes (formatage, contrats, version) sont listées dans [CLAUDE.md](CLAUDE.md#commandes).

## Construire le paquet installable

Sous Windows, avec .NET, Node.js et pnpm installés :

```powershell
pwsh tools/release.ps1
```

Le script calcule la version avec GitVersion, publie le service local, construit l'interface et
l'application de bureau, puis fabrique avec Velopack l'installateur et les paquets de mise à jour dans
`artifacts/release/`. Rien n'est publié. L'application installée écrit ses journaux (la sienne et celle
du service local) dans `%LOCALAPPDATA%\Voyager\logs`, sans nom de compte (le dossier personnel y est remplacé par `~`) ni donnée de voyage. Au
démarrage, l'application installée interroge son serveur de mises à jour (par défaut les releases
GitHub du dépôt) pour chercher une version plus récente : seules l'adresse IP et la version courante
sont visibles de ce serveur.

### Essayer l'installation et la mise à jour

Sans rien publier sur GitHub, avec un serveur web qui sert un dossier en `https` (ou en `http` vers
votre propre machine). L'adresse n'est écrite que dans le paquet, jamais dans le dépôt, et ne doit contenir ni identifiant ni
paramètre. Un paquet fabriqué pour `http://localhost` est un paquet d'essai : il ne se distribue pas :

```powershell
pwsh tools/release.ps1 -Version 0.1.0-alpha.1 -UpdateSource https://votre-serveur/voyager/
```

1. Copiez le contenu de `artifacts/release/` dans le dossier servi, puis installez
   `VoyagerApp-alpha-Setup.exe` : l'application s'installe dans `%LOCALAPPDATA%\VoyagerApp`.
2. Fabriquez une version plus récente avec la même adresse (`-Version 0.1.0-alpha.2`) et copiez de
   nouveau `artifacts/release/` dans le dossier servi.
3. Lancez l'application installée : la petite fenêtre cherche, télécharge avec une barre de progression,
   installe, et Voyager redémarre dans la `0.1.0-alpha.2` (le numéro est dans la page « À propos »).

Pour désinstaller : « Applications installées » de Windows. Vos données restent dans
`%LOCALAPPDATA%\Voyager`.

## Contribuer

Git Flow : le travail part de `develop` sur une branche `feature/…` et y revient par une PR
([ADR-0007](docs/adr/0007-git-flow-et-versions-gitversion.md)). Conventions de travail : voir
[CLAUDE.md](CLAUDE.md), qui sert aussi d'instructions à Claude Code.

## Licence

[MIT](LICENSE)
