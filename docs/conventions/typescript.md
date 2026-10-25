# Conventions TypeScript et React

## Outils

| Outil                        | Réglage                                                                                                                                                                                               |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Node.js                      | version LTS, fixée dans `package.json` (`engines`) et `.nvmrc`                                                                                                                                        |
| Gestionnaire de paquets      | **pnpm 10**, version fixée dans `package.json` (`packageManager`) et fournie par Corepack ; pnpm refuse toute version publiée depuis moins de 14 jours (`minimumReleaseAge`)                          |
| TypeScript                   | `strict: true`, `noUncheckedIndexedAccess: true`, `noImplicitOverride: true`                                                                                                                          |
| Lint                         | ESLint + `typescript-eslint` (configurations `strict-type-checked` et `stylistic-type-checked`), `eslint-plugin-react-hooks` (ajouté avec React, à l'étape 2 du squelette) ; `--max-warnings 0` en CI |
| Formatage                    | Prettier, vérifié en CI (`prettier --check`)                                                                                                                                                          |
| Tests                        | Vitest + Testing Library                                                                                                                                                                              |
| Build                        | Vite                                                                                                                                                                                                  |
| Installation et mises à jour | Velopack (ADR-0009) ; Electron construit et lance l'application                                                                                                                                       |

## La règle d'or

**Aucun calcul métier dans l'interface.** L'interface affiche ce que renvoie le service local et lui
envoie ce que saisit l'utilisateur. Un total, une part, une faisabilité : c'est calculé en C#.

- Les montants arrivent en `google.type.Money` (unités + nanos) : on les **formate** avec
  `Intl.NumberFormat`, on ne les convertit jamais en `number` pour calculer.
- Les dates arrivent en `google.type.Date` ou `Timestamp` + fuseau : on les formate, on ne fait pas
  d'arithmétique de dates.

## Nommage et fichiers

| Élément                              | Règle                                | Exemple                           |
| ------------------------------------ | ------------------------------------ | --------------------------------- |
| Composants React                     | PascalCase, un composant par fichier | `QuoteCard.tsx`                   |
| Hooks                                | `use` + PascalCase                   | `useQuotes.ts`                    |
| Autres fichiers                      | camelCase                            | `formatMoney.ts`                  |
| Types et interfaces                  | PascalCase, sans préfixe `I`         | `QuoteSummary`                    |
| Variables, fonctions                 | camelCase                            | `selectedQuote`                   |
| Constantes de module (valeurs fixes) | MAJUSCULES séparées par `_`          | `DEFAULT_LOCALE`, `MAX_TRAVELERS` |
| Tests                                | à côté du fichier, suffixe `.test`   | `QuoteCard.test.tsx`              |

Le nommage est vérifié par la règle ESLint `@typescript-eslint/naming-convention`.

## TypeScript

- Pas de `any`. Si un type est vraiment inconnu : `unknown`, puis on le restreint.
- Pas de `!` (assertion non nulle) sauf justification en commentaire.
- `type` pour les formes de données, `interface` pour les contrats qu'un plugin implémente.
- Exports nommés uniquement (pas de `export default`), pour que les renommages restent sûrs.
- Imports de types avec `import type`.

## React

- Composants fonctionnels et hooks uniquement.
- **Données venant du service local** : TanStack Query au-dessus du client gRPC généré (cache,
  rechargement, états de chargement). Pas d'appel gRPC directement dans un composant.
- **État d'interface local** (onglet ouvert, filtre) : `useState` dans le composant. Un état global
  ne s'ajoute que s'il est vraiment partagé, et par décision explicite.
- Composants **Fluent UI React** en priorité ; un composant maison seulement si Fluent n'a pas
  d'équivalent.
- Accessibilité : chaque contrôle a un libellé ; navigation au clavier possible partout.

## Textes de l'interface

- L'interface est en **français d'abord**, mais **aucun texte en dur** dans les composants : tout
  passe par des messages Fluent ([ADR-0008](../adr/0008-globalisation-fluent-et-linguini.md)) :
  `@fluent/react`, fichiers `src/i18n/<langue>/<zone>.ftl`, en **français et en anglais**.
- Les clés sont en anglais et décrivent le sens : `quotes.compare.title`.

## Electron

- `contextIsolation: true`, `sandbox: true`, `nodeIntegration: false` pour toutes les fenêtres.
- Le script `preload` expose une API minimale et typée (`contextBridge`) ; l'interface n'accède
  jamais directement à Node.js ni au système de fichiers.
- Le processus principal valide tout ce qui arrive de l'interface.

## Plugins (côté interface)

- Un plugin importe **uniquement** `@voyager/plugin-sdk` (et React, fourni par l'hôte).
- Il déclare ce qu'il fournit auprès du registre ; il ne modifie jamais l'interface de l'hôte
  autrement.
- Ses textes passent par sa propre clé de traduction (`plugins.lodging.…`).
