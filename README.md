# Voyager

Préparer un voyage de groupe avec un budget juste : Voyager réunit les envies, les devis jour par
jour, les réservations, la répartition des coûts entre voyageurs et, plus tard, le carnet de voyage.

> Projet en construction : les fondations techniques sont posées, pas encore de fonctionnalité.

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
service local et le tableau de bord. Dans Visual Studio : `Voyager.AppHost` comme projet de
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

Les autres commandes (formatage, contrats, version) sont listées dans [CLAUDE.md](CLAUDE.md#commandes).

## Contribuer

Git Flow : le travail part de `develop` sur une branche `feature/…` et y revient par une PR
([ADR-0007](docs/adr/0007-git-flow-et-versions-gitversion.md)). Conventions de travail : voir
[CLAUDE.md](CLAUDE.md), qui sert aussi d'instructions à Claude Code.

## Licence

[MIT](LICENSE)
