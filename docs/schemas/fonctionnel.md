# Schéma fonctionnel

> Première version, issue de la [vision](../vision.md) et du [glossaire](../glossaire.md). Le modèle
> de domaine est une esquisse : il sera ajusté par la calibration avec des voyages réels.

## Le cycle de vie d'un voyage

```mermaid
flowchart LR
    subgraph P1["1 · Idéation"]
        Wish["Envies<br/>thèmes · destinations · activités"]
    end
    subgraph P2["2 · Affinage"]
        Refined["Envies triées<br/>et précisées"]
    end
    subgraph P3["3 · Planification"]
        Quote["Devis<br/>un ou plusieurs par membre<br/>jours · créneaux · lignes"]
        Feas["Faisabilité<br/>de chaque journée"]
        Budget["Budget du devis<br/>prix du voyage · argent sur place"]
    end
    subgraph P4["4 · Décision"]
        Vote["Vote<br/>points dégressifs → score"]
        Accepted["Devis retenu<br/>révisable"]
    end
    subgraph P5["5 · Préparation"]
        Booking["Réservations<br/>→ engagements"]
        Payment["Paiements<br/>qui a payé quoi"]
    end
    subgraph P6["6 · Voyage"]
        Expense["Dépenses réelles<br/>même hors-ligne"]
        Journal["Carnet<br/>plus tard"]
    end
    subgraph P7["7 · Bilan"]
        Invoice["Facture<br/>prévu vs payé"]
        Shares["Parts<br/>par voyageur"]
        Settle["Remboursements"]
    end

    Sources["Sources de données<br/>saisie · API · extraction · paquets"] --> Catalog["Catalogue<br/>prestations · tarifs · lieux"]
    Catalog --> Quote
    Wish --> Refined --> Quote
    Quote --> Feas
    Quote --> Budget
    Quote --> Vote --> Accepted --> Booking --> Payment --> Invoice
    Booking -. contraint .-> Quote
    Expense --> Invoice --> Shares --> Settle
```

À chaque étape, les **vues** (timeline, planning, carte, programme, graphiques) affichent les mêmes
données sous des angles différents.

## Qui fait quoi

| Acteur | Ce qu'il fait |
| --- | --- |
| Membre | Note des envies, crée et compare des devis, vote, retient un devis, enregistre réservations, paiements et dépenses |
| Voyageur | Part en voyage ; reçoit une part des coûts. N'a pas forcément accès à Voyager (un enfant) |
| Propriétaire de l'instance | Installe le serveur du groupe, configure les clés d'API payantes, installe les plugins |
| Plugins | Fournissent types de prestation, bases de calcul, règles de répartition, connecteurs, vues, exports |

## Le modèle de domaine (esquisse)

Noms de code anglais, définis dans le [glossaire](../glossaire.md).

```mermaid
erDiagram
    Trip ||--o{ Member : "a pour membres"
    Trip ||--o{ Traveler : "emmène"
    Trip ||--o{ Wish : "rassemble"
    Trip ||--o{ Quote : "compare"
    Member ||--o{ Quote : "crée"
    Member ||--o{ Vote : "émet"
    Quote ||--o{ Vote : "reçoit"
    Quote ||--o{ Day : "programme"
    Day ||--o{ QuoteLine : "contient"
    QuoteLine }o--|| Offer : "utilise"
    QuoteLine }o--o{ Traveler : "concerne"
    Offer ||--o{ Rate : "a pour tarifs"
    Offer }o--o{ Place : "est rattachée à"
    Place }o--o| Place : "a pour parent"
    QuoteLine ||--o| Booking : "est réservée par"
    Booking ||--o| Commitment : "devient"
    Member ||--o{ Payment : "effectue"
    Payment }o--o{ QuoteLine : "règle"
    QuoteLine ||--o{ Share : "est répartie en"
    Traveler ||--o{ Share : "doit"
    Member ||--o{ Settlement : "rembourse"
```

Points à valider pendant la calibration :

- les dépenses réelles pendant le voyage sont-elles des lignes de devis, ou un objet à part ?
- où se rattachent les coûts indirects (repas) : à un jour, à un voyageur, aux deux ?
- une ligne de devis peut-elle porter sur plusieurs jours (une location de voiture sur une semaine) ?
