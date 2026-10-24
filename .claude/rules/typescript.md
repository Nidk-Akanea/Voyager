---
paths:
  - "src/ts/**"
  - "plugins/*/ui/**"
  - "**/*.ts"
  - "**/*.tsx"
---

# Code TypeScript et React

Référence complète : [conventions TypeScript et React](../../docs/conventions/typescript.md).
L'essentiel :

- **Aucun calcul métier dans l'interface.** Montants (`google.type.Money`) et dates sont **formatés**,
  jamais convertis en `number` ni additionnés. Si un écran a besoin d'un total, il vient du service
  local.
- TypeScript strict ; pas de `any` (utilise `unknown`) ; pas de `!` sans commentaire ; exports nommés ;
  `import type`.
- Composants fonctionnels ; un composant par fichier, en PascalCase ; hooks `useXxx`.
- Données du service local via TanStack Query au-dessus du client Connect-ES généré ; jamais d'appel
  gRPC direct dans un composant.
- Fluent UI React d'abord ; composant maison seulement sans équivalent.
- **Aucun texte en dur** : tout passe par i18next (`i18n/fr.json`), clés en anglais.
- Constantes de module en `UPPER_SNAKE_CASE`.
- Electron : `contextIsolation`, `sandbox`, pas de `nodeIntegration` ; le `preload` expose une API
  minimale et typée ; le processus principal valide tout ce qui vient de l'interface.
- Un plugin n'importe que `@voyager/plugin-sdk` (et React fourni par l'hôte).
- ESLint `--max-warnings 0` et Prettier doivent passer.
