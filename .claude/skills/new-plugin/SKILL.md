---
name: new-plugin
description: Crée le squelette d'un nouveau plugin de Voyager dans plugins/<id>/ (ADR-0015) — manifeste, partie .NET, partie d'interface si besoin, textes français et anglais, tests — l'ajoute à la solution et vérifie qu'il se construit, se charge et passe les tests d'architecture. À utiliser quand l'utilisateur veut ajouter un plugin, ou quand une fonctionnalité d'une spec doit être livrée sous forme de plugin.
---

# Nouveau plugin

Plugin demandé (identifiant, ce qu'il apporte, avec ou sans écran) : $ARGUMENTS

Un plugin est un dossier de `plugins/` : une partie .NET chargée par le service local, et en option une
partie d'interface (un module React) chargée par la page. Lis d'abord l'
[ADR-0015](../../../docs/adr/0015-mecanisme-de-plugins-decouverte-chargement-et-preuve.md), la section
« Les plugins » de l'[organisation du code](../../../docs/conventions/organisation-du-code.md) et la spec
[preuve des plugins](../../../docs/specs/plateforme-preuve-des-plugins.md).

**Les modèles sont les deux plugins de démonstration**, qui compilent et sont testés à chaque PR : ils ne
peuvent pas vieillir comme le ferait un modèle de fichiers. `diagnostics-runtime` est le plus petit (partie
.NET seule) ; `diagnostics-clock` y ajoute un panneau React. Copie-les, ne les réinvente pas.

## 1. Cadrer

1. **Identifiant** : anglais, minuscules, chiffres et tirets (`^[a-z][a-z0-9]*(-[a-z0-9]+)*$`), qui nomme ce
   que le plugin apporte (`lodging`, pas `my-plugin`). `service` est réservé à l'hôte. Il sert de nom de
   dossier, d'identifiant dans le manifeste et de préfixe à tous ses textes et libellés de faits.
2. **Ce qu'il apporte** : un point d'extension qui **existe** (`ExtensionPoints` dans `Voyager.Sdk` ; pour
   l'instant seul `diagnostic-fact-source`). Si le besoin demande un point d'extension qui n'existe pas, arrête-toi :
   c'est un ADR (`/new-adr`) et pas de point d'extension sans deux implémentations connues ou prévues
   ([ADR-0002](../../../docs/adr/0002-architecture-modulaire-par-contrats-et-plugins.md)). Pas de service gRPC
   propre au plugin : hors périmètre de la preuve, donc pas de modèle de `.proto`.
3. **Avec ou sans écran** : une partie d'interface n'est utile que si la liste standard ne suffit pas.
4. Une fonctionnalité qui a une spec doit avoir sa spec « Validée » ; sinon `/new-spec` d'abord.
5. Si un terme métier nouveau apparaît, ajoute-le au [glossaire](../../../docs/glossaire.md) (FR ↔ nom de code).

## 2. Préparer

- `git switch develop`, `git pull`, puis `git switch -c feature/plugin-<id>` (ou continue la branche de la
  fonctionnalité si le plugin en fait partie).
- Lis les fichiers du plugin modèle que tu vas copier : `plugins/diagnostics-runtime/` (ou `diagnostics-clock/`
  avec écran).

## 3. Créer le plugin

Remplace `<id>` par l'identifiant, `<Name>` par la forme PascalCase (`diagnostics-clock` → `DiagnosticsClock`).

| Fichier                                                                    | Contenu                                                                                     | Modèle                                      |
| -------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- | ------------------------------------------- |
| `plugins/<id>/plugin.json`                                                 | `id`, `sdk`, `dotnet.assembly`, `ui.module` (si écran), `provides`. **Jamais de `version`** | `diagnostics-runtime` / `diagnostics-clock` |
| `plugins/<id>/dotnet/Voyager.Plugins.<Name>/Voyager.Plugins.<Name>.csproj` | `AssemblyName` et `RootNamespace` ; une seule référence : le SDK, avec `Private="false"`    | idem                                        |
| `plugins/<id>/dotnet/Voyager.Plugins.<Name>/<Name>Plugin.cs`               | `IPlugin` : `Configure` enregistre ses services et ses sources                              | idem                                        |
| `plugins/<id>/dotnet/Voyager.Plugins.<Name>/<Source>.cs`                   | La source de faits (`IDiagnosticFactSource`), libellés préfixés `<id>-`                     | `RuntimeFactSource`, `ClockFactSource`      |
| `plugins/<id>/tests/Voyager.Plugins.<Name>.Tests/…Tests.csproj`            | Projet MSTest qui référence la partie .NET                                                  | `diagnostics-clock`                         |
| `plugins/<id>/tests/Voyager.Plugins.<Name>.Tests/<Source>Tests.cs`         | Au moins un test du comportement, avec `FakeClock` si l'heure compte, données fictives      | `ClockFactSourceTests`                      |
| `plugins/<id>/i18n/fr/messages.ftl` et `i18n/en/messages.ftl`              | Mêmes identifiants dans les deux langues, **tous préfixés `<id>-`**, aucun terme (`-nom`)   | `diagnostics-clock/i18n`                    |

Avec écran, ajoute (modèle : `plugins/diagnostics-clock/ui/`) :

| Fichier                                    | Contenu                                                                                           |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------- |
| `plugins/<id>/ui/package.json`             | `@voyager/plugin-<id>`, `private`, scripts `build` et `typecheck`, dépendances de développement   |
| `plugins/<id>/ui/tsconfig.json`            | Étend `tsconfig.base.json`                                                                        |
| `plugins/<id>/ui/vite.config.ts`           | Construction en bibliothèque ; React, Fluent UI et le SDK sont **externes** (fournis par la page) |
| `plugins/<id>/ui/src/index.ts`             | `register(registry)` : déclare le panneau avec `registry.addDiagnosticFactPanel`                  |
| `plugins/<id>/ui/src/<Name>Panel.tsx`      | Le composant ; textes par `useLocalization`, valeurs par `formatFactValue`                        |
| `plugins/<id>/ui/src/<Name>Panel.test.tsx` | Test du composant avec les textes anglais                                                         |

Règles que les tests et le lint font respecter (ne les contourne pas) :

- **.NET** : le plugin ne dépend que de `Voyager.Sdk`, des types partagés de `Voyager.Core`, de NodaTime et des
  deux abstractions Microsoft (injection, journaux). `PluginAssemblyTests` le vérifie sur les assemblies.
- **Interface** : le module n'importe que `@voyager/plugin-sdk`, React, Fluent UI et `@fluent/react` ; ESLint le
  vérifie. Aucun texte en dur, aucun calcul métier.
- **Textes** : mêmes identifiants en français et en anglais, tous préfixés par l'identifiant du plugin ; un test
  (`pluginTranslations.test.ts`) le vérifie pour chaque plugin du dépôt.
- **Faits** : 50 au plus, fournis en 5 secondes au plus ; une exception ou un dépassement n'écarte que ce plugin.
- **Version** : jamais écrite à la main. Celle du plugin vient de son propre historique Git
  (`pwsh tools/get-version.ps1 -Component <id>`) et est inscrite dans le manifeste à la construction.
- **Champ `sdk`** : `major.minor` de la version actuelle du SDK, que donne
  `pwsh tools/get-version.ps1 -Component sdk -Variable MajorMinorPatch` (`0.1.0` → `"0.1"`). En `0.x`, un
  plugin écrit pour une autre version mineure est refusé au démarrage.

## 4. Brancher

1. **Solution** : ajoute le plugin à `Voyager.slnx`, dans un dossier `/plugins/<id>/` pour ses projets et son
   manifeste, et dans `/plugins/<id>/i18n/`, `/plugins/<id>/ui/`, `/plugins/<id>/ui/src/` pour ses fichiers
   (même forme que `diagnostics-clock`). La solution liste les fichiers un par un.
2. **Interface** : `pnpm install` (le dossier `plugins/*/ui` est déjà dans l'espace de travail pnpm).
3. Rien d'autre : l'application, Aspire (`tools/build-plugins.ps1`) et `tools/release.ps1` prennent tous les
   dossiers de `plugins/` qui ont un `plugin.json`.

## 5. Vérifier

1. `dotnet build Voyager.slnx` sans avertissement, puis `dotnet test --solution Voyager.slnx` (dont les tests
   d'architecture et le chargement des vrais plugins).
2. Avec écran : `pnpm lint`, `pnpm typecheck`, `pnpm test`, `pnpm format:check`.
3. `pwsh tools/build-plugins.ps1 -Version 0.0.0-dev -Configuration Debug` construit le plugin dans
   `artifacts/plugins/<id>/` (manifeste avec sa version, assembly sans les assemblies partagées, `ui/`, `i18n/`).
   Si Windows refuse de remplacer une DLL, un service local tourne encore : arrête-le.
4. Lance l'application (`Voyager.AppHost`) ou le service et la page : le plugin doit apparaître dans la page
   « À propos » avec son état « chargé » et ses faits.
5. `dotnet format Voyager.slnx` et `npx markdownlint-cli2` si de la documentation a changé.
6. Applique la [definition of done](../../rules/definition-of-done.md) : relecteurs `architecture-reviewer`
   et `code-reviewer`, puis `code-polish`.
7. Le plugin n'a pas de version tant qu'il n'est pas committé : GitVersion la calcule à partir de ses commits.
   Livre avec `/ship`.
