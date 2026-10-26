# ADR-0014 — Écran de démarrage et mise à jour au démarrage

- **Statut** : Proposée
- **Date** : 2026-10-26
- **Remplace** : en partie [ADR-0009](0009-installation-et-mises-a-jour-velopack.md) : la décision 2
  (« propose de l'installer au prochain lancement ») et la décision 5 (hébergement des mises à jour sur
  les *releases* GitHub seulement)

## Contexte

[ADR-0009](0009-installation-et-mises-a-jour-velopack.md) a choisi Velopack et une mise à jour qui se
télécharge sans bruit au démarrage, puis s'installe « au prochain lancement ». Telle qu'elle est écrite
dans l'application, l'utilisateur ne voit rien : ni la recherche, ni le téléchargement (230 Mo), ni
l'installation. Ce n'est pas testable à la main (on ne sait pas ce qui se passe), et pas acceptable pour
un téléchargement de cette taille.

L'utilisateur veut **essayer une installation puis une mise à jour** avant de commencer le métier, ce
qui demande deux choses :

- des écrans qui montrent ce que fait l'application : un écran de démarrage, et la mise à jour avec sa
  progression (le design viendra plus tard, juste avant le métier) ;
- une source de mises à jour qu'on peut essayer **sans rien publier** sur GitHub, par exemple un serveur
  web de l'utilisateur.

Une mise à jour est du code que l'application exécute, et rien ne la signe (pas de signature de code
pour l'instant, voir le backlog) : la façon dont elle voyage compte.

## Options

### Comportement de la mise à jour

**Option A — Automatique au démarrage.** Une petite fenêtre de démarrage cherche une mise à jour ; s'il
y en a une, elle la télécharge avec une barre de progression, l'installe, et l'application redémarre
seule dans la nouvelle version. Sinon, elle démarre le service local et ouvre la fenêtre principale.

- Avantages : simple à comprendre et à tester ; personne ne reste sur une version périmée ; le service
  local, qui verrouille ses fichiers, n'est pas démarré tant qu'une mise à jour est en route.
- Inconvénients : l'utilisateur ne peut pas la refuser ni la remettre à plus tard ; un démarrage plus
  long quand il y a une mise à jour, et jusqu'à 8 secondes de plus quand le serveur de mises à jour ne
  répond pas (hors-ligne).

**Option B — Avec un choix.** L'écran propose « Mettre à jour maintenant » ou « Plus tard ».

- Avantages : l'utilisateur garde la main (un voyage en cours, une mauvaise connexion).
- Inconvénients : plus d'écrans et d'états à construire et à tester ; des utilisateurs qui repoussent
  la mise à jour indéfiniment.

**Option C — En arrière-plan** (ce que faisait l'application). Elle s'ouvre tout de suite, télécharge
sans bruit, puis affiche un bandeau « Redémarrer pour mettre à jour ».

- Avantages : aucun écran bloquant ; le démarrage n'attend jamais le réseau.
- Inconvénients : un bandeau dans l'interface principale, donc après le design ; plus difficile à
  essayer à la main.

### Source des mises à jour

**Source 1 — Les *releases* GitHub seulement** (ADR-0009). Aucun essai possible sans publier sur un
dépôt public.

**Source 2 — Un serveur web choisi à la fabrication du paquet**, écrit dans le `package.json` de
l'application, donc dans l'archive `app.asar` dont l'intégrité est contrôlée (ADR-0013). Sans choix, la
source reste GitHub. Le serveur doit être en `https`, ou en `http` vers la machine elle-même.

- Avantages : on essaie une mise à jour sans rien publier ; aucun réglage ni variable d'environnement
  ne peut rediriger les mises à jour (un attaquant qui écrit déjà dans le dossier d'installation de
  l'utilisateur pourrait, lui, remplacer l'application entière : c'est hors de portée d'une source) ;
  l'adresse n'est pas dans le dépôt public, elle est donnée à `tools/release.ps1 -UpdateSource`.
- Inconvénients : une adresse figée dans chaque paquet ; une source par paquet.

**Source 3 — Un réglage modifiable** (variable d'environnement, fichier de configuration).

- Avantages : on change de serveur sans reconstruire.
- Inconvénients : quiconque peut écrire dans l'environnement de l'utilisateur peut lui faire installer
  son propre code. C'est exactement ce que l'application évite pour le service local (ADR-0012,
  règle 6). Écartée.

## Décision

**Option A** pour le comportement, **Source 2** pour la source. L'utilisateur a choisi l'option A.

1. Au lancement, l'application installée ouvre une fenêtre de démarrage (la même interface, page
   `?screen=startup`, donc traduite en français et en anglais comme le reste). Elle montre : recherche de
   mise à jour, téléchargement avec progression, installation, démarrage.
2. La recherche s'arrête après 8 secondes. Le téléchargement n'a pas de limite de durée, mais il est
   abandonné quand sa progression reste immobile 60 secondes. Une erreur, hors-ligne ou sans release,
   est journalisée (sans l'adresse du serveur) et l'application s'ouvre normalement.
3. Une mise à jour trouvée est téléchargée, remise au programme de mise à jour de Velopack, et
   l'application quitte pour qu'il l'installe et la relance. Le service local n'a pas encore été démarré.
4. La source est celle du `package.json` si elle est valide (`https`, ou `http` vers la machine, sans
   identifiant ni paramètre dans l'adresse), sinon les *releases* GitHub.
5. En développement, la fenêtre de démarrage n'apparaît pas (rien à attendre) ; la variable
   `VOYAGER_SHOW_STARTUP_SCREEN` la fait apparaître pour la regarder. L'application installée l'ignore.

## Conséquences

- Positif : une installation puis une mise à jour s'essaient de bout en bout, sans publier ; les écrans
  existent déjà quand le design arrivera ; la décision 5 d'ADR-0009 reste vraie par défaut.
- Négatif : une mise à jour est imposée ; au démarrage, un serveur muet retarde l'application de 8
  secondes au plus ; une adresse de serveur dans le paquet.
- **L'intégrité d'une mise à jour repose sur HTTPS et sur les empreintes du fichier de versions de
  Velopack**, pas sur une signature : la signature de code reste au backlog, à faire avant une diffusion
  hors du groupe. Il faut protéger l'accès en écriture au serveur de mises à jour.
- Si l'installation échoue après que l'application a quitté, l'utilisateur n'a plus de fenêtre : il
  relance l'ancienne version, qui retente. Le journal de Velopack, à côté de l'installation, dit pourquoi.
- À vérifier : si le client de Velopack refuse qu'un serveur en `https` le redirige vers `http`.
  Sinon, la garantie « jamais en clair » ne tient que pour l'adresse de départ.
- Un paquet fabriqué pour `http://localhost` est un paquet d'essai : il ne se distribue pas.
- Si l'utilisateur veut finalement pouvoir repousser une mise à jour, l'option B s'ajoute à celle-ci
  (même écran, deux boutons) sans tout reprendre.
- À faire ensuite : le design des deux écrans (avant le métier), la première release publiée, les canaux
  (question Q24 du backlog).
