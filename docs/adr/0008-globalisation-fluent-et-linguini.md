# ADR-0008 — Globalisation : Fluent, avec Linguini côté .NET

- **Statut** : Acceptée
- **Date** : 2026-10-24
- **Remplace** : la ligne « Traductions : i18next + react-i18next » de l'[ADR-0006](0006-dependances-principales.md)

## Contexte

Voyager doit être disponible **en français et en anglais** dès le départ, et pouvoir accueillir
d'autres langues plus tard. L'utilisateur demande d'utiliser **Linguini**.

Linguini est l'implémentation C# de **Project Fluent**, le système de traduction de Mozilla (Firefox).
Fluent écrit les textes dans des fichiers `.ftl` et gère proprement ce que les systèmes simples gèrent
mal : pluriels, genres, nombres, dates, et phrases dont la structure change d'une langue à l'autre.

Des textes sont produits à deux endroits :

- **l'interface React** : la grande majorité des libellés ;
- **le code .NET** : messages d'erreur métier présentés à l'utilisateur, notifications, exports
  (programme en PDF, notes de version), textes produits par les plugins.

L'ADR-0006 avait retenu i18next pour l'interface : avec Fluent côté .NET, on aurait deux formats de
traduction différents.

## Options

### Option A — i18next côté interface, Linguini côté .NET

- Avantages : i18next est très répandu côté React.
- Inconvénients : deux formats (JSON i18next et `.ftl`) ; les traductions ne sont pas partagées entre
  .NET et l'interface ; les règles de pluriel diffèrent d'un côté à l'autre.

### Option B — Fluent partout : Linguini côté .NET, `@fluent/bundle` et `@fluent/react` côté interface

- Avantages : **un seul format** (`.ftl`) et une seule façon de penser les traductions ; un message
  défini une fois peut servir des deux côtés ; Fluent est maintenu par Mozilla pour JavaScript.
- Inconvénients : écosystème plus petit qu'i18next ; Linguini est maintenu par une petite équipe
  (version 0.x).

## Décision

**Option B : Fluent partout.**

1. **Langues** : français (`fr`) et anglais (`en`). Le français est la langue de référence : un texte
   est d'abord écrit en français, puis traduit. Si une traduction manque, on affiche le français.
2. **Fichiers** : `.ftl` en UTF-8, rangés par langue puis par zone :
   - interface : `src/ts/ui/src/i18n/<langue>/<zone>.ftl` ;
   - .NET : dans le projet qui produit le texte, `Resources/i18n/<langue>/<zone>.ftl`, embarqués
     dans l'assembly ;
   - plugin : `plugins/<id>/i18n/<langue>/*.ftl`, avec des identifiants préfixés par l'identifiant du
     plugin.
3. **Identifiants de message** en anglais, en kebab-case et préfixés par la zone :
   `quotes-compare-title`, `budget-share-of = Part de { $traveler }`.
4. **Bibliothèques** : `Linguini.Bundle` côté .NET, `@fluent/bundle` et `@fluent/react` côté
   interface. Aucune autre bibliothèque de traduction.
5. **Le cœur ne traduit pas** : `Voyager.Core` renvoie des erreurs et des résultats avec des codes
   stables (`reason`) et des valeurs ; la traduction se fait à la frontière (hôte ou interface). Le
   cœur reste sans dépendance de présentation.
6. **Nombres, montants et dates** sont formatés selon la langue choisie : `Intl` côté interface,
   culture .NET côté serveur ; jamais de format écrit en dur.
7. **Choix de la langue** : par défaut celle du système, modifiable dans les réglages de l'utilisateur.
8. **Contrôle** : un test vérifie que chaque message existe dans les deux langues et que les variables
   utilisées sont les mêmes.

## Conséquences

- `@fluent/bundle`, `@fluent/react` et `Linguini.Bundle` rejoignent la liste des dépendances
  autorisées ; i18next en sort.
- Les conventions TypeScript et C#, la règle `typescript.md` et l'agent `code-polish` passent de
  `i18n/fr.json` aux fichiers `.ftl` quand cet ADR sera accepté.
- Chaque PR qui ajoute un texte l'ajoute en français **et** en anglais.
- Linguini étant en version 0.x, on l'utilise derrière une petite abstraction de nos hôtes
  (`ILocalizer`), pour pouvoir en changer sans toucher au reste.
