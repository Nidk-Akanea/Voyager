# Conventions de code

Règles communes à tout le code de Voyager. Elles s'appliquent à l'utilisateur comme à Claude, et les
outils (analyseurs, lint, formatage, tests d'architecture) les vérifient automatiquement là où c'est
possible.

| Document                                        | Contenu                                                                    |
| ----------------------------------------------- | -------------------------------------------------------------------------- |
| [Organisation du code](organisation-du-code.md) | Rangement du dépôt, projets .NET, paquets TypeScript, plugins, tests       |
| [C#](csharp.md)                                 | Rigueur, nommage, style, types métier, erreurs, journalisation             |
| [TypeScript et React](typescript.md)            | Outils, règle « pas de calcul dans l'interface », nommage, React, Electron |
| [gRPC et Protocol Buffers](grpc.md)             | Contrats, nommage, types communs, compatibilité, erreurs                   |
| [Tests](tests.md)                               | Outils, niveaux de tests, nommage, données fictives                        |

Les conventions Git (commits, branches, PR) sont dans [CLAUDE.md](../../CLAUDE.md#git).

## Principes communs

1. **Langues** : code, identifiants, commentaires et commits en anglais ; documentation en français.
2. **Les noms viennent du [glossaire](../glossaire.md).**
3. **Le métier est en C#**, dans `Voyager.Core` ; l'interface affiche et saisit.
4. **Strict** : un avertissement est une erreur, partout.
5. **Exact** : argent en décimal avec devise, dates de calendrier sans heure, instants en UTC + fuseau.
6. **Discret** : aucune donnée personnelle dans les journaux, les tests ou les exemples.
7. **Simple d'abord** : pas d'abstraction ni de point d'extension sans au moins deux usages connus.
