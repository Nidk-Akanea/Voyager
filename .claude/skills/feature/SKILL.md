---
name: feature
description: Implémente de bout en bout une spec validée de Voyager — branche feature, contrat .proto, cas d'usage du cœur et tests, adaptateurs (persistance, service gRPC), écran React, documentation, relectures, puis livraison par /ship. À utiliser quand l'utilisateur demande d'implémenter une fonctionnalité dont la spec est au statut « Validée ».
---

# Implémenter une fonctionnalité

Spec ou fonctionnalité demandée : $ARGUMENTS

## 0. Vérifier qu'on peut commencer

- La spec existe dans `docs/specs/` et son statut est **« Validée »**. Sinon, arrête-toi : propose
  `/new-spec`, ou demande à l'utilisateur de la valider.
- Les décisions nécessaires sont prises : si la fonctionnalité impose un choix structurant non couvert
  par un ADR, propose `/new-adr` avant de coder.
- Crée `feature/<sujet>` depuis `develop` à jour.

## 1. Construire, de l'intérieur vers l'extérieur

1. **Contrat** : ajoute ou fais évoluer le `.proto` du domaine (`proto/voyager/<domaine>/v1/`) selon
   les [conventions gRPC](../../../docs/conventions/grpc.md) ; `pnpm proto:lint` et
   `pnpm proto:breaking` doivent passer.
2. **Cœur** : un cas d'usage par opération (`/new-use-case`), avec ses tests (`test-writer`) qui
   couvrent les critères d'acceptation et les cas limites de la spec.
3. **Adaptateurs** : implémentation des ports dans `Voyager.Persistence` (et migrations SQLite et
   PostgreSQL si le modèle change), service gRPC dans l'hôte concerné, correspondances écrites à la
   main ; tests d'intégration des chemins principaux.
4. **Interface** : écran dans `src/ts/ui/src/features/<domaine>/`, données via TanStack Query et le
   client généré, Fluent UI, textes en messages Fluent (`.ftl`) français et anglais, **aucun calcul métier**.
5. **Documentation** : glossaire, spec (statut « Implémentée » seulement si l'utilisateur le
   confirme), schémas si l'architecture a bougé.

Committe par étape cohérente (`feat: …`, `test: …`) : la PR sera fusionnée en squash.

## 2. Relire

Lance `architecture-reviewer` et `code-reviewer` (et `privacy-reviewer` si des données, journaux ou
la configuration Electron sont touchés). Traite leurs remarques, puis lance `code-polish`.

## 3. Livrer

Applique la [definition of done](../../rules/definition-of-done.md), puis `/ship`.
