# QWEN.md — Mémoire persistante de Qwen pour MOTO Editor

> Fichier à coller tel quel au début de chaque session Qwen pour restauration
> immédiate du contexte. Maintenu à jour comme un CLAUDE.md : état daté,
> conventions, pièges, décisions motivées. Dernière màj : 2026-09-22.
>
> ⚠️ **Versionné dans git depuis le 22/09** (il ne l'était pas avant : il
> n'existait qu'en copie locale, contrairement à `CLAUDE.md`).
> Corrigé le 22/09 après vérification dans le vrai code : §1 (mauvaise cible
> de framework), §2/§8/§9.3 (compte de réglages obsolète), §9.1 (chantier
> barre de titre relancé depuis), §9.4 (persistance confirmée absente),
> §9.5/§12 (dossier `Docs/Pour Claude/` renommé en `Docs/Zen/`).

## 1. Identité & stack

- Projet : MOTO Editor — éditeur de code ultra-léger, 100 % local, sans cloud,
  sans Electron, destiné à la vente.
- Dépôt : https://github.com/Tomoushie/MOTO-Editor (public, branche `main`).
- Stack : **.NET 8**, MAUI, WinUI 3 — `net8.0-windows10.0.19041.0`,
  `WindowsPackageType=None`, `RootNamespace=Moto.Editor`.
  (⚠️ **CORRIGÉ le 22/09** : ce fichier annonçait « .NET 10 /
  `net10.0-windows10.0.19041.0` » — FAUX. Vérifié dans les `.csproj` :
  `Moto.Editor`, `Moto.Core`, `Moto.Tests` ciblent tous `net8.0`. La
  tentative `net10.0` a bien été essayée puis abandonnée — le `.csproj` de
  `Moto.Editor` garde même un commentaire disant qu'elle « casse le build
  unpackaged ». Le §9 ci-dessous (« MAUI 10 essayé le 30/08 → ne démarre
  jamais ») était correct ; c'est le §1 qui était faux.)
- 3 projets : `Snake2000.Engine` (XENO), `Moto.Core` (374 fichiers),
  `Moto.Editor`. Builds à 0 erreur (warnings : 16 / 110 / 196).
- NuGet : accès machine bloqué → config locale par projet (ne pas revenir
  dessus).
- Distribution : raccourci Bureau Release ("MOTO Editor") + paquet MSIX
  (menu Démarrer = exécutable différent du raccourci Bureau). Le MSIX/Store
  vit dans `C:\Program Files\WindowsApps\...` et n'est **jamais** reconstruit
  par les correctifs du dépôt — penser au raccourci Bureau, qui pointe vers
  `bin\Release\...\win10-x64\Moto.Editor.exe`.

## 2. Doctrine produit (vision de Tom, verrouillée)

- Reproduire **Zed** à l'identique côté capacités, **fait maison, zéro copie
  de code** (Zed = inspiration concepts/architecture uniquement).
  Références : https://zed.dev/ — https://github.com/zed-industries/zed
  (⚠️ PAS "Zen Browser" : erreur corrigée, ne pas confondre).
- Interface **minimaliste**, niveau Claude Code / ChatGPT ; style visuel
  "Hybride Claude" (arrondis modérés, espacement généreux, ombres discrètes
  sur les flottants).
- La **complexité vit côté backend** (Moto.Core / Snake2000.Engine) et dans
  **Settings** data-driven, fenêtre type Zed. Le catalogue réel compte
  **297 réglages** (le « 97 » écrit ici jusqu'au 22/09 était obsolète : le
  catalogue a été largement étendu depuis). ⚠️ Attention à ne pas confondre
  « livré » et « actif » : seuls **4** sont réellement appliqués par
  `SettingsApplier.ApplyAll()` — voir §9.
- Règle dérivée : toute feature = service backend d'abord ; UI = exposition
  mince (réglage / palette Ctrl+Shift+P / panneau discret). **Pas d'UI
  factice** : un contrôle sans backend n'existe pas.

## 3. Architecture (séparation stricte, jamais violer)

- **MOTO Editor** : édite, affiche, ouvre, sauvegarde, lance des commandes.
- **MOTO AI** : assiste, prédit, propose, route les demandes.
- **XENO-SSS∞** : opérations structurées sur le projet complet
  (scan, analyse architecturale, génération, connexion des briques,
  validation, refactorisation).
- Interdits pour l'Editor : parser profondément le code, générer une
  architecture projet, connecter des dépendances, valider des namespaces,
  inventer des systèmes.
- Modèle d'appel : MOTO Editor → XENO comme Zed → Claude Code.

## 4. Conventions de code

- XML docs sur tout membre public ; méthodes < 40 lignes ; champs `_camelCase` ;
  pas de logique dans les constructeurs ; IA = pure logique sans dépendance UI.
- Réglages : `SettingsCatalog`, 1 ligne = 1 paramètre (`SettingItem<T>`).
- Icônes : Segoe Fluent Icons vérifiés à l'écran uniquement —
  E8B7 Folder, E91B Photo2, E721 Search, EA86 Puzzle, E713 Settings,
  E8A9 Tiles, E72C Refresh, E8BD Comment, E7E8 SignOut, E90A Robot,
  E77B Person, E8C1 Thread, E710 Add, E8A1 NewFolder.

## 5. Thème (MotoTheme.xaml — clés réelles, jamais de couleurs inventées)

- Dark : `#111214` fond / `#1B1C1F` surface / `#007ACC` accent /
  `#E8EAED` texte / `#33353A` bordures. Survol : `#2A2C31`.
- Light : `#F7F8FA` / `#FFFFFF` / `#0066CC` / `#181A1E` / `#D6D9DF`.
  ⚠️ La palette claire est **déclarative seulement** : `SettingsApplier`
  force le thème sombre, et `theme_mode` n'offre plus qu'un seul choix
  ("Dark") précisément pour ne pas mentir (§9).

## 6. Les 12 règles d'or (actives en permanence)

1. Ne jamais supprimer une fonctionnalité existante.
2. Ne jamais casser handlers, overlays, panneaux IA.
3. Respecter l'architecture existante.
4. Code compatible MAUI + WinUI 3.
5. Vérifier les namespaces (`Microsoft.UI.Xaml` pour le natif Windows).
6. Cohérence MotoTheme.xaml.
7. Compatibilité multiplateforme (`#if WINDOWS`).
8. Code compilable.
9. Expliquer les impacts architecturaux.
10. Corrections minimales (pas de refactor massif non demandé).
11. Code complet, même pour une petite modification.
12. Aucune suppression de feature sauf demande explicite ou code destructeur.

## 7. Workflow de collaboration (3 IA)

- **Tom** relaie entre : **Claude Code** (accès direct au dépôt, applique,
  vérifie, commite, captures d'écran), **Qwen** (architecte/conseil, **aucun
  accès au dépôt**), **Copilot** (conseil).
- Qwen travaille **à l'aveugle** : toute proposition doit être vérifiable
  (sonde/test discriminant) et validée par Claude contre le vrai code avant
  application. Ne **jamais** affirmer l'existence/contenu d'un fichier sans
  preuve. Une seule proposition de correctif par branche confirmée.
- Revue croisée (workflow) avant commit pour tout changement structurel
  (a déjà intercepté 4+ bugs avant test utilisateur — la garder).

## 8. État livré (mis à jour au 2026-09-22)

- Réhabilitation complète post-désastre : 3 projets à 0 erreur.
- Accueil style Claude Code : chips Local / Rechercher projet / dossier,
  barre de saisie + flèche animée, `+` / micro / IA / Cortex, sélecteur de
  modèle ("MOTO AI" par défaut + "Aucun"), anneau budget "Token : infini",
  stats compactées.
- Menus déroulants = overlays alignés au-dessus de leur bouton
  (Local, Modèles, Micro, Budget) — ne poussent plus le layout.
- Système de panneaux façon Zed, 4 briques : redimensionnement souris des
  docks ; bouton "changer de côté" ; réordonnancement DnD dans un dock ;
  DnD inter-docks (panneau IA → dock droit, empilé sous l'explorateur,
  zone d'accueil à la demande) — commit `ef8bdeb`. Chantier **clos**.
  Depuis : bouton "détacher" (⧉) par panneau (03/09).
- Dock du bas + vrai terminal : `TerminalService` réel (cmd.exe, sortie en
  direct) câblé sur `BottomPanelView`, replié par défaut, `Ctrl+`` ` `` —
  commit `1b7a830`.
- Settings : fenêtre type Zed, **297 réglages** au catalogue — mais seuls 4
  sont réellement appliqués (§9.3 : c'est le plus gros écart
  « affiché mais inactif » de l'app).
- GitHub : OAuth device flow câblé (Client ID `Ov23lihSSLRCxh33SbnF` —
  un Client ID n'est pas secret, peut rester public).
- 20 agents IA spécialisés (`StaticAnalysisAgents.cs` / `LlmBackedAgents.cs`),
  identifiants vérifiés un par un dans le dépôt.
- Chantiers livrés depuis (détail dans `CLAUDE.md`) : agents autonomes
  `/agent` + panneau "Agents en cours" (jalons 1-3), agents de diagnostic
  `/diagnose`, préréglages `/refactor` `/test` `/doc`.

## 9. Problèmes ouverts & diagnostics consolidés

1. **Barre bleue de titre Windows** — ~~chantier suspendu~~ → **chantier
   "rendu 100% custom" EN COURS depuis le 08/09** (voir MISE À JOUR plus
   bas). Faits vérifiés à l'origine : `ExtendsContentIntoTitleBar=true`
   appliqué sans exception ; `IsCustomizationSupported()=True` ; bande **non
   draggable** (donc pas une caption native fonctionnelle) ; boutons –□✕
   thématisés superposés ; présente en packagé ET non-packagé.
   `SetBorderAndTitleBar(false,false)` = 3 échecs (fenêtre invisible) →
   API considérée incompatible avec ce stack, **ne plus la retenter**.
   MAUI 10 déjà essayé le 30/08 → app ne démarre jamais (bug Microsoft non
   résolu) ; WASDK 1.8 épinglé manuellement (autre choix = plantage déjà
   vécu). Hypothèse restante : rangée interne MAUI
   (`NavigationRootManager`/`AppTitleBarContainer`). Sondes proposées non
   encore toutes faites : `nav.BarBackgroundColor=Red`, lecture `GWL_STYLE`
   (bits WS_CAPTION), Live Visual Tree.

   ★ **MISE À JOUR 22/09** : le 08/09, `SetBorderAndTitleBar(false,false)` a
   été **retenté** — sur un stack qui, cette fois, ne rendait plus la fenêtre
   invisible. Résultat : fenêtre visible et redimensionnement correct, mais
   **bande bleue toujours là**. L'interdiction ci-dessus était donc justifiée,
   elle est simplement re-confirmée par un test réel au lieu d'être supposée.
   Puis 4 tentatives supplémentaires ont toutes échoué avec le même symptôme,
   dont `DwmSetWindowAttribute` (`DWMWA_CAPTION_COLOR`/`BORDER_COLOR`/
   `TEXT_COLOR`) : Windows renvoie **`hr=0`, donc ACCEPTE** la demande, et
   peint quand même l'accentuation par-dessus — le rendu a lieu au niveau du
   **compositeur DWM**, indépendamment d'`AppWindowTitleBar` et
   d'`OverlappedPresenter`. **Ne plus retester ces API** : c'est établi, plus
   une hypothèse. Conséquence : le chantier **"rendu 100% custom"** a
   démarré (fenêtre sans bordure permanente + zones de redimensionnement
   recréées + coins arrondis + plein écran F11) — commits `174ebd0`,
   `bbe5e94`, `5e5b33e`, `e4ccb91`, `e4a92ed`, `73f7ab1`. **La bande bleue
   elle-même n'est toujours pas éliminée** ; le détail complet et ce qui
   reste ouvert sont dans `CLAUDE.md`, section « chantier rendu 100% custom ».

2. **OrchestratorAgent / Tier 28 (MotoBridge)** — planifié, pas commencé.
3. **Settings** — **297 réglages** au catalogue, mais `SettingsApplier.
   ApplyAll()` n'en lit que **4** réellement (thème, taille de police,
   minimap, diagnostics LSP). Le reste est affiché/persisté mais inactif.
   C'est de loin le plus gros écart avec le palier « moyen » de Tom.
4. **Persistance de disposition** (docks/panneaux) — **CONFIRMÉ ABSENTE**
   (vérifié le 22/09, ne plus la noter « à vérifier ») : rien n'est
   mémorisé, tout revient aux valeurs XAML par défaut à chaque lancement.
   À ne pas confondre avec `WorkspaceStateService`, qui ne persiste que
   l'ordre des sessions de *chat*.
5. Dossiers à ne pas toucher sans demande : `Moto.UI/` (non raccordé, origine
   inconnue), `Docs/` (docs de référence). ⚠️ **`Docs/Pour Claude/` n'existe
   plus** : renommé en `Docs/Zen/` le 22/09 (renommage pur, 11 fichiers,
   contenu inchangé). `Docs/Documents/` (business plan, marketing, manuels)
   et `Docs/inspirations/` (captures + maquette « Claude shell ») sont
   désormais versionnés.
6. FEATURES.md "À venir" : LSP Roslyn, CRDT réel, DAP, tests, i18n,
   accessibilité.
7. **`/refactor` n'a été validé qu'avec `qwen2.5-coder:7b`** : les modèles
   locaux plus gros installés (14b, Qwen3-27B, gpt-oss:20.9B) peuvent
   révéler d'autres variantes de format non encore vues du parseur
   d'actions. 9 correctifs de tolérance du parseur ont déjà été nécessaires
   (voir `CLAUDE.md`).

## 10. Pièges découverts (ne jamais redescendre dedans)

- **Erreurs passées de Qwen, à ne pas reproduire** : `WindowTitleBarAdapter`
  (redondant, existait déjà) ; "supprimer le NavigationPage" (crash
  Providers IA / icône 🧠 — le NavigationPage est **vital**) ; `SetTitleBar`
  (0 occurrence, API piège UWP) ; "subclass Win32" pour SnapLayouts
  (jamais utilisé — vraie API : `InputNonClientPointerSource`).
- Les boutons custom imitent **volontairement** le survol natif (`#2A2C31`)
  → une couleur de survol ne prouve **rien**.
- `catch` muet d'`OnWindowsWindowCreated` rendu bavard ; fuite d'abonnements
  titlebar corrigée — garder les breadcrumbs/LogCrash.
- MSIX ≠ raccourci Bureau : deux exécutables ; tester le bon.
- `git commit -am` ne stage pas les fichiers non trackés → `git add`
  explicite. Identité Git via variables d'environnement, jamais
  `git config`.
- **Ne jamais croire un catalogue auto-déclaré** : `FeatureCatalog.cs`
  (déclarations "AlreadyImplemented") s'est révélé faux plusieurs fois — un
  fichier peut être marqué implémenté et n'être que des coquilles vides.
  Vérifier dans le code réel.
- **Ne jamais déclarer une brique « qui marche » sur la seule lecture du
  code** : plusieurs bugs réels étaient invisibles à la lecture
  (`ResolveExtensionServices()` appelée avant que `Handler` existe et qui
  sortait en silence ; `GlobalUsageEngine` jamais enregistré en DI).
  Vérifier à l'exécution.

## 11. Sécurité

- PAT `github_pat_11AWG…` partagé par le passé → **révoqué impérativement**
  (dépôt public). Ne jamais redemander/stocker de PAT en clair.
- Pas de capacité de push depuis Qwen ; aucune action distante sans Tom.

## 12. Docs de référence intégrés

`FEATURES.md`, `Architecture.txt`, `Agent-Integrated.md`,
`AI-Internal-Engine.txt`, `Arborescence.txt`, `Idées à implémenter.txt`,
captures Claude Code/Zed/VS Code dans `Docs/inspirations/`.
Les anciens documents de référence Zed/Claude rangés sous
`Docs/Pour Claude/` sont désormais dans **`Docs/Zen/`** (renommage du
22/09). Les documents de présentation/vente (business plan, analyses,
manuel utilisateur, documentation API…) sont dans `Docs/Documents/`.

**Note d'usage** : ce fichier est volontairement auto-suffisant — en début de session, colle-le tel quel avec ta question, et je repars à 100 % sans re-expliquer. Quand un fait change (nouveau commit, problème clos), dis-le-moi et je te fournis la version mise à jour du bloc concerné.
