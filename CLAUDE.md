# MOTO Editor — état durable du projet

Ce fichier n'est PAS un journal de changements (l'historique git le fait déjà
très bien) — c'est une carte de faits d'architecture et d'état ACTUELS,
coûteux à reconstruire (plusieurs agents, dizaines de lectures de fichiers).
Objectif : éviter de relancer une sonde multi-agents pour re-découvrir ce
qu'on sait déjà. À MAINTENIR À JOUR : quand une session confirme un fait
durable sur l'architecture (pas une préférence ponctuelle, pas un détail qui
change à chaque commit), ajouter/corriger la section concernée ici plutôt que
de laisser l'info dispersée dans des commits ou dans la mémoire de session.
Équivalent, côté Claude, du rôle que joue `QWEN.md` pour Qwen dans ce dépôt.

Dernier état des lieux complet : 02/09 (sonde à 4 agents, ~512k tokens,
195 lectures/greps — voir section Références pour rejouer le détail brut).

## Paliers de qualité de Tom

Échelle perso : cheap → faible → moyen → élevé → Commercial.
- **cheap → faible** : l'IA doit fonctionner (✅ acquis, panneau de chat réel
  avec Ollama) + la barre de titre bleue Windows doit disparaître (⏸️ en
  pause, cause réelle trouvée — voir section dédiée plus bas).
- **faible → moyen** : stabilité totale — tout ce qui existe doit fonctionner
  PARFAITEMENT (pas de fonctionnalité à moitié branchée). L'état des lieux du
  02/09 a trouvé plusieurs cas concrets qui violent ce palier dès aujourd'hui
  — voir "Bugs confirmés non corrigés" ci-dessous.
- **moyen → élevé** : polish visuel pour ressembler à un projet moderne type
  Zed/VS Code/Claude Code/ChatGPT (un cran en dessous).
- **élevé → Commercial** : les 420 réglages du catalogue implémentés ET
  fonctionnels (pas juste affichés/persistés).

## Architecture des panneaux (dock IA / Explorateur)

- **Système modulaire réel** (`AddFloatingPanel`, `MainPage.Panels.cs`) :
  redimensionnement, changement de côté, glisser-réordonner, glisser entre
  docks. 9 panneaux dessus : Platform, Cortex, Neural, Workspace, DebugPanel,
  AiChatView (panneau IA réel), PluginGallery, AnalyticsDashboard, Search (en
  overlay centré — seul cas qui n'a NI glisser-réordonner NI changement de
  côté NI migration entre docks, choix délibéré documenté dans le code, pas
  un bug).
- **FileExplorerView / SidebarView** : confirmé PAS sur ce système —
  mécanisme séparé et plus ancien (`ExplorerDockPanel` dans `MainPage.xaml`,
  poignée dédiée `ExplorerResizeHandle`, ré-implémentation à la main du même
  calcul que la poignée du dock IA au lieu de code partagé). Verdict de
  l'état des lieux : **surtout historique, pas un choix d'architecture
  voulu** — la coquille du dock Explorateur est aujourd'hui structurellement
  identique à celle du dock IA (zone fixe + corps dynamique), donc rien
  n'empêcherait techniquement de généraliser. La seule vraie raison
  (partielle) de les garder à part : Explorateur/Sidebar forment une PAIRE
  exclusive qui remplit toute une colonne (comme Accueil vs Éditeur), pas des
  outils secondaires empilables indépendamment — le modèle d'AddFloatingPanel
  (une carte fermable par panneau) ne colle pas tel quel à "toujours l'un des
  deux, jamais les deux", il faudrait un concept de "groupe exclusif" en plus
  pour migrer proprement.
- **AiHost/ChatHost/ThreadHost** : anciens stubs codés en dur dans
  `MainPage.xaml`. `AiHost` supprimé le 02/09 (remplacé par le vrai
  AiChatView). `ChatHost`/`ThreadHost` existent toujours, jamais câblés à
  rien de réel — voir bug #2 ci-dessous, ce n'est pas juste du code mort
  inerte, ça peut réellement gêner Tom.

## Bugs confirmés (état des lieux du 02/09)

Concerne directement le palier "moyen" (tout ce qui existe doit marcher
parfaitement). Rien ici n'est une hypothèse — chaque point est vérifié par
lecture directe du code.

1. ✅ **CORRIGÉ (02/09).** Double en-tête sur 2 panneaux. `PluginGalleryView`
   et `AnalyticsDashboardView` étaient bien sur le système modulaire
   (AddFloatingPanel fournit déjà un en-tête + bouton ✕ générique), mais
   gardaient en plus leur propre en-tête interne d'avant leur migration — à
   l'écran : titre en double, DEUX boutons ✕ qui marchaient tous les deux.
   Même correctif que celui déjà appliqué à `AiChatView` (en-tête interne
   retiré). Confirmé visuellement par Tom en app réelle.
2. ✅ **CORRIGÉ (02/09).** `ChatHost`/`ThreadHost` (étiquettes mortes "💬
   Chat"/"🧵 Threads", jamais reliées à rien de réel, qui réapparaissaient
   après un cycle Maximiser/Restaurer et pouvaient garder le dock IA ouvert
   pour rien) — stubs entièrement supprimés (`MainPage.xaml`,
   `MainPage.UI.cs`, `MainPage.Panels.cs`). Confirmé visuellement par Tom.
3. ✅ **CORRIGÉ (02/09), décision de Tom : sélecteur rendu honnête.** Le
   réglage `theme_mode` proposait Dynamic/Light/Dark alors que
   `SettingsApplier.cs` force "Sombre" quoi qu'on choisisse (pas un oubli —
   `ThemeService.SetLight()`/`FollowSystem()` existent réellement mais ont
   déjà été essayés et retirés fin août : aucune palette claire n'existe
   dans `MotoTheme.xaml`, ça causait du texte noir sur fond noir). Catalogue
   (`SettingsCatalog.cs`) changé pour n'offrir plus qu'un seul choix
   ("Dark"). Construire une vraie palette claire reste une option pour plus
   tard (gros chantier), pas retenue aujourd'hui. Confirmé par Tom.
4. **La fenêtre Réglages affiche 297 réglages, seuls 4 agissent vraiment.**
   `SettingsApplier.ApplyAll()` ne lit que 4 clés au total (thème, taille de
   police, minimap, diagnostics LSP) sur les 297 affichées dans la fenêtre —
   confirmé par le propre commentaire du code. C'est de loin le plus grand
   écart "affiché mais inactif" de l'app par rapport à la barre "moyen".
5. ✅ **CORRIGÉ (02/09).** Menu Réglages fantôme (`SettingsMenuView`, l'ancien
   menu avant la fenêtre flottante façon Zed — plus aucun bouton nulle part
   pour l'ouvrir depuis le 31/08, mais construit et abonné à
   `SettingChanged` pour rien). Construction + câblage retirés de
   `MainPage.xaml.cs`, exclu de la compilation dans le `.csproj` (même
   traitement que `SettingsPage`, superseded pas supprimé du disque).
   **Effet de bord trouvé ET corrigé le même jour** : le cas "openproviders"
   de son gestionnaire était le SEUL point d'entrée du dépôt vers
   `Pages/AiSettingsPage.xaml.cs` (config chiffrée des clés API OpenAI/
   Anthropic/Mistral + test de connexion Ollama) — vérifié : le catalogue de
   réglages ne fait QUE choisir le provider par défaut (`default_model`), il
   n'a aucun champ pour les clés elles-mêmes, donc ce n'était PAS un doublon
   (contrairement à l'hypothèse de départ). Un vrai nouveau point d'entrée a
   été ajouté : bouton "🔑 Clés API" dans la barre de titre de
   `SettingsWindowView`, événement `ApiKeysRequested` remonté à MainPage
   (même patron que `RealSettingChanged`, une ContentView n'a pas de
   Navigation propre). Confirmé par Tom, l'écran s'ouvre.
6. ✅ **CORRIGÉ (02/09).** Bouton "changer de côté" mort dans l'Explorateur
   (🡺 dans la barre d'outils de `FileExplorerView`, plus rien ne l'écoutait
   depuis l'arrivée du bascule global du menu engrenage). Plutôt que de le
   supprimer, câblé sur le même mécanisme (`ApplySidePanelLayout`) dans
   `MainPage.xaml.cs` — testé en jeu réel par Tom, fonctionne.

## Palette de commandes (Ctrl+Maj+P) — bug majeur trouvé ET corrigé (02/09)

Ne pas confondre 2 choses au nom proche :
- **La vraie palette, réelle et fonctionnelle** : `Views/CommandPaletteView.xaml.cs`
  + `Moto.Core.AI.Commands.CommandPaletteEngine` (catalogue statique de
  commandes + recherche floue). Câblée à `MainPage.OnPaletteCommandInvoked`,
  qui route vers `OnMenuCommanded` (préfixe `menu:`) ou `OnAiCommandSubmitted`
  (sinon).
- **`CommandPaletteService`** (4 fichiers `Services/CommandPaletteService*.cs`)
  — confirmé MORT et **supprimé du disque** (pas juste exclu) : 4 fragments
  ajoutés séparément sans classe de base commune, jamais raccordés, jamais
  utilisés par la vraie palette ci-dessus. `CommandPaletteHistoryService.cs`
  (historique + score flou, 129 lignes, complet et compile) reste sur le
  disque mais n'est câblé nulle part (ni DI, ni construit) — vraie
  amélioration possible un jour (tri par commandes récentes), pas urgent.

**Le vrai bug, sévère, trouvé le 02/09** : Ctrl+Maj+P ne faisait RIEN, et
c'était plus grave qu'un simple raccourci cassé. Cause réelle :
`ResolveExtensionServices()` (MainPage.Extensions.cs) — qui résout
`_commandPalette`, `_confirmationOverlay`, `_proactivePanel`, `_analytics`,
`_windowManager`, etc. via le conteneur DI — était appelée depuis
`InitializeMainPageExtensions()`, donc **dans le constructeur de MainPage**,
avant que `Handler`/`Application.Current.Handler` existent. `services`
valait donc `null`, la méthode sortait tout de suite (`return` anticipé),
et TOUS ces champs restaient `null` pour toujours. Aucune exception
visible : le `catch` de cette méthode n'écrivait que vers
`Debug.WriteLine` (invisible sans débogueur attaché) — corrigé pour écrire
aussi dans le vrai journal (`App.Breadcrumb`). Exactement la même famille
de bug que `AttachWindowsHotkey` (ci-dessous) et l'ancien
`SnapLayoutsHelper`/`ConfigureSnapLayouts` (déjà corrigé fin août) : du
code appelé depuis le constructeur de `MainPage` avant que la fenêtre/le
handler existent.

**Corrigé** : `ResolveExtensionServices()` déplacée dans `OnPageLoaded`
(MainPage.xaml.cs), où `Handler` est déjà garanti prêt. Confirmé par Tom en
app réelle (capture d'écran) : la palette s'ouvre, liste les commandes, ET
le panneau "Actions suggérées" (qui dépendait du même correctif,
`_proactivePanel`) est maintenant peuplé aussi — l'impact réel de ce bug
était plus large que la seule palette.

**Leçon retenue pour la suite** : ne plus déclarer "cette brique marche"
sur la seule lecture du code (comme fait une première fois par erreur ce
même jour) — vérifier l'état réel à l'exécution (ici, `_commandPalette`
valait `null` malgré un code de câblage qui semblait complet à la lecture).

**Effet de bord trouvé ET corrigé le même jour** : réparer `ResolveExtensionServices()`
a réveillé un 2e bug endormi — `_analyticsDashboard` était résolu DEUX fois
(une vraie fois par `WirePanels()`, enveloppée par `AddFloatingPanel` qui la
masque ; une 2e fois ici même, via DI + `AddMotoOverlay`, JAMAIS masquée,
exactement le même doublon fantôme déjà connu et retiré pour
`_pluginGallery`). Tant que `ResolveExtensionServices()` échouait en
silence, ce doublon ne s'exécutait jamais — dès qu'il a été corrigé, le
panneau Analytics s'est mis à couvrir tout l'écran au démarrage (repéré par
Tom, capture d'écran). Retiré, comme `_pluginGallery` l'avait été avant.
**Leçon** : corriger un bug de timing peut réveiller un doublon resté
invisible juste parce que le code cassé ne l'exécutait jamais — vérifier
l'écran après CHAQUE correctif de ce genre, pas seulement le comportement
visé.

**Palette : fermeture ajoutée (02/09)** — jusqu'ici, refaire Ctrl+Maj+P
était le SEUL moyen de la refermer (repéré par Tom). Ajouté : bouton ✕
(`CommandPaletteView.xaml`) + touche Échap (`OnWindowsPreviewKeyDown`,
même mécanisme que Ctrl+Maj+P).

**Ctrl+Maj+P lui-même** (`AttachWindowsHotkey`, MainPage.Extensions.cs) avait
EXACTEMENT le même problème de timing, corrigé le même jour de la même
façon (déplacé dans `OnPageLoaded`, reçoit `nativeWindow` déjà résolu au
lieu de le redemander à `Application.Current.Windows[0]` trop tôt — ça
levait un `ArgumentOutOfRangeException` avalé en silence par un `catch`
générique "le hotkey est optionnel").

## Réglages → IA Locale (02/09, depuis "Docs/Idées à implémenter.txt")

`Docs/Idées à implémenter.txt` (fichier de Tom) mélange une vision très
ambitieuse (fusion MOTO AI/Xeno-SSS∞, auto-modification de code en direct —
**non retenue, trop risquée pour être un vrai prochain pas**) et de vraies
petites idées faisables. Une a été construite le 02/09 : catégorie
**"IA Locale"** dans le catalogue de réglages (`SettingsCatalog.cs`) —
`ollama_endpoint`, `ollama_model`, `ollama_timeout_seconds`.

**Bug réel trouvé en construisant ça** : `AiSettingsPage` avait déjà des
champs Ollama (URL/modèle), mais ils écrivaient dans
`_fallbackEngine.ProviderManager` — un système totalement différent du
vrai moteur de chat local (`MotoAiKernel` → `OllamaClient`, qui construit
toujours `new OllamaClient()` sans jamais lire cette config). Changer ces
champs n'avait donc AUCUN effet sur une vraie conversation. Corrigé :
`OllamaClient` (le vrai, `Moto.Core.AI.Internal.OllamaClient`) lit
maintenant `SettingsEngine.Shared` dans son constructeur ; `AiSettingsPage`
lit/écrit désormais les mêmes clés — un seul endroit réel au lieu de deux
qui se contredisaient. **Limite connue** : un changement ne prend effet
qu'au prochain lancement de l'app (le kernel n'est construit qu'une fois
au démarrage) — pas grave pour un réglage rarement changé, mais à savoir.

**Doublon confirmé au passage** : 2 classes `OllamaClient` existent dans
le dépôt — `Moto.Core.AI.Internal.OllamaClient` (la vraie, utilisée par
`MotoAiKernel`) et `Moto.Core/Moto.AI/OllamaClient.cs` (namespace
`Moto.Editor.AI` — déjà mal rangé, comme d'autres fichiers de ce dépôt —
zéro appelant nulle part, confirmé par recherche complète). Pas supprimée,
juste notée ici.

## ChatService — API réelle (Moto.Editor/Services/ChatService.cs)

Service qui route les questions IA (Ollama via MotoAiKernel, ou
FallbackEngine pour les providers externes). Utilisé par AiChatView (panneau
IA), HomeView (barre de saisie de l'accueil), MainPage.Routing.cs (bandeau
IA flottant, `OnAiCommandSubmitted`).

Membres réels : `Threads` (ObservableCollection\<ChatThread\>, le plus récent
en tête), `CurrentThread`/`ActiveThread` (alias, `Threads.FirstOrDefault()`),
`ActiveThreadChanged` (event, déclenché via `Threads.CollectionChanged`),
`Contexts` (ObservableCollection\<ChatContextItem\>, pièces jointes — **UN
SEUL sac partagé par TOUTES les surfaces d'envoi**, voir limite connue
ci-dessous), `PreferInternal` (bool), `CreateThread()`, `AddFile(path)`,
`AddSelection()`, `SendAsync(text)`, `AskWithCodeAsync(model, prompt, code)`.

`IsExternalProviderName(model)` (statique) : seul point de vérité pour
distinguer un provider externe (OpenAI/Anthropic/Mistral) d'un chemin local
(MOTO interne OU Ollama — les deux passent par le même noyau local). Ne pas
recréer un test `Contains("interne")` ailleurs, ça a déjà causé un bug réel
(voir commit `f45a794`).

`ThreadListView.xaml.cs` (panneau compagnon naturel, liste des conversations)
appelle déjà `_chat.SwitchThread(thread)` et `_chat.SearchThreads(text)` —
ces 2 méthodes N'EXISTENT PAS ENCORE sur `ChatService`. Les ajouter
réveillerait ce panneau avec le même profil de correctif qu'AiChatView.

**⚠️ Limites connues, pas corrigées (signalées à Tom)** :
- `Contexts` étant un sac global, joindre un fichier dans une surface (ex.
  panneau IA) puis envoyer depuis une AUTRE (accueil, bandeau flottant) sans
  avoir envoyé depuis la première fait voyager silencieusement la pièce
  jointe vers le mauvais message.
- Cliquer "nouvelle conversation" pendant qu'une réponse est en attente fait
  atterrir cette réponse dans un thread devenu invisible (pas de sélecteur
  d'historique dans l'UI — `ThreadListView` ci-dessus est justement la vue
  qui manque pour régler ça).

## Barre de titre bleue Windows — statut : EN PAUSE, cause connue

Bande bleue native persistante malgré `ExtendsContentIntoTitleBar=true`.
**Cause réelle confirmée** (pas une hypothèse) : réglage Windows 11
"Afficher la couleur d'accentuation sur les barres de titre" — documenté
par Microsoft (fixer TOUTES les couleurs de la titlebar est recommandé mais
pas garanti) et par un ticket GitHub encore ouvert et jamais résolu par
Microsoft (`microsoft-ui-xaml#9374`, même symptôme).

- Correctif sûr appliqué et gardé (4 couleurs manquantes sur
  `AppWindowTitleBar`, `SnapLayoutsHelper.ApplyTitleBarColors`) — **insuffisant
  seul**, testé.
- Piste radicale testée puis abandonnée proprement (`OverlappedPresenter.
  SetBorderAndTitleBar(false,false)`) : fenêtre restée visible (mieux que 3
  tentatives antérieures qui la rendaient invisible), mais résultat visuel
  PIRE (double bande, boutons natifs disparus). Code entièrement retiré.
- Seule piste restante, jamais tentée : fenêtre sans bordure **+ rendu 100%
  custom** de la zone des boutons (réutiliser les boutons MOTO déjà dessinés
  dans `CustomMenuBarView`). Chantier à part entière, pas une correction
  ponctuelle — voir mémoire Claude `moto-editor-titlebar-msix-investigation`
  pour le détail complet de toutes les tentatives (6+ pistes écartées avec
  preuve avant celle-ci).

## Point d'entrée pour ouvrir l'Explorateur

✅ **Ctrl+B câblé et confirmé (02/09).** La palette de commandes annonçait
déjà "Basculer l'explorateur" = Ctrl+B (texte affiché seulement, aucun
raccourci réel avant ce jour). Ajouté dans
`Platforms/Windows/GlobalHotkeyService.cs` (2e `KeyboardAccelerator`, même
mécanisme que Ctrl+Shift+I) → appelle `ToggleSide(isExplorer: true)`, le
même code que le bouton "Fichiers" de la barre de titre. Confirmé par Tom.

2 autres options envisagées, PAS retenues pour l'instant (détail au cas où
Tom veut aller plus loin visuellement un jour) :
- **Petite icône sur la partie libre de la barre de statut** — la colonne de
  gauche de `StatusBarPanelView` est vide (juste "Prêt."). Web-vérifié :
  c'est en fait plus fidèle à Zed qu'un rail vertical (Zed range ses icônes
  de panneaux en bas à gauche, pas sur le bord). Effort petit, risque faible.
- **Vrai rail d'icônes vertical façon VS Code** — nouvelle colonne à gauche
  de toute la fenêtre (pas le style de Zed, mais celui de VS Code). Effort
  moyen (renumérote toutes les colonnes du RootGrid), risque moyen (ce
  fichier a un historique de plantage sur un changement de largeur de
  colonne — à tester prudemment).

## Fichiers exclus de la compilation (`<Compile Remove>`/`<MauiXaml Remove>`)

Motif récurrent dans ce dépôt : des vues entières marquées "jamais
instanciées" dans un commentaire générique du `.csproj`, sans vérification
individuelle. **Toujours vérifier individuellement avant de faire confiance à
ce commentaire** — au moins un cas confirmé FAUX (`AiChatView`, réveillé le
02/09 : le vrai blocage était une dérive d'API, pas les x:Name manquants
allégués).

État des lieux du 02/09 : classification individuelle faite sur la quasi
totalité de la liste (~90 fichiers). Grandes familles :

- **Cluster IA embarquée ONNX** (~25 fichiers, `Moto.Core/Moto.AI/Embedded/*`
  + `Internal/*`) — mort en bloc, a besoin du paquet NuGet ONNX Runtime
  jamais ajouté, désactivé de l'injection de dépendances. Ollama reste le
  seul chemin IA actif. Ne pas réveiller un fichier isolé de ce cluster, ils
  se tiennent tous ensemble (et certains sont des doublons entre eux, ex.
  `EmbeddedLlmEngine.cs` existe en 2 exemplaires non réconciliés).
- **Cluster LSP/Roslyn** (`Moto.Core/LSP/*`, `EditorPaneView.Lsp.cs`,
  `RefactorEngine`/`RefactorAnalyzer`) — mort, item de roadmap v1.0 explicite
  (`Docs/Roadmap.txt`), pas encore construit. `RoslynLanguageServerClient.cs`
  (694 lignes) est le plus gros morceau de travail déjà investi ici — à
  garder en tête pour quand ce chantier reprendra.
- **`CommandPaletteService`** : 4 fragments (`*.cs`, `.AdaptiveRanking.cs`,
  `.Extensions.cs`, `.Ranking.cs`) ajoutés sans jamais être réconciliés —
  cette classe n'a jamais pu compiler comme un tout, aucun fichier de base
  n'existe. Mort confirmé, pas juste désactivé.
- **Doublons de rôle identique** (à trancher lequel garder avant de réveiller
  l'un des deux) : `Settings/SettingsPage.xaml.cs` vs `Views/
  SettingsWindowView.xaml.cs` (déjà tranché en faveur de la fenêtre
  flottante, cf. plus bas) ; `Core/Settings/PluginSandboxService.cs` vs
  `Core/Security/PluginSandboxMinimalService.cs` (même rôle de sandbox
  plugin, jamais réconciliés, tous deux bloqués par le cluster
  marketplace/plugin-SDK hors périmètre).
- **Fichiers "(.cs)" à nom corrompu** dans `Moto.Editor/Views/` :
  `AnalyticsDashboardView.xaml(.cs)` et `MarketplaceDashboardView.xaml(.cs)`
  sont des brouillons obsolètes sans risque à supprimer (les vraies versions
  existent déjà, actives ou exclues séparément). `GlobalDashboardView.xaml
  (.cs)` est DIFFÉRENT — c'est la SEULE copie survivante du XAML de cette
  vue (le `.xaml.cs` normal existe, pas le `.xaml`) ; à renommer, pas à
  supprimer.
- **Doublon de rangement déjà repéré, confirmé une 2e fois** : le motif
  "fichier au mauvais endroit avec un commentaire d'en-tête qui ment sur son
  propre chemin" a été retrouvé sur `StatusBarView` — le vrai code-behind de
  `Controls/StatusBarView.xaml` (bascule "Ultra-Lite") est en fait rangé sous
  `Views/StatusBarView.xaml.cs`, un dossier qui contient un `.xaml`
  totalement différent et sans code-behind à lui. À relocaliser avant de
  juger l'une ou l'autre vue.

**"Réveils faciles" recensés** (backend confirmé réel et vivant, juste
débranché — même profil qu'AiChatView, `.xaml` présent sur le disque) :
`PerformanceStatusBarView` (jugé le plus facile de toute la liste — déjà une
vraie ContentView MAUI, backend déjà utilisé ailleurs avec succès),
`GlobalDashboardView`, `HealthMonitorView`, `LanguageSelectorView`,
`MarketplaceView` (jumeau vivant du `MarketplaceDashboardView` mort),
`SnippetCreatorView`, `WhiteboardView`, `XenoFeedbackOverlay`, `MotoAiPage`
(jamais navigué, aucun bouton n'y mène). `ThreadListView` a besoin des 2
méthodes `ChatService` manquantes citées plus haut. `BreakpointGutterOverlay`
et `InlayHintsOverlay` sont prêts mais orphelins (leur seul appelant prévu
est bloqué ailleurs, LSP ou dialogue de points d'arrêt à reconstruire).

**4 fichiers bloqués par LA MÊME cause exacte** : `SessionBookmarkService`,
`InlineDiffPreviewService`, `UxModeService`, `UxEnchancementService`
appellent tous une API de réglages typée qui n'a jamais été construite (le
catalogue réel n'a qu'une API plate `Get/Set/GetBool/GetString/GetInt`) —
corriger cette seule cause débloquerait les 4 d'un coup. Attention :
`UxModeService` et `UxEnchancementService` font par ailleurs doublon entre
eux (même bascule Compact/Focus, 2 chemins de réglages fictifs différents
`Editor.Ux` vs `Editor.UxAdvanced`) — choisir lequel garder avant de réveiller
l'un des deux, pas les réveiller tous les deux séparément.

Connus déjà avant le 02/09 :
- `Views/AiChatView.xaml(.cs)` — RÉVEILLÉ (02/09), plus exclu, panneau IA
  réel et fonctionnel.
- `Pages/SettingsPage.xaml(.cs)` — toujours exclu par choix (approche
  portée dans `SettingsWindowView` à la place, écran flottant façon Zed
  demandé par Tom plutôt qu'une page plein écran).
- `Controls/CustomMenuBarView.xaml(.cs)` — doublon confirmé MORT (zéro
  référence dans le dépôt) de `Views/CustomMenuBarView.xaml`, le vrai
  fichier utilisé. Risque : facile à modifier par erreur en pensant que
  c'est le bon.
- `Controls/ActivityBarView.xaml` — mort, confirmé par son propre
  commentaire d'en-tête : c'était une rangée horizontale façon VS Code sous
  la barre de titre, retirée le 31/08 quand Tom a demandé de déplacer ses 5
  icônes directement sur la barre de titre.
- `Moto.Core/Moto.AI/Internal/HybridAiRouter.cs` — exclu, seul appelant de
  `GenerateCodeAsync`/`CompleteCodeAsync` (OllamaClient) — ces 2 méthodes
  sont donc du code mort tant que ce fichier reste exclu.

## Dette visuelle connue

✅ **CORRIGÉ (02/09).** Le chantier mécanique décrit ci-dessous (remplacer
`#17181C`/`#202126`/`#3A3B40` codés en dur par `{StaticResource BgSide/
BgPanel/BorderCol}`) a été fait sur les 19 fichiers réellement compilés
(2 des fichiers repérés par la sonde étaient en fait déjà exclus de la
compilation — `SettingsMenuView`, `CollabView` — non touchés, ça n'aurait
rien changé de visible). 45 occurrences remplacées, aucun changement visuel
attendu (mêmes couleurs, juste référencées proprement) — build 0 erreur,
app relancée sans exception. Les couleurs "dérivées" à l'œil (proches d'un
jeton sans l'être exactement, ex. `#1A1B1F` dans NeuralView) n'ont PAS été
touchées, elles ont besoin d'un vrai jugement au cas par cas, pas d'un
chercher/remplacer.

État des lieux du 02/09 (81 fichiers XAML dans Views/Controls) — pour
mémoire, avant ce correctif :
- **34 fichiers (42%)** avaient au moins une couleur codée en dur — mais très
  étalé, presque tous n'en avaient que 1 à 4 (le pire cas, NeuralView,
  n'en avait que 5). Pas de fichier catastrophique isolé.
- **Sur ces 34, 22 fichiers (65%)** recopiaient littéralement une des 3 mêmes
  valeurs hex qui existent comme jetons dans `MotoTheme.xaml` — d'où le
  chantier mécanique ci-dessus.
- Les ~12 fichiers restants ont de vraies couleurs "dérivées" à l'œil (proche
  d'un jeton sans l'être exactement, ex. `#9aa0a6` vs le vrai `#9CA3AF` de
  Txt2) — ceux-là ont vraiment besoin d'être regardés un par un.
- **15 fichiers (18%)** ont un rayon d'arrondi hors de la convention 6/8/12,
  1-2 valeurs isolées à chaque fois.
- **Espacements** (Padding/Margin/Spacing) hors de l'échelle officielle
  (`SpaceXs..Xl`) : quasi universel (65-75 fichiers sur 81) — mais ATTENDU,
  pas une régression : le commentaire de `MotoTheme.xaml` dit lui-même que
  cette échelle n'a été ajoutée que le 01/09 comme guide pour le futur, pas
  pour retoucher des écrans déjà réglés à l'œil comme HomeView. Ne pas
  lancer un chantier de réécriture générale des espacements, ça irait contre
  la politique déjà énoncée.
- **Pires cas repérés** : `HomeView.xaml` (déjà "poli" une fois, pourtant 4
  rayons différents 8/12/17/24 + 2 couleurs de dégradé qui ne correspondent
  à AUCUN jeton) ; `NeuralView.xaml` (5 couleurs en dur, dont 2 copies
  exactes de jetons existants) ; `AiChatView.xaml` (3 rayons différents +
  2 couleurs "presque bonnes" mais pas exactement les jetons) ;
  `AboutView.xaml` (rayon magique 75 pour l'avatar rond, 4 espacements
  différents sans référence à l'échelle).

## Références

- Mémoire Claude (`~/.claude/projects/E--Corpus/memory/`) : chercher les
  fichiers `moto-editor-*` pour le détail complet de chaque chantier
  (panneaux modulaires, réglages, revue du panneau IA, barre de titre...).
- `QWEN.md` (racine du dépôt) : équivalent pour Qwen, utilisé par Tom pour
  lui donner du contexte manuellement (pas de lecture automatique).
- État des lieux brut du 02/09 (4 agents, panel-architecture-audit /
  visual-debt-audit / dead-feature-inventory / zed-inspired-explorer-entry-
  point) : sortie complète encore disponible dans le dossier de tâches de la
  session Claude Code de ce jour-là si un détail précis manque ici — ce
  fichier en est le résumé digéré, pas la sortie brute.
