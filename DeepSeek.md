# DeepSeek.md — Mémoire persistante pour MOTO Editor

> **À qui s'adresse ce fichier** : à tout modèle IA (DeepSeek, Claude, Qwen,
> Copilot, Gemini…) qui reprend ce projet sans avoir vu les sessions
> précédentes. Il est **auto-suffisant** : le coller en début de session suffit
> à repartir à 100 %.
>
> **Différence avec `CLAUDE.md` et `QWEN.md`** :
> - `CLAUDE.md` (≈131 Ko) est la carte d'architecture exhaustive, tenue par
>   Claude Code qui a un accès direct au dépôt. C'est la référence de fond.
> - `QWEN.md` est la mémoire de Qwen (conseil, **sans** accès au dépôt).
> - **Ce fichier** est le résumé d'état + les règles de méthode, écrit par
>   DeepSeek à la suite de la session du 30/09 – 01/10. Il contient les faits
>   que `QWEN.md` ignore encore (il date du 22/09 sur plusieurs points).
>
> ⚠️ **Règle d'or de ce dépôt** : ne jamais croire un document (y compris
> celui-ci) sans vérifier dans le code réel. Plusieurs fichiers d'état se sont
> révélés faux ou optimistes — c'est arrivé à `FeatureCatalog.cs`,
> `FEATURES.md`, `Roadmap.md`, `AI-OPTIMIZATIONS.md`, à la présentation projet,
> et à `QWEN.md` lui-même. **Vérifier avant de croire.**

Dernière mise à jour : **2026-10-01**.

---

## 1. Identité & stack

- MOTO Editor — éditeur de code 100 % local, sans cloud, sans Electron,
  destiné à la vente. Dépôt : https://github.com/Tomoushie/MOTO-Editor
  (public, branche `main`).
- **Stack : .NET 8**, MAUI, WinUI 3 — `net8.0-windows10.0.19041.0`,
  `WindowsPackageType=None`, `RootNamespace=Moto.Editor`.
  ⚠️ **Ne pas croire les documents qui annoncent « .NET 10 »** : la bascule a
  été tentée le 30/08, elle a **échoué** (le SDK MAUI 8 ne supporte pas ce
  TFM), et le projet est **revenu à .NET 8**. La présentation projet
  (`Docs/Documents/`) l'a annoncé à tort pendant des semaines et se
  contredisait elle-même — corrigé le 30/09.
- 3 projets : `Snake2000.Engine` (XENO), `Moto.Core`, `Moto.Editor`.
- **Ligne de base de compilation à ne jamais dépasser : 0 erreur ·
  477 avertissements** (`Release`, `--no-incremental`). Mesuré le 30/09,
  re-vérifié le 01/10. ⚠️ Le chiffre **479** qui circule dans les documents
  plus anciens est **périmé** : une ligne de base trop haute ferait accepter
  un lot qui *ajoute* des avertissements. Toujours remesurer.
- **Environnement de build (01/10, 06:40)** : la machine a été re-upgradée
  pendant la nuit — les SDK .NET 8/9 ont été **désinstallés** (05:32–05:46)
  et un **SDK 10.0.401 installé** (06:35). Conséquence : `dotnet build`
  cassait (`global.json` exige 8.0.4xx). Réparé en réinstallant
  **`Microsoft.DotNet.SDK.8` (8.0.425, winget)** + `dotnet workload install
  maui`. `global.json` **inchangé** (la doctrine reste verrouillée sur
  .NET 8 — la tentative .NET 10 du 30/08 a échoué). Le SDK 10 présent sur
  la machine est **sans effet** tant que `global.json` pète 8.0.4xx.
  Build de référence après réparation : `Moto.Editor.csproj` Release
  `--no-incremental` = 0 erreur / 477 avertissements.
- Distribution : le raccourci Bureau « MOTO Editor » pointe vers
  `Moto.Editor\bin\Release\net8.0-windows10.0.19041.0\win10-x64\Moto.Editor.exe`
  — **pas** `bin\Debug`. Un paquet MSIX séparé existe dans
  `C:\Program Files\WindowsApps\MotoSoftware.MOTOEditor_...` et n'est **jamais**
  reconstruit par les correctifs du dépôt. **Reconstruire la Release avant de
  demander un test à Tom**, sinon il teste un binaire périmé.

## 2. Doctrine (verrouillée par Tom)

- Reproduire **Zed** en capacités, **fait maison, zéro copie de code**.
  Références : https://zed.dev/ et https://github.com/zed-industries/zed.
  (⚠️ Pas « Zen Browser » — confusion déjà commise.)
- Interface minimaliste, niveau Claude Code ; complexité **côté backend**.
- **Pas d'UI factice** : un contrôle sans backend réel n'existe pas.
- **Règle dérivée, la plus importante pour la suite** : tout ce qui est
  annoncé doit fonctionner. C'est le verrou du palier « vendable ».

### Échelle de qualité de Tom — 2 axes × 6 niveaux

`cheap → faible → moyen → élevé → vendable → triple A`

| Axe | Position |
|---|---|
| **Visuel** | « moyen », candidat « élevé » sur les surfaces parcourues (à juger par Tom) |
| **Backend / Structure** | **élevé** — le passage à « vendable » exige que tout ce qui est annoncé fonctionne |

## 3. Architecture (séparation stricte, jamais violer)

- **MOTO Editor** : édite, affiche, ouvre, sauvegarde, lance des commandes.
- **MOTO AI** : assiste, prédit, propose, route les demandes.
- **XENO-SSS∞** : opérations structurées sur le projet complet.
- Interdits pour l'Editor : parser profondément le code, générer une
  architecture projet, connecter des dépendances, valider des namespaces.

## 4. Contraintes techniques à connaître AVANT d'écrire du code

Ces pièges sont tous **mesurés**, pas supposés. Chacun a coûté du temps.

- **MAUI 8 n'a pas `FontWeight`** sur `Label` (seulement `FontAttributes` :
  None/Bold/Italic). `FontWeight` a été ajouté en MAUI 10 — ne pas l'utiliser.
- **Un `Button` doit porter un des 5 styles du thème**
  (`MotoPrimary/Secondary/Ghost/Danger/IconButton`). Sinon son `Padding` et
  son `CornerRadius` sont **ignorés en silence** : le mappage `NoNative`
  (`MauiProgram.cs`) remet à zéro marge, bordure et style natif de **tous**
  les boutons après les réglages MAUI.
- **`StrokeShape` attend `RoundRectangle N` en NOMBRE.** Ne JAMAIS écrire
  `StrokeShape="RoundRectangle {StaticResource …}"` : la valeur est passée à
  un `TypeConverter` qui attend du texte, ça compile mais rend faux **en
  silence**, sans qu'aucun contrôle automatique ne le voie.
- **`StaticResource` se résout à l'analyse XAML** : aucune référence vers un
  jeton déclaré plus bas dans le même dictionnaire (a causé un crash au
  démarrage : `StaticResource not found for key RadiusMd`).
- **Une colonne `Grid` `Auto` ne se replie PAS** quand son enfant passe à
  `IsVisible=false` — le `WidthRequest` reste mesuré (36 px de vide constatés).
  Il faut lier la largeur depuis le modèle (`GridLength`, qui vit dans
  `Microsoft.Maui.Controls`, **pas** `Microsoft.Maui.Graphics` → CS0234).
- **Un `x:Name` posé dans un `DataTemplate` est inatteignable** depuis le
  code-behind (l'élément est instancié une fois **par ligne**). Toute valeur
  par ligne doit transiter par une propriété du modèle.
- **`CollectionView` n'applique pas l'état `Selected` sous Windows.**
  Utiliser un `DataTrigger` sur une propriété du modèle.
- **`FontImageSource` (icône-police) dans un `Button` ne s'affiche pas sous
  Windows** → texte seul, ou glyphe via `Text` + police `MotoIcons`.
- **Ne pas mettre de `Shadow`** sur un élément toujours visible au-dessus de
  l'éditeur WebView : les calques affichés ensuite (palette, confirmation) ne
  se dessinent plus. Cause profonde non établie.
- **Écrans secondaires : jamais `Navigation.PushAsync`.** MainPage croit alors
  se fermer et détruit Cortex/Workspace/Doc/Preview. Utiliser
  `ScreenHost.Show("Titre", vue)`.
- **Fermeture de fenêtre** : une fois le `Handler` nul, plus aucun appel natif
  (`InputNonClientPointerSource`, `AppWindow`, `DispatcherQueue`) — ça a
  causé un plantage `0xC000027B` **sans aucune ligne de journal**.

## 5. Réglages : le point le plus important du projet aujourd'hui

C'est **le plus gros écart « affiché mais inactif »** de l'application, et
donc le verrou direct du palier « vendable ».

**Mesures successives** (script `scripts/settings-coverage.ps1`, périmètre =
code réellement compilé) :

| Date | Opérants / déclarés |
|---|---|
| 22/09 | 12 / 324 (3,7 %) |
| 28/09, avant câblage | 20 / 332 (6,0 %) |
| 28/09, après `tabs_*` | 29 / 332 (8,7 %) |
| **01/10, après `pp_*` + `tb_*`** | **45 / 332 (13,6 %)** |
| **01/10, après `gp_*` (+ `sb_*` en parallèle)** | **59 / 332 (17,8 %)** |
| **01/10, après fusion `ap_*` + `cp_*`** | **64 / 332 (19,3 %)** |
| **01/10, après `terminal_*` (5 clés)** | **69 / 332 (20,8 %)** |
| **01/10, après `terminal_*` (2e lot, 5 clés de plus)** | **74 / 332 (22,3 %)** |
| **01/10, après `git_*` (4 clés)** | **78 / 332 (23,5 %)** |
| **01/10, après `agent_font_size` + correction du verrou `RealEffectKeys`** | **79 / 332 (23,8 %)** |
| **01/10, après `search_include_ignored` (+ défaut corrigé au catalogue)** | **80 / 332 (24,1 %)** |
| **01/10, après `auto_indent` + `auto_update` (lot auto_*)** | **82 / 332 (24,7 %)** |
| **01/10, après `doc_folder` + correction du défaut de `doc_auto_update` (lot doc_*)** | **83 / 332 (25,0 %)** |

⚠️ **Ne comparer qu'un avant/après mesuré dans le MÊME arbre de travail.** Le
chantier `ap_*`/`cp_*` annonçait **45 → 50 (15,1 %)** dans SON worktree, où
`gp_*`/`sb_*` n'étaient pas câblés : son « après » (50) n'est donc **pas**
comparable aux 59 du tronc, qui incluent ces deux familles. Un chiffre de
couverture lu hors de son worktree ne veut rien dire.

### ⚠️ Le piège qui a failli casser l'application

`SettingsEngine.GetBool(clé)` / `GetInt(clé)` / `GetString(clé)` **sans second
argument ne consultent JAMAIS le catalogue**. Ils retombent sur
`false` / `0` / `""` pour une clé absente du store. Le « défaut déclaré » du
catalogue n'existe donc qu'**à l'affichage** dans la fenêtre Réglages.

Conséquence concrète : rendre un réglage opérant avec cet appel fait
**disparaître** l'élément sur une installation neuve (cas réel : la barre
d'onglets, les icônes de l'explorateur, le statut git).

**Toujours passer le défaut explicitement**, via les mappages créés pour ça :
`Moto.Editor/Settings/TabBarSettings.cs`, `PanelSettings.cs`,
`TitleBarSettings.cs`, `GitPanelSettings.cs`, `DockPanelSettings.cs`,
`TerminalSettings.cs` — chacun expose
`DeclaredBool/DeclaredInt/DeclaredString` qui lit
`SettingsCatalog.ById(id).Default`.

### La méthode qui marche (à reproduire)

1. Un fichier `XxxSettings.cs` : mappage centralisé, défauts **déclarés**.
2. **Un point d'accroche unique** : `MainPage.ApplyLayoutSettings()`
   (`MainPage.UI.cs`), appelé au démarrage, à **chaque changement de réglage**
   et au retour de plein écran. Il appelle déjà `StatusBar`, `EditorPane`,
   `MenuBar`, `ExplorerPanel`.
   ⚠️ Variante `gp_*` : quand le panneau n'est **pas** un contrôle statique de
   `MainPage.xaml` mais une vue construite à la demande dans une fenêtre
   (`GitPanelView`), il faut la retrouver via `WindowManager.Get(...)` puis
   parcourir l'arbre avec `IVisualTreeElement.GetVisualChildren()` —
   `Element.LogicalChildren` est **obsolète** et ferait monter la ligne de base
   d'avertissements (477 → 478).
3. Des lectures à clé **LITTÉRALE** (le script de mesure ne compte pas les
   variables — une indirection fait mentir la mesure, en baisse).
4. **N'inventer aucune donnée** : un réglage qui affiche une valeur fausse est
   **pire** qu'un réglage inerte. Laisser inerte et documenter la raison.

### Familles traitées

- **`tabs_*`** (9 clés) : barre d'onglets, boutons d'action, précédent/suivant,
  icônes, pastille d'erreurs, position et mode de la croix, max d'onglets,
  onglet activé après fermeture.
- **`pp_*`** (12 clés sur 13) : explorateur de fichiers. Dont **trois valeurs
  qui étaient figées en dur** — indentation (16 px), hauteur de ligne (24 px),
  fichiers `.` toujours masqués. Plus filtrage `.gitignore` réel, dépliage des
  dossiers parents, largeur/côté, statut git par fichier (vrai `GitService`).
- **`tb_*`** (4 clés sur 10) : barre de titre — menus, hôte + projet, branche
  git réelle, position des boutons de fenêtre.
- **`gp_*`** (12 clés sur 15) : panneau Git. Tri, style de statut, groupement,
  repli des non suivis, vue « arborescente » (chemin complet), stats de diff
  **réelles** (`git diff --numstat`, nouveau `GitService.GetDiffStatsAsync`),
  comportement au clic (ouvrir le fichier / déplier le diff réel du projet),
  scrollbar, longueur max du titre de commit appliquée **au message envoyé à
  git**, ouverture au démarrage, largeur et dock de la fenêtre. Les lignes du
  panneau sont passées de `string` à un modèle (`Models/GitChangeNode.cs`) :
  sans ça, aucun réglage d'affichage ne pouvait les atteindre.
  **Restent inertes :** `gp_button` (le bouton git de la barre de statut
  n'existe pas), `gp_fallback_branch` (afficher « main » dans un dossier sans
  dépôt = branche inventée, pas un repli réellement utilisé),
  `gp_count_badge` (le seul nombre non ambigu, `Staged.Count`, ne répond pas au
  libellé « changements non commités » ; un total compterait deux fois tout
  fichier indexé puis remodifié — « MM »).
- **`ap_*`** (3 clés sur 7) et **`cp_*`** (2 clés sur 3) : géométrie et dock.
  Mappage `Moto.Editor/Settings/DockPanelSettings.cs`, appliqué par
  `ApplyAgentAndCollabPanelSettings(s)` depuis `ApplyLayoutSettings` **et**
  `SettingsWindow.RealSettingChanged` (préfixes `ap_`/`cp_`).
  ⚠️ **`ap_*` ne vise PAS un « AgentPanelView » — il n'en existe aucun.** Le
  libellé du catalogue désigne le panneau de chat IA réel, **`Views/AiChatView`**
  (titre « MOTO AI », `KindFor` → `"aichat"`). C'est lui que `ap_width`/`ap_height`
  dimensionnent, et `ap_dock` place son dock via le mécanisme **existant**
  `_panelsSwapped` + `ApplySidePanelLayout` (pas de 2e système de dock).
  `cp_*` pilote `CollabPanelView` : `cp_width` sa largeur, `cp_dock` son ancrage
  gauche/droite (colonne centrale et marge basse conservées).
  **Restent inertes :** `ap_button`/`cp_button` (bouton de barre de statut
  inexistant), `ap_limit_width`/`ap_max_width` (« contenu centré » : le chat
  occupe toute la largeur, seules les bulles ont une borne figée de 420 px),
  `ap_flexible` (exigerait de désactiver une poignée de redimensionnement qui
  fonctionne), `dp_dock` (le panneau Debug n'est jamais rendu visible — le seul
  écran Debug est une fenêtre séparée, hors du `RootGrid`).

- **`terminal_*`** (10 clés sur 23) : dock Terminal du bas (`TerminalPanelView` +
  `TerminalService`). Mappage `Moto.Editor/Settings/TerminalSettings.cs`,
  appliqué par `ApplyTerminalSettings(s)` depuis `ApplyLayoutSettings` **et**
  `SettingsWindow.RealSettingChanged` (préfixe `terminal_`). Câblés :
  `terminal_font_size` / `terminal_font_family` (ressources
  `TerminalFontSize`/`TerminalFontFamily` en `DynamicResource` — touchent d'un
  coup les lignes du `DataTemplate` et le champ de saisie),
  `terminal_default_height` (hauteur du dock, avec garde `_appliedTerminalHeight`
  pour ne pas écraser le geste de la poignée de redimensionnement),
  `terminal_max_scroll_lines` (trim dans `MainViewModel.OnTerminalOutput`,
  0 = illimité), `terminal_audible_bell` (`Console.Beep()` sur le caractère
  BEL, retiré de la ligne affichée). Le clamp du geste
  `OnBottomDockResizePanUpdated` est passé de `120..360` (figé avant catalogue)
  aux bornes déclarées `100..1200`.
  **2e lot (01/10, environnement du shell)** — tous résolus par
  `MainViewModel.StartTerminal` (unique point de démarrage) et transmis à
  `TerminalService.Start(dir, TerminalStartOptions)` :
  - `terminal_shell` : `cmd.exe` / `powershell.exe` / `bash.exe -i` (vérifié en
    pipe : powershell fournit déjà son invite, bash sans `-i` non) —
    « System » (défaut) = comportement historique du service ;
  - `terminal_working_dir` : profil utilisateur si pas de projet, dossier projet
    pour le défaut « Current Project Directory », « Home » force le profil ;
    « Custom » **sans clé compagnon de chemin au catalogue** → avertissement
    affiché dans le terminal + répertoire projet conservé (jamais de dossier
    inventé) ;
  - `terminal_env_vars` : JSON clé-valeur parsé (`System.Text.Json`) et injecté
    dans `psi.Environment` ; JSON invalide = rien d'appliqué + erreur annoncée
    dans le terminal ;
  - `terminal_detect_venv` : probe `.venv`/`venv`/`env` dans le répertoire de
    départ et envoie `call …\activate.bat` (cmd) ou `& '…\Activate.ps1'`
    (PowerShell — exécuter activate.bat depuis PowerShell lance un cmd enfant et
    n'active rien) ; bash = non appliqué (activation Windows non portable vers
    un shell POSIX) ;
  - `terminal_breadcrumbs` : titre de l'en-tête = répertoire RÉEL du shell
    démarré en segments (`C: › Users › …`), via
    `MainViewModel.TerminalTitle` (« Terminal » tant qu'aucun shell n'a
    démarré — afficher un chemin là serait prétendre un shell inexistant).
  **Famille TERMINÉE : 10 opérantes, 13 inertes (raison par clé), 0 à câbler.**
  **Restent inertes :** `terminal_font_weight` (MAUI 8 n'a pas de `FontWeight`
  sur `Label`), `terminal_cursor_*` / `terminal_alternate_scroll` (aucun
  émulateur VT — sortie = `CollectionView` de lignes, seul le `Entry` de saisie
  a un curseur, natif WinUI non configurable), `terminal_option_as_meta`
  (sémantique macOS, app Windows), `terminal_copy_on_select` /
  `terminal_keep_selection_on_copy` (la sortie n'est pas sélectionnable),
  `terminal_open_links_mouse` (aucune détection de liens),
  `terminal_default_width` (dock du bas pleine largeur), `terminal_show_scrollbar`
  (pas de `ScrollBarVisibility` exposé sur `CollectionView` en MAUI 8),
  `terminal_scroll_multiplier` (pas de configuration du pas de molette),
  `terminal_thread_init_cmd` (le « thread terminal » n'existe pas dans le code),
   `terminal_min_contrast` (seuil **APCA** — algorithme Myndex précis ; une
   approximation changerait les couleurs du thème au nom d'un standard non
   réellement calculé = réglage « affichant faux », interdit — câbler exige le
   référentiel officiel).

- **`git_*`** (4 clés sur 16) : famille « Version Control »
  (`SettingsCatalog.Extensions.cs`), mappage `Moto.Editor/Settings/GitSettings.cs`.
  Avant ce chantier, **aucune des 16 clés n'était lue** hors catalogue. Câblés :
  - `git_integration` : gâchette d'ouverture de la fenêtre « Git » —
    `MainPage.OpenSpecializedWindow` (case `"git"`, qui sert palette,
    bouton et démarrage) refuse d'ouvrir quand décoché, et
    `ApplyLayoutSettings` **ferme** la fenêtre déjà ouverte au moment du
    changement (sinon : réglage décoché, panneau visible = affichage faux).
    Portée « panneau » réelle ; gutter/blame annoncés **n'existent pas**
    (voir inertes) ; les indicateurs de l'explorateur restent sous `pp_git_*`
    (une seconde gâchette sur les mêmes afficheurs = deux interrupteurs
    concurrents) ;
  - `git_path_style` : libellé des trois listes du panneau —
    `GitChangeNode.DisplayPath`, « File Name First » (défaut déclaré) =
    `Nom (dossier)`, « Path First » = chemin complet « / » (l'affichage
    historique). La clé `Path` (envoyée à `git`) n'est jamais réécrite ;
  - `git_stage_restore_buttons` : colonne des boutons stage/restore pilotée
    par `GitChangeNode.StageButtonsColumnWidth` (mécanisme `ColumnDefinition
    Width` + INPC, comme `FileNode.GitColumnWidth`). ⚠️ **Divergence
    documentée** : le descriptif dit « sur les hunks de diff », or **aucun
    hunk n'existe** — câblé sur les seuls widgets réels (boutons PAR FICHIER,
    même rôle), même décision que `ap_*` ;
  - `git_diff_base` : base du « Diff du projet » — « Head » (défaut) =
    `git diff` sans argument (inchangé), « Default Branch » =
    `GitService.GetDefaultBranchAsync()` (`git symbolic-ref
    refs/remotes/origin/HEAD`), base annoncée dans la ligne de statut ;
    branche par défaut introuvable (dépôt local sans remote) = repli sur HEAD
    + message explicite, jamais une branche devinée. `GetDiffAsync` accepte
    désormais une ref SEULE (`git diff {ref}`) — avant, une ref seule
    retombait silencieusement sur `git diff` sans argument.
  **Famille TERMINÉE : 4 opérantes, 12 inertes (raison par clé), 0 à câbler.**
  **Restent inertes (12) :** `git_gutter_visibility` / `git_gutter_debounce`
  (le gutter de `CodeEditorView` n'a que des numéros de ligne ; aucune commande
  git ne calcule un statut **par ligne**, le seul overlay est une TODO vide),
  les 7 `git_blame_*` (0 commande `git blame` dans le produit, aucune vue de
  blame, `GitCommit` sans auteur — la doc annonce le blame « À venir (v1.0) »),
  `git_branch_author` (pas de branch picker : le bouton 🌿 écrit les noms dans
  la statut bar ; `CheckoutAsync` sans appelant ; pas de donnée auteur),
   `git_diff_full_file` (aucun visualiseur de diff : le clic ouvre le fichier
   ou déplie des résumés `+n −m` — rien à basculer), `git_hunk_style` (aucun
   rendu de hunks).

- **`agent_*`** (9 clés sur 11 — famille TERMINÉE) : famille « Version
  Control/IA » à deux visages. Les **8 clés « IA Locale »** (`agent_engine`,
  `agent_model`, `agent_num_ctx`, `agent_max_steps`, `agent_max_minutes`,
  `agent_tool_mode`, `agent_thought`, `agent_verify_command`) étaient **déjà
  lues** par `Moto.Core/Moto.AI/Autonomy/V2/AgentV2Settings.cs` — opérantes,
  effet pris au **prochain run** de l'agent (`AgentV2Runner.ExecuteAsync`
  relit les settings à chaque run, pas en direct). La 9e, `agent_font_size`
  (int 8..30, défaut 13), a été câblée par ce lot : mappage
  `Moto.Editor/Settings/AgentSettings.cs`, ressource `AgentFontSize` posée sur
  `AiChatView` par `ApplyAgentAndCollabPanelSettings` (patron identique à
  `terminal_font_size` : défaut statique dans le XAML + écriture par code en
  `DynamicResource` sur les 3 éléments de texte — bulles et saisie). Le défaut
  déclaré (13) est **identique** à `FontSizeBody` : réglage intact = aucun
  pixel ne bouge.
  **⚠️ CORRECTION TRANSVERSE (même commit)** : le verrou `RealEffectKeys`
  (`SettingsWindowView.xaml.cs`) n'invoquait `RealSettingChanged` que pour 17
  clés — les dispatches `tb_*`, `gp_*`, `git_*`, `pp_*`, `sb_*` et
  `terminal_*` du handler MainPage **n'arrivaient jamais en direct** (effet
  uniquement au démarrage/retour de plein écran). Rattrapées (38 clés de
  plus, toutes déjà comptées opérantes) + dispatch `pp_`/`sb_` et `agent_`
  ajoutés. Sans ce verrou, un réglage qui ne bouge pas au changement se lit
  exactement comme un réglage inerte.
  **Restent inertes (2) :** `agent_skills` et `agent_sandbox` — boutons
  `Action` du catalogue : le déclencheur `SettingItem.ActionRequested` n'a
  **aucun abonné** dans tout le dépôt, et les écrans annoncés (installation de
  skills, permissions du sandbox de l'agent) **n'existent pas** (faux amis
  écartés : `ClaudeShellViewModel` = 4 skills de démo codés en dur ;
  `SandboxEngine` = copie du projet pour `run.sandbox`). Câbler = construire
  la fonctionnalité.

- **`platform_*`** (1 clé sur 8 — famille TERMINÉE) : catégorie « Agent /
  Platform Engine » (`SettingsCatalog.Platform.cs`). Seule
  `platform_auto_detect` est réellement câblée — et sa lecture
  (`MainPage.Panels.LoadWorkspace`) a été **corrigée** par ce lot : elle
  appelait `GetBool("platform_auto_detect")` sans défaut déclaré → `false`
  sur install neuve alors que la fenêtre Réglages affiche « ON » (affichage
  faux). Mappage `Moto.Editor/Settings/PlatformSettings.cs`, dispatch live
  `platform_` (relance l'analyse si activée + projet ouvert).
  **Restent inertes (7) — la chaîne produit est morte en amont (vérifié) :**
  `PlatformDetector.Analyze` ne remplit ni `Detections` ni `Proposals`
  (« 0 portage(s) proposé(s) » en permanence), `BuildProposal` et
  `AttachContinuousDetection` n'ont aucun appelant, `ApplyAsync` (bouton «
  Générer ») est injoignable tant que la liste est vide →
  `platform_include_linux`, `platform_generate_ci`, `platform_ci_provider`,
  `platform_auto_validate`, `platform_incremental_validate`,
  `platform_avalonia_linux`, `platform_smart_detect`. Assigner les
  propriétés `PlatformEngine` sans consommateur atteignable = câblage
  cosmétique (aucun effet observable), interdit.

- **`search_*` + `seed_search_from_cursor`** (1 clé sur 8 — famille TERMINÉE) :
  catégorie « Recherche & Fichiers », section « Recherche »
  (`SettingsCatalog.cs:95-102`). Seule `search_include_ignored` est câblée —
  elle pilote le filtrage `.gitignore` du panneau de recherche de fichiers,
  qui possède **sa propre instance** de `FileTreeService`
  (`SearchView.xaml.cs:24`), jamais configurée : la recherche affichait donc
  **toujours** les fichiers gitignorés, quel que soit le réglage (faux état).
  Correction de défaut au catalogue (false → true, précédent `tabs_file_icons`
  du 28/09) : le défaut rejoint le comportement réel → **aucun changement
  visible** au câblage, décocher reste un vrai choix. Mappage
  `Moto.Editor/Settings/SearchSettings.cs`, dispatch live `search_` →
  `SearchView.RefreshVisibility` (ré-applique les règles **et** rejoue la
  requête en cours, sinon invisible jusqu'à la prochaine frappe).
  **Restent inertes (7) — la recherche de CONTENU n'existe pas :**
  `search_whole_word`, `search_case_sensitive`, `search_smartcase`,
  `search_regex`, `search_wrap`, `search_center_on_match` (le find in file /
  find in files / barre Ctrl+F sont absents : l'éditeur WebView n'expose
  aucune fonction find, le Ctrl+F navigateur est désactivé, aucun FindBar,
  aucun raccourci Ctrl+F — seul existe le matching de **nom** de fichier,
  figé IgnoreCase) et `seed_search_from_cursor` (pré-remplissage exigeant
  l'API word-at-caret, inexistante ; seul `On Selection` serait
  implémentable, le mode par défaut `Always` resterait faux). ⚠️
  `search_wrap` a pour défaut déclaré **true** : une lecture sans second
  argument inverserait le réglage. ⚠️ Le mensonge produit préexistant
  `edit.search` (Ctrl+F annoncé « Recherche dans le fichier »
  `CommandPaletteEngine.cs:152`, qui ouvre le bandeau IA
  `MainPage.Routing.cs:60`) fait partie du même chantier. ⚠️ Doublon
  tranché : `file_finder_include_ignored` (Enum, même widget, section « File
  Finder ») reste inerte — **un seul interrupteur par widget**.

- **`auto_*`** (2 clés sur 7 — famille TERMINÉE) : catégorie « Général » /
  « Éditeur / Enregistrement auto » / « Éditeur / Indentation » /
  « Agent / Conversation / Documentation »
  (`SettingsCatalog.cs:32,67,68,82,276,280` + `SettingsCatalog.Extensions.cs:75`
  + `SettingsCatalog.Doc.cs:10` + `SettingsCatalog.AutoLink.cs:10,12,14`
  + `SettingsCatalog.Context.cs:14` + `SettingsCatalog.Platform.cs:10,19,21`).
  Câblées par ce lot : `auto_indent` (gate indentation auto dans le JS de
  `CodeEditorView` via `SetAutoIndent`) + `auto_update` (clé migrée depuis
  `editor.update.autoCheck` vers `auto_update` dans `AutoUpdateService`,
  alignement catalogue/UI). Déjà câblées avant : `doc_auto_update` (gate
  watcher FS dans `DocEngine`), `platform_auto_detect` (gate auto-analyse
  panneau Plateforme via `PlatformSettings`).
  **Restent inertes (11) — une raison par clé :**
  `auto_save`/`auto_save_delay` (fonctionnalité absente : pas de timer, pas de
  dirty flag), `auto_compact`/`auto_compact_threshold` (pas de folding), `auto_doc`
  (doublon de `doc_auto_update`), `autolink_enabled`/`autolink_auto_apply`/
  `autolink_scan_interval_sec` et `context_auto_apply` : **PAS câblables** —
  la machinerie annoncée (scan périodique + application auto) n'est branchée
  sur **aucun** point d'entrée UI atteignable (voir §5, famille
  AutoLink/Context),
  `platform_auto_validate`/`platform_incremental_validate` (chaîne produit
  morte). ⚠️ `auto_doc` et `doc_auto_update` = doublon sémantique, même
  catégorie, même défaut — seule `doc_auto_update` est active.

### Familles encore inertes, et pourquoi (NE PAS LES RETENTER SANS LIRE)

- **`preview_*` : les 6 clés.** Le concept d'« onglet aperçu » **n'existe nulle
  part** dans le code (`grep IsPreview|PreviewTab` = 0 hors catalogue). Ne pas
  confondre avec `LivePreviewView`, qui est un aperçu de **rendu** web.
  Câbler ces clés demande de **construire** le concept — c'est un chantier.
- **`tb_sign_in` / `tb_user_menu` / `tb_user_picture`** : **aucun compte
  utilisateur MOTO n'existe**. Le seul compte réel est GitHub OAuth, déjà
  servi par l'avatar existant.
- **`sb_*` (barre de statut)** : ★ **ITEM 1 CLOS (01/10, décision C)** — les
  4 clés `sb_project_panel`/`sb_terminal`/`sb_search`/`sb_debugger` ont de VRAIS
  boutons dans `StatusBarPanelView.xaml` (tranche 1), et les **4 puces de DONNÉES
  sont désormais câblées** (tranche 2, chacune avec une donnée RÉELLE, jamais
  inventée) : `sb_line_endings` (CRLF/LF détecté depuis `EditorDocument.Text`),
  `sb_language` (`CodeEditorView.LanguageDisplayName`, nom lisible depuis
  l'extension), `sb_cursor_position` (position poussée par le WebView via
  `CodeEditorView.SelectionChanged` puis convertie en ligne:colonne),
  `sb_encoding` (BOM détecté sur les premiers octets : UTF-8 BOM / UTF-16 LE /
  UTF-16 BE, sinon « UTF-8 » = défaut du chargeur). `sb_diagnostics`/`sb_active_file`
  étaient déjà câblées avant. Couverture totale de l'item 1 : 90 → 94/332 (28,3 %).
  Reste `op_button` → item 3 (outline).
- **`op_*` (Outline Panel) : LES 5 CLÉS NE SONT PAS CÂBLABLES — `OutlinePanelView`
  N'EXISTE PAS** dans le dépôt (recherche complète faite le 01/10). Il n'y a
  aucun panneau « outline » (vue symboles) à configurer. C'est l'item 3 de la
  feuille de route (décision C) — reste à construire, `op_button` compris.
- **`gp_button`, `cp_button`, `ap_button`** : ★ **FAITS (01/10, tranche 1)** —
  les 3 boutons existent dans `StatusBarPanelView.xaml` (Git, Collab, IA),
  visibilité par `StatusBarSettings`, action réelle (`OpenSpecializedWindow("git")`,
  `OnActivitySelected("collab")`, `OnActivitySelected("ai")`). Seul `op_button`
  reste (panneau outline inexistant, item 3).
- **`pp_count_badge`** : annonce un « nombre de terminaux » alors qu'il n'y a
  **qu'un seul** terminal.
- `tb_branch_icon`, `tb_worktree`, `tb_onboarding`, `tabs_git_status`,
  `tabs_pinned_layout` : concept ou donnée inexistants.
- **`autolink_*` (3 clés) et `context_*` (4 clés) : PAS CÂBLABLES — vérifié le
  01/10, la machinerie n'est branchée sur AUCUN point d'entrée UI atteignable.**
  Les classes « réelles » (celles que l'UI référence) sont
  `Moto.Core.AI.AutoLink.AutoLinkEngine` et `Moto.Core.AI.Context.ContextEngine`
  (+ `ContextAnalyzer`) ; les doublons `Moto.Core.AI.Internal.AutoLinkEngine` et
  `Moto.Core.AI.Internal.ContextEngine` sont du **code mort** (0 appelant — la
  seule « référence » à `Internal.AutoLinkEngine` est dans le code GÉNÉRÉ par
  `AvaloniaLinuxGenerator`, qui importe d'ailleurs `Moto.Core.AI.AutoLink`).
  Mais même les classes réelles sont **invoquées nulle part** : le singleton
  `ContextEngine` est enregistré en DI (`MotoServiceCollectionExtensions.cs:262`)
  et **jamais résolu** ; les panneaux `AutoLinkPanel`/`ContextPanel` sont
  atteignables via la palette (`ai.autolink`/`ai.context`,
  `MainPage.Routing.cs:92-93`) mais ne font que basculer `IsVisible` — `Load()`
  n'a aucun appelant, `ApplyRequested`/`DismissRequested` aucun abonné. Câbler
  `autolink_enabled`/`autolink_auto_apply`/`autolink_scan_interval_sec`/
  `context_auto_apply` exigerait de **construire** la pipeline (déclencheur à la
  demande + timer périodique + remplissage + apply), une fonctionnalité, pas un
  câblage.

> **Constat de fond (01/10)** : une part notable du catalogue décrit une
> application qui n'existe pas encore. Ce n'est pas seulement du « câblage en
> retard » — certaines fonctionnalités annoncées n'ont **aucun support** dans
> le code. C'est un point à connaître pour juger le palier « vendable » :
> la règle « tout ce qui est annoncé fonctionne » ne se satisfait pas
> uniquement en câblant, elle demande aussi de **retirer ou d'assumer**
> les réglages sans support.
- **Bilan final du câblage (01/10)** — trois états :
  **FAITES** : `tabs_*` (9), `pp_*` (12), `tb_*` (4), `sb_*` (6 : diagnostics +
  active_file + 4 boutons d'action), `gp_*` (13 : + gp_button), `ap_*`/`cp_*`
  (7 : + ap_button/cp_button), `terminal_*` (10), `git_*` (4), `agent_*` (9),
  `search_*` (1), `auto_*` (2 : auto_update + auto_indent), `doc_*` (doc_folder
  câblé, doc_auto_update corrigé, doc_on_project_open déjà actif).
  **NON CÂBLABLES (fonctionnalité absente)** : `preview_*` (6), `op_*` (5 — pas de
  OutlinePanelView), `file_*` (7 — pas de Quick Open ni d'indexeur ; `file_finder_include_ignored`
  = doublon de `search_include_ignored`), `lsp_*` (LSP absent), `collab_*` (audio absent),
  `autolink_*`/`context_auto_apply` (pipeline jamais déclenchée depuis l'UI), `sb_*`
  (4 restants : les 4 puces de données sb_language/sb_encoding/sb_line_endings/
  sb_cursor_position — donnée absente, tranche 2 de l'item 1).
  **MINCE** : `show_*` (seul `show_gutter` est câblable — le gutter existe dans le JS —
  mais exige un décalage CSS non vérifiable à l'œil ; `show_whitespace`/`show_edit_predictions`/
  `show_merge_conflict`/`show_turn_stats` n'ont aucun rendu).
  ⚠️ **Les préfixes ne suivent PAS les catégories affichées** dans la fenêtre
  Réglages (ex. la catégorie « Terminal » n'utilise pas `term_` mais
  `terminal_`). Toujours inventorier par préfixe RÉEL plutôt que de le deviner
  — une recherche sur un préfixe supposé renvoie 0 résultat et laisse croire à
  tort qu'il n'y a rien à faire.

- **Répartition complète par catégorie** (332 clés) : Fenêtre & Layout 50,
  Panneaux 44, Agent 33, AI 31, Éditeur 26, Terminal 22, Apparence 19,
  Version Control 17, Recherche & Fichiers 17, Général 14, IA Locale 11,
  Collaboration 10, Langages & Outils 9, Marketplace 6, Developer 6,
  Débogueur 5, Débutant 4, MCP 3, Raccourcis 3, Network 2.

### ★ Décision C (01/10, Tom) — construire les fonctionnalités manquantes

Le câblage est **épuisé**. Tom a choisi de **construire** les fonctionnalités
décrites par les réglages inertes, plutôt que de les retirer (A) ou de les
marquer « à venir » (B). Ordre de construction suggéré, du plus petit au plus
lourd (chaque item = une fonctionnalité réelle à développer, PAS un câblage) :

1. **Boutons de barre de statut** — ✅ **ITEM 1 CLOS (01/10)** : les 7
   boutons d'ACTION sont construits et câblés (`sb_project_panel`,
   `sb_terminal`, `sb_search`, `sb_debugger` + `gp_button`/`cp_button`/
   `ap_button`) — `StatusBarPanelView.xaml` a maintenant de vrais boutons
   (patron `Border`+`Label`+`TapGestureRecognizer`), visibilité par
   `StatusBarSettings`, action réelle branchée dans `MainPage` (toggle
   explorateur/recherche/IA/collab/terminal, fenêtres « debug »/« git »).
   ✅ **TRANCHE 2 FAITE (01/10)** : les 4 puces de DONNÉES sont câblées avec
   de VRAIES données — `sb_line_endings` (CRLF/LF depuis `EditorDocument.Text`),
   `sb_language` (`CodeEditorView.LanguageDisplayName`), `sb_cursor_position`
   (`CodeEditorView.SelectionChanged` → ligne:colonne), `sb_encoding` (BOM).
   Couverture 90 → **94/332 (28,3 %)**. Reste `op_button` (item 3, outline).
2. **Onglets aperçu** — ✅ **CŒUR FAIT (01/10)** : `EditorDocument.IsPreview`
   (italique via DataTrigger), `OpenFilePath(asPreview)` remplace l'aperçu
   existant, permanence à la 1re édition, explorateur → aperçu. Câblées :
   `preview_enabled` + `preview_project_panel`. Reportées (features inexistantes) :
   `preview_file_finder` (item 5), `preview_multibuffer`, `preview_code_nav`/`keep_on_nav`.
3. **Panneau outline (vue symboles)** — ✅ **FAIT (01/10)** : `OutlineExtractor`
   (motifs réels C#/Python/JS), `OutlinePanelView` (dock op_dock), chip barre de
   statut (op_button), auto_reveal (SetCursorLine). Les 5 clés `op_*` lues.
   ⚠️ `op_auto_fold`/`op_indent_guides` sont LUS MAIS SANS EFFET (liste plate,
   pas d'arbre) — faux positifs de la couverture, à corriger.
4. **Auto-save** — ✅ **FAIT (01/10)** : `EditorDocument.IsDirty`, `MarkDirty`
   (debounce « After Delay »), `TrySaveOnFocusChange` (« On Focus Change »),
   `SaveDocumentAsync`. Câblées : `auto_save` + `auto_save_delay`.
5. **Quick Open / file finder** — ⏳ **PARTIEL (01/10)** : `file_finder_icons`
   câblé (icônes dans SearchView). Reste : `file_scan_*`/`file_types` (aucun
   indexeur configurable — Quick Open complet à construire) ;
   `file_finder_include_ignored` = doublon de `search_include_ignored` ;
   `file_finder_skip_focus` = comportement déjà d'origine.
6. **Git blame** — ⏳ **PARTIEL (01/10, reprise Tom)** : `GitService.GetBlameAsync`
   (parse `git blame --porcelain`, hash/auteur/résumé par ligne, réels) + puce
   `BlameLabel` dans la barre de statut au mouvement du curseur. Câblées :
   `git_blame_enabled`/`git_blame_delay`/`git_blame_commit_summary`. Reste inerte
   (rendu INLINE dans l'éditeur) : `git_blame_location`/`padding`/`min_column`/`avatar`.
7. **Git gutter** — ✅ **FAIT (01/10, reprise Tom)** : `GitService.GetChangedLineNumbersAsync`
   (parse `git diff HEAD --unified=0`) + div `#gitGut` dans le gutter WebView
   (`setGitLines` + sync scroll). Câblées : `git_gutter_visibility`/`git_gutter_debounce`.
8. **LSP** — 🔄 **RÉÉCRIT (01/10, décision B de Tom)** : `RoslynLanguageServerClient.cs`
   (~200 lignes) réécrit contre `OmniSharp.Extensions.LanguageClient` 0.19.9 (API réactive
   `IRequestProgressObservable`), réflexion sur les assemblies (aucun nom inventé). Build
   vert 0/476, app démarre. Câblé : `LanguageServerManager` en DI + `WireLsp` +
   `InitializeLsp` + `LspSettings` (`lsp_enabled`/`lsp_completions`/`lsp_diagnostics`/
   `lsp_highlights`, défauts DÉCLARÉS) + `RealEffectKeys`. ⚠️ **PAS encore fonctionnel de
   bout en bout** : l'intégration éditeur (`OpenDocumentWithLspAsync`/
   `UpdateDocumentWithLspAsync`/`RequestCompletionsAsync`) n'a AUCUN appelant dans le flux
   d'édition, et AUCUN serveur Roslyn n'est fourni/résolu au runtime. Chantier suivant :
   brancher ces méthodes sur EditorChanged/curseur + fournir le serveur.
9. **Pipeline AutoLink/Context** — ⏳ **PARTIEL (01/10, reprise Tom)** : `ContextEngine`
   résolu (DI) + scan périodique (`autolink_enabled`/`autolink_scan_interval_sec`) +
   `SetActiveFile` à chaque chargement + auto-apply réelle (`context_auto_apply`,
   `ContextEngine.Apply` écrit sur disque). Câblées : 3 clés. `autolink_auto_apply`
   reste inerte (pipeline `AutoLinkEngine` séparée, non branchée).
10. **Audio de collaboration** — ⏸️ **BUILD FROM SCRATCH (01/10)** : les 5 clés
    `collab_*` décrivent des APPELS vocaux (micro+streaming+périphériques). Le seul
    `VoiceEngine` est TTS/STT, pas un moteur d'appels. Aucune capture/streaming
    audio, aucune énumération de sortie. Chantier multi-sessions (capture + WebRTC
    + gestion d'appel), le plus lourd.

⚠️ **Pour chaque item : relire la raison d'inertie dans `CLAUDE.md` bug #4**
(elle donne les preuves fichier:ligne de l'absence de support). Une fois la
fonctionnalité construite, câbler la clé correspondante avec la méthode
éprouvée (`XxxSettings` + défaut DÉCLARÉ + `ApplyLayoutSettings` + dispatch
live + clé dans `RealEffectKeys`).

## ★ Décision visuelle (01/10, Tom) — thème CHAUD « Claude Code »

Après la décision C (fonctionnalités), Tom a demandé un focus **visuel** :
s'inspirer FORTEMENT de VS Code / Zen / Claude Code / Cursor / Cherry Studio /
Goose, consulter `Docs/inspirations` (deux dossiers identiques `MOTO-Editor-wt`
et `MOTO-Editor-vendable`) et les dépôts GitHub listés. **La référence clé est
`Docs/inspirations/IntefaceClaudeExtended.txt`** (1342 lignes) : une maquette
HTML/CSS complète « MOTO Editor — Interface type Claude Code (v4) ».

**Décisions de Tom (01/10)** : (1) **thème CHAUD complet** — accent orange
`#D97757` + fonds bruns (INVERSE la décision D2 « bleu #007ACC » du 22/09,
l'ancien bleu est conservé sous le jeton `AccentWarm`) ; (2) commencer par le
**Chat IA**.

**Réalisé (4 lots, build 0/476, app lancée 9 s sans exception)** :
- `156acd2` thème chaud : `Accent` #D97757, fonds #262421/#1e1c1a/#2e2c28/
  #3b3833, textes #ece9e4/#a09b93, variantes hover/pressed dérivées de
  #D97757/#E08B6D.
- `6ff0306` réconciliation : 12 neutres codés en dur → jetons (suivent le thème).
- `cf62569` micro-interaction : press-scale 0.94 des boutons icône
  (`.icobtn:active{transform:scale(.94)}` de la maquette).
- `487c495` bulles de chat chaudes (user #3B3833, IA #2A2825).

**Constat honnête sur le Chat IA** : composeur (« @ … / … », modèle, contexte,
slash), blocs de code (en-tête + Appliquer + Copier) et bulles sont DÉJÀ en
place. Les manques restants ne sont pas purement visuels : **Thinking + appels
d'outils** = fonctionnel (le chat passe par `OllamaClient` texte brut ; les
champs `LlmMessage.Thinking`/`ToolCalls` parsés par `OllamaChatClient` ne sont
utilisés que par la boucle Agent, pas par le chat — afficher exige de basculer
le chat vers le client à outils) ; **heatmap** = données absentes (un graphe de
contributions serait factice, interdit) ; **puces de code inline** = limitées en
MAUI (`Span` sans coin arrondi, `FormattedText` non bindable).

## Bridge Xeno (MOTO-Xeno-Desktop) — contexte durable

- **Orchestrateur bridge HTTP** : `start_orchestrator.exe` dans
  `E:\Corpus\MOTO-Xeno-Desktop\dist\`, port **127.0.0.1:5001**, dashboard
  `/dashboard` (« OS Cognitif v5.1 »). 21 agents (8 système + 13 cognitifs, dont
  `UIAgent`[Designer] et `DesignAgent`[DesignSynthesizer] pertinents pour le
  visuel), 15 modules (tiers 30-45), 20 règles GOV001-020.
- **Endpoints qui marchent** : `/api/agents`, `/api/runtime`,
  `/api/cognitive/tier46/system-cognition-bridge-final` (liste des agents).
- **Endpoints cassés** : `/api/cognitive/tier47/meta-forecast` (500) et
  `/api/cognitive/tier45/product-viability` (`ModuleNotFoundError: modules.tier45`).
  ⚠️ **DÉCISION DE TOM (01/10)** : NE PAS réparer `meta-forecast` — « il ne sert
  qu'à prédire, pas à économiser un seul token ». Laisser tel quel.
- MOTO Editor a déjà l'intégration : `Moto.Core/Integration/XenoGateway.cs`,
  `Moto.Core/Moto.AI/XenoFallbackBridge.cs`, `Controls/XenoFeedbackControl`,
  `Views/AboutXenoView`, etc.

## 6. Méthode de travail — leçons apprises

- **Vérifier avant de croire.** Ne jamais déclarer « cette brique marche » sur
  la seule lecture du code : plusieurs bugs réels y étaient invisibles.
- **Un réglage non appliqué immédiatement se lit comme inerte.** Si un
  changement de réglage n'agit qu'au redémarrage (ou au retour de plein écran),
  l'utilisateur conclut qu'il ne marche pas. C'est un défaut réel — corrigé une
  fois sur les `tb_*`.
- **`ApplyLayoutSettings()` tourne AVANT que les panneaux existent** (elle est
  appelée par `WireSettings()`, alors que `WirePanels()` vient après dans le
  constructeur). Appliquer un réglage sur `_aiChatPanel`/`_cortexPanel`/… y lève
  donc une **`NullReferenceException` au démarrage** : l'app ne se lance plus du
  tout. Coûté une fois le 01/10 sur `ap_height`. Parade : garde `is null` **et**
  second appel après `WirePanels()`. **Seul le contrôle 4 du garde-fou
  (« démarrage réel ») attrape ce bug** — la lecture du code ne le voit pas.
- **Le garde-fou ne juge pas la beauté.** `scripts/visual-lot-verify.ps1`
  prouve qu'un lot ne casse rien (0 erreur, avertissements ≤ ligne de base,
  périmètre, lancement réel sans exception) — **pas** que le résultat est joli.
  Seul l'œil de Tom juge.
- **Ne pas faire tourner deux chantiers dans le même arbre de travail.**
  Constaté le 01/10 : les fichiers de l'un font échouer le garde-fou de
  l'autre, les mesures de couverture se mélangent, et un commit peut avaler le
  code de l'autre session. **Utiliser des worktrees séparés.**
- **Commits** : chemins explicites, **jamais `git add -A`**. Ne pas pousser
  sans accord explicite de Tom. Identité Git par **variables d'environnement**
  (`GIT_AUTHOR_NAME`/`GIT_COMMITTER_NAME` = `Tomoushie`), jamais `git config`.
- **Fichiers à ne jamais toucher** : `Moto.Core/Moto.AI/Generation/CodeApply.cs`
  et `Fichierdetest.txt` ne viennent pas des sessions IA — les laisser hors des
  commits.

## 7. État de la barre de titre bleue — RÉSOLUE

Longtemps le chantier le plus coûteux du projet. **La conclusion compte** :

- Le diagnostic « c'est Windows/DWM qui peint la bande » était **FAUX**.
  C'était **MAUI** : `AppTitleBarContainer` (hauteur 32) + une marge haute de
  32 sur `ContentGrid`.
- **Résolu le 25/09** (commit `5ac1136`) par
  `Platforms/Windows/MauiTitleBarBand.cs`, qui épingle ces deux réglages.
- **6 tentatives antérieures avaient échoué**, dont `DwmSetWindowAttribute` :
  Windows renvoyait `hr=0` (donc **acceptait**) et peignait quand même
  par-dessus. **Ne plus retester ces API** — c'est établi, plus une hypothèse.

## 8. Sécurité

- Un PAT GitHub a été partagé par le passé → **révoqué**. Dépôt public : ne
  jamais redemander ni stocker de PAT en clair.
- Aucune action distante sans l'accord de Tom.

## 9. Où lire la suite

- `CLAUDE.md` — référence d'architecture exhaustive (le plus à jour).
- `QWEN.md` — mémoire de Qwen (⚠️ **plusieurs chiffres y sont périmés** :
  il annonce 297 réglages et « seuls 4 appliqués », et présente la barre de
  titre comme un chantier ouvert ; voir §5 et §7 ci-dessus pour l'état réel).
- `Docs/design/Couverture-reglages.md` — rapport de couverture des réglages.
- `Docs/README.md` — vitrine et état réel du projet.

---

**Note d'usage** : ce fichier est vivant. Quand un fait change (nouveau commit,
problème clos, chiffre remesuré), le mettre à jour ici plutôt que de laisser
l'information se perdre dans l'historique git.
