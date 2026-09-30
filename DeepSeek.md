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
`TitleBarSettings.cs` — chacun expose `DeclaredBool/DeclaredInt/DeclaredString`
qui lit `SettingsCatalog.ById(id).Default`.

### La méthode qui marche (à reproduire)

1. Un fichier `XxxSettings.cs` : mappage centralisé, défauts **déclarés**.
2. **Un point d'accroche unique** : `MainPage.ApplyLayoutSettings()`
   (`MainPage.UI.cs`), appelé au démarrage, à **chaque changement de réglage**
   et au retour de plein écran. Il appelle déjà `StatusBar`, `EditorPane`,
   `MenuBar`, `ExplorerPanel`.
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

### Familles encore inertes, et pourquoi (NE PAS LES RETENTER SANS LIRE)

- **`preview_*` : les 6 clés.** Le concept d'« onglet aperçu » **n'existe nulle
  part** dans le code (`grep IsPreview|PreviewTab` = 0 hors catalogue). Ne pas
  confondre avec `LivePreviewView`, qui est un aperçu de **rendu** web.
  Câbler ces clés demande de **construire** le concept — c'est un chantier.
- **`tb_sign_in` / `tb_user_menu` / `tb_user_picture`** : **aucun compte
  utilisateur MOTO n'existe**. Le seul compte réel est GitHub OAuth, déjà
  servi par l'avatar existant.
- **`sb_*` (barre de statut) — vérifié le 01/10, plus précisément que ce qui
  était écrit avant.** Les 10 clés prétendent configurer des **boutons** de la
  barre de statut. Or `StatusBarPanelView.xaml` ne contient **AUCUN de ces
  boutons** : ses seuls éléments nommés sont `StatusLabel`, `RightChips`,
  `ErrorsLabel`, `WarningsLabel`, `StateChips`, `SandboxLabel`, `LockedLabel`,
  `AiStatusLabel`. Le tri réel :
  - **Pas câblables — la donnée n'existe pas** : `sb_encoding` et
    `sb_line_endings` (**aucune notion d'encodage ni de fins de ligne** dans
    `Moto.Editor` : vérifié, 0 occurrence de `LineEnding`/`EOL`), et
    `sb_cursor_position` (l'éditeur principal est `CodeEditorView`, un
    **WebView** — la position du curseur vit côté JavaScript, pas dans un
    modèle C# consultable).
  - **Pas câblables — le bouton n'existe pas** : `sb_project_panel`,
    `sb_language`, `sb_terminal`, `sb_debugger`, `sb_search` (ces actions
    existent ailleurs dans l'app, mais aucun bouton de barre de statut ne les
    porte). Les câbler = **créer** les boutons, donc ajouter une
    fonctionnalité, pas câbler un réglage.
  - **Partiellement câblable** : `sb_diagnostics` (les compteurs existent déjà
    — `ErrorsLabel`/`WarningsLabel`) et `sb_active_file` (le nom du fichier
    actif est disponible via `MainViewModel.SelectedDocument`), à condition
    d'ajouter l'élément d'affichage manquant.
- **`op_*` (Outline Panel) : LES 5 CLÉS NE SONT PAS CÂBLABLES — `OutlinePanelView`
  N'EXISTE PAS** dans le dépôt (recherche complète faite le 01/10). Il n'y a
  aucun panneau « outline » (vue symboles) à configurer.
- **`gp_button`, `cp_button`, `ap_button`, `op_button`** : prétendent tous
  configurer « un bouton dans la barre de statut », qui n'existe pas (voir
  `sb_*` ci-dessus). Même conclusion : créer le bouton serait un ajout de
  fonctionnalité, pas un câblage.
- **`pp_count_badge`** : annonce un « nombre de terminaux » alors qu'il n'y a
  **qu'un seul** terminal.
- `tb_branch_icon`, `tb_worktree`, `tb_onboarding`, `tabs_git_status`,
  `tabs_pinned_layout` : concept ou donnée inexistants.

> **Constat de fond (01/10)** : une part notable du catalogue décrit une
> application qui n'existe pas encore. Ce n'est pas seulement du « câblage en
> retard » — certaines fonctionnalités annoncées n'ont **aucun support** dans
> le code. C'est un point à connaître pour juger le palier « vendable » :
> la règle « tout ce qui est annoncé fonctionne » ne se satisfait pas
> uniquement en câblant, elle demande aussi de **retirer ou d'assumer**
> les réglages sans support.
- **Restent à faire** (préfixes réels, mesurés le 01/10 sur les 332 clés) :
  `terminal_*` (23), `git_*` (16), `gp_*` (15), `pp_*` (13, fait),
  `tabs_*` (11, fait), `tb_*` (10, fait), `sb_*` (10, 2 faits),
  `agent_*` (9), `platform_*` (8), `ap_*` (7), `search_*` (7), `auto_*` (7),
  `file_*` (7), `preview_*` (6, toute la famille inerte), `op_*` (5, inerte),
  `show_*` (5), `lsp_*` (4), `context_*` (4), `doc_*` (4), `collab_*` (4)…
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

> **Constat de fond (01/10)** : une part notable du catalogue décrit une
> application qui n'existe pas encore. Ce n'est pas seulement du « câblage en
> retard » — certaines fonctionnalités annoncées n'ont **aucun support** dans
> le code. C'est un point à connaître pour juger le palier « vendable » :
> la règle « tout ce qui est annoncé fonctionne » ne se satisfait pas
> uniquement en câblant, elle demande aussi de **retirer ou d'assumer**
> les réglages sans support.

## 6. Méthode de travail — leçons apprises

- **Vérifier avant de croire.** Ne jamais déclarer « cette brique marche » sur
  la seule lecture du code : plusieurs bugs réels y étaient invisibles.
- **Un réglage non appliqué immédiatement se lit comme inerte.** Si un
  changement de réglage n'agit qu'au redémarrage (ou au retour de plein écran),
  l'utilisateur conclut qu'il ne marche pas. C'est un défaut réel — corrigé une
  fois sur les `tb_*`.
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
