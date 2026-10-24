---
paths:
  - "proto/**"
  - "plugins/*/proto/**"
  - "**/*.proto"
  - "buf.yaml"
  - "buf.gen.yaml"
---

# Contrats gRPC (.proto)

Référence complète : [conventions gRPC](../../docs/conventions/grpc.md). Les `.proto` sont les contrats
de l'ADR-0002 : une erreur ici casse le client, le serveur et les plugins.

- **Ne renumérote jamais un champ et ne réutilise jamais un numéro.** Un champ supprimé devient
  `reserved` (numéro et nom).
- Renommer, changer un type ou supprimer est une rupture : crée une version `v2` du paquet au lieu de
  modifier `v1`, et signale-le à l'utilisateur.
- Paquet `voyager.<domaine>.v1` ; `option csharp_namespace = "Voyager.Contracts.<Domaine>.V1"`.
- Services `XxxService` ; une paire `XxxRequest` / `XxxResponse` par méthode ; champs en `snake_case` ;
  valeurs d'énumération préfixées, `0` = `…_UNSPECIFIED`.
- Types communs : `google.type.Money`, `google.type.Date`, `google.protobuf.Timestamp` + `time_zone`
  (IANA), `google.type.LatLng`, identifiants en `string` UUID.
- Erreurs métier : code de statut gRPC + `google.rpc.ErrorInfo` avec une `reason` stable en MAJUSCULES.
- Chaque service, méthode et message a un commentaire en anglais.
- `buf lint` et `buf breaking` doivent passer.
