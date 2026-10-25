# ADR-0009 — Installation et mises à jour : Velopack

- **Statut** : Acceptée
- **Date** : 2026-10-24
- **Remplace** : la ligne « Application, packaging, mises à jour : Electron + Electron Forge » de
  l'[ADR-0006](0006-dependances-principales.md), pour l'installation et les mises à jour

## Contexte

L'utilisateur veut **un script qui produit une release installable et déployable**, et demande
d'utiliser **Velopack**.

Velopack est un outil d'installation et de mise à jour automatique d'applications de bureau
(Windows, macOS, Linux). Il prend une application déjà construite, en fait un installateur, et publie
des mises à jour **différentielles** : l'utilisateur ne télécharge que ce qui a changé. Il fonctionne
avec n'importe quelle application, et fournit des bibliothèques pour .NET et pour Node.js / Electron.

Ce que Voyager livre (ADR-0003, 0007) :

- **l'application de bureau** : Electron + interface React + **service local .NET** embarqué ;
- **le serveur du groupe** : un service ASP.NET Core et sa base PostgreSQL, en local ou en ligne ;
- **les plugins**, chacun avec sa version.

## Options

### Application de bureau

- **Electron Forge** (avec Squirrel pour Windows) : l'outil officiel d'Electron, mais des mises à jour
  complètes à chaque version et une configuration Windows datée.
- **electron-builder** + `electron-updater` : très répandu, mais pensé pour Electron seul ; le service
  .NET embarqué y est un simple fichier de plus.
- **Velopack** : mises à jour différentielles, installateur sans droits administrateur, un seul outil
  qui comprend aussi la partie .NET. → **Velopack**, choix de l'utilisateur.

### Serveur du groupe

Velopack sert aux applications de bureau, pas aux serveurs. Le serveur du groupe est livré comme une
**image Docker** avec un fichier Docker Compose (ADR-0004).

## Décision

1. **Application de bureau** : Velopack (`vpk`, outil .NET) produit l'installateur Windows et les
   paquets de mise à jour. Electron sert à construire et lancer l'application ; Electron Forge ne fait
   plus le packaging ni les mises à jour.
2. **Mises à jour** : l'application vérifie au démarrage s'il existe une nouvelle version, via la
   bibliothèque Velopack pour Node.js dans le processus principal Electron, et propose de l'installer
   au prochain lancement. Le service local est mis à jour avec l'application.
3. **Canaux** : `stable` pour les versions publiées depuis `main` ; `beta` pour les branches
   `release/…`. Les versions `alpha` de `develop` ne sont pas publiées.
4. **Version** : celle calculée par GitVersion (ADR-0007), transmise à `vpk` et à Electron.
5. **Hébergement des mises à jour** : les *releases* GitHub du dépôt. `vpk` sait les publier et
   l'application sait les lire.
6. **Le script** `tools/release.ps1` enchaîne, sur une version donnée :
   1. calcul de la version (GitVersion) ;
   2. publication du service local .NET (autonome, `win-x64`) ;
   3. construction de l'interface et de l'application Electron, avec le service local dans ses
      ressources ;
   4. `vpk pack` : installateur et paquets de mise à jour ;
   5. construction de l'image Docker du serveur du groupe ;
   6. sur demande, publication : release GitHub (installateur et paquets) et image Docker.

   La CI l'exécute à chaque tag de version (ADR-0007) ; il peut aussi être lancé à la main.
7. **Plugins** : livrés avec l'application tant qu'ils font partie du dépôt ; un mécanisme
   d'installation séparé sera défini dans l'ADR du mécanisme de plugins.

## Conséquences

- Velopack (`Velopack` / `vpk` côté .NET, `velopack` côté Node.js) rejoint la liste des dépendances
  autorisées ; Electron Forge n'est plus utilisé pour le packaging.
- La bibliothèque Velopack pour Node.js n'est publiée sur npm qu'en préversion à ce jour : sa version
  sera vérifiée et fixée au moment de l'intégrer.
- **Signature de code** : sans certificat, Windows SmartScreen avertira à l'installation. Un
  certificat de signature (payant) sera à prévoir avant de diffuser l'application hors du groupe.
- Le script ne peut être écrit et testé que lorsque l'application de bureau existera : il arrive avec
  l'étape 2 du squelette (tranche « À propos »), et la publication automatique à la première release.
- macOS et Linux sont possibles avec Velopack, mais hors périmètre tant que l'application cible
  Windows.
