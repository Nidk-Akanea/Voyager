# Contrats gRPC

Les fichiers `.proto` de Voyager, rangés par domaine et par version :
`voyager/<domaine>/v1/<domaine>.proto` (paquet `voyager.<domaine>.v1`).

Ce sont les contrats de l'[ADR-0002](../docs/adr/0002-architecture-modulaire-par-contrats-et-plugins.md) :
lis les [conventions gRPC](../docs/conventions/grpc.md) avant d'en modifier un. `pnpm proto:lint` et
`pnpm proto:breaking` vérifient le style et la compatibilité.
