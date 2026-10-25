---
name: new-use-case
description: Crée un cas d'usage dans un domaine de Voyager.Core (Trips, Catalog, Quotes, Bookings, Budget) selon l'architecture hexagonale — commande, traitement, ports nécessaires, enregistrement dans l'injection, et ses tests via test-writer. À utiliser quand il faut ajouter une opération métier au cœur, par exemple dans le cadre de /feature.
---

# Nouveau cas d'usage

Domaine et opération demandés : $ARGUMENTS

1. Lis la spec concernée dans `docs/specs/` (elle doit être « Validée »), le
   [glossaire](../../../docs/glossaire.md) et l'[organisation du code](../../../docs/conventions/organisation-du-code.md).
   Nomme l'opération avec les termes du glossaire : verbe + nom, en anglais (`CreateQuote`).
2. Vérifie le sens des dépendances entre domaines : le cas d'usage ne peut utiliser que son domaine
   et ceux dont il a le droit de dépendre. Sinon, déclare un port.
3. Crée `src/dotnet/Voyager.Core/<Domaine>/UseCases/<Operation>.cs` :
   - un `record` pour la commande (ou la requête), avec des types métier (`Money`, NodaTime,
     identifiants typés) ;
   - une classe `sealed` qui reçoit ses ports et `IClock` par son constructeur principal et expose
     une méthode qui renvoie un `Result` (asynchrone seulement si un port l'est) ;
   - documentation XML sur les types publics.
4. Crée les ports manquants dans le domaine (`I<Nom>Repository`…), sans implémentation : elle
   viendra dans un adaptateur.
5. Enregistre le cas d'usage **dans les hôtes qui l'utilisent** (`Voyager.LocalService`,
   `Voyager.GroupServer`), dans leur extension d'injection du domaine
   (`<Domaine>ServiceCollectionExtensions.cs`, créée si besoin). **Jamais dans `Voyager.Core`** :
   le cœur ne dépend pas de `Microsoft.Extensions` (ADR-0005, 0006), et un test d'architecture le
   vérifie.
6. Délègue les tests à l'agent `test-writer` (projet `tests/Voyager.Core.Tests/`).
7. `dotnet build Voyager.slnx` (aucun avertissement) et `dotnet test --solution Voyager.slnx`.
8. Si un terme métier nouveau est apparu, ajoute-le au glossaire.
