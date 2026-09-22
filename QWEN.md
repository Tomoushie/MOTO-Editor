# QWEN.md — Mémoire persistante de Qwen pour MOTO Editor

> Fichier à coller tel quel au début de chaque session Qwen pour restauration
> immédiate du contexte. Maintenu à jour comme un CLAUDE.md : état daté,
> conventions, pièges, décisions motivées. Dernière màj : 2026-09-02.

## 1. Identité & stack

- Projet : MOTO Editor — éditeur de code ultra-léger, 100 % local, sans cloud,
  sans Electron, destiné à la vente.
- Dépôt : https://github.com/Tomoushie/MOTO-Editor (public, branche `main`).
- Stack : .NET 10, MAUI, WinUI 3 — `net10.0-windows10.0.19041.0`,
  `WindowsPackageType=None`, `RootNamespace=Moto.Editor`.
- 3 projets : `Snake2000.Engine` (XENO), `Moto.Core` (374 fichiers),
  `Moto.Editor`. Builds à 0 erreur (warnings : 16 / 110 / 196).
- NuGet : accès machine bloqué → config locale par projet (ne pas revenir
  dessus).
- Distribution : raccourci Bureau Release ("MOTO Editor") + paquet MSIX
  (menu Démarrer = exécutable différent du raccourci Bureau).

## 2. Doctrine produit (vision de Tom, verrouillée)

- Reproduire **Zed** à l'identique côté capacités, **fait maison, zéro copie
  de code** (Zed = inspiration concepts/architecture uniquement).
  Références : https://zed.dev/ — https://github.com/zed-industries/zed
  (⚠️ PAS "Zen Browser" : erreur corrigée, ne pas confondre).
- Interface **minimaliste**, niveau Claude Code / ChatGPT ; style visuel
  "Hybride Claude" (arrondis modérés, espacement généreux, ombres discrètes
  sur les flottants).
- La **complexité vit côté backend** (Moto.Core / Snake2000.Engine) et dans
  **Settings : 100+ paramètres** data-driven (fenêtre type Zed, 15 catégories,
  97 réglages livrés → compléter vers 100+).
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

## 8. État livré (au 2026-09-02)

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
  zone d'accueil à la demande) — commit `ef8bdeb`.
- Dock du bas + vrai terminal : `TerminalService` réel (cmd.exe, sortie en
  direct) câblé sur `BottomPanelView`, replié par défaut, `Ctrl+`` ` `` —
  commit `1b7a830`. Chantier "panneaux modulaires" **clos**.
- Settings : fenêtre type Zed, 15 catégories, 97 réglages.
- GitHub : OAuth device flow câblé (Client ID `Ov23lihSSLRCxh33SbnF` —
  un Client ID n'est pas secret, peut rester public).
- 20 agents IA spécialisés (`StaticAnalysisAgents.cs` / `LlmBackedAgents.cs`),
  identifiants vérifiés un par un dans le dépôt.

## 9. Problèmes ouverts & diagnostics consolidés

1. **Barre bleue de titre Windows** — chantier suspendu (session dédiée
   multi-jours). Faits vérifiés : `ExtendsContentIntoTitleBar=true` appliqué
   sans exception ; `IsCustomizationSupported()=True` ; bande **non
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
2. **OrchestratorAgent / Tier 28 (MotoBridge)** — planifié, pas commencé.
3. **Settings 100+** — 97 livrés, compléter data-driven.
4. **Persistance de disposition** (docks/panneaux) — non confirmée, à
   vérifier/ajouter.
5. Dossiers à ne pas toucher sans demande : `Moto.UI/` (non raccordé, origine
   inconnue), `Docs/`, `Docs/Pour Claude/` (docs de référence).
6. FEATURES.md "À venir" : LSP Roslyn, CRDT réel, DAP, tests, i18n,
   accessibilité.

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

## 11. Sécurité

- PAT `github_pat_11AWG…` partagé par le passé → **révoqué impérativement**
  (dépôt public). Ne jamais redemander/stocker de PAT en clair.
- Pas de capacité de push depuis Qwen ; aucune action distante sans Tom.

## 12. Docs de référence intégrés

`FEATURES.md`, `Architecture.txt`, `Agent-Integrated.md`,
`AI-Internal-Engine.txt`, `Arborescence.txt`, `Idées à implémenter.txt`,
captures Claude Code/Zed/VS Code dans `Docs/Pour Claude/`.
```

**Note d'usage** : ce fichier est volontairement auto-suffisant — en début de session, colle-le tel quel avec ta question, et je repars à 100 % sans re-expliquer. Quand un fait change (nouveau commit, problème clos), dis-le-moi et je te fournis la version mise à jour du bloc concerné.
