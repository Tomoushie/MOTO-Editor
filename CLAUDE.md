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

## ⚠️ Piège de test : raccourci de bureau = build Release, pas Debug

Confirmé le 02/09 : le raccourci "MOTO Editor" du bureau de Tom
(`Desktop\MOTO Editor.lnk`) pointe vers
`Moto.Editor\bin\Release\net8.0-windows10.0.19041.0\win10-x64\Moto.Editor.exe`
— PAS le dossier `bin\Debug\...` reconstruit à chaque session de code. Tom
avait testé une correction fraîche via son raccourci de bureau et rien
n'avait changé : le Release datait du 30/08 (avant même le début de la
session du 02/09), donc semaines de corrections absentes. **Dès qu'un test
nécessite que Tom ferme et rouvre l'app lui-même (persistance entre
sessions, redémarrage, etc. — pas juste "regarde la fenêtre déjà ouverte"),
reconstruire AUSSI le Release** (`dotnet build Moto.Editor/Moto.Editor.csproj
-f net8.0-windows10.0.19041.0 -c Release`) avant de le lui demander, en plus
du Debug habituel. Le binaire `win10-x64` est le bon sous-dossier dans les
deux configurations (pas directement sous `net8.0-windows10.0.19041.0\`).

## Maquette "shell type Claude Code" (`Views/Claude/`, 03/09)

Qwen a converti sa propre maquette HTML/CSS/JS (voir `Docs/inspirations/`)
en un vrai shell MAUI, livré en 4 fichiers ("100% additif, 0 erreur
attendue"). **3 vraies causes de blocage trouvées à la compilation, pas
supposées** (même famille de pattern que le reste de ce fichier : vérifier
avant de croire une note d'accompagnement) :
- `Border.CornerRadius` n'existe pas en MAUI (3 occurrences) → `StrokeShape
  RoundRectangle`.
- `Button.Flyout`/`MenuFlyout` (API WinUI native) n'existe pas en MAUI
  cross-plateforme (2 menus, ☰ et utilisateur) → remplacés par
  `DisplayActionSheet`, le vrai mécanisme déjà utilisé ailleurs dans ce
  dépôt (`MainPage.UI.cs`, `OnLicenseClicked`).
- `AppWindow.Presenter` est en lecture seule, `Window` (MAUI) n'a pas de
  `Destroy()` → repris du patron déjà éprouvé dans
  `CustomMenuBarView.xaml.cs` (`OverlappedPresenter.Minimize/Maximize/
  Restore` + `Window.Close()` natif).
- Bonus : la note affirmait que `MotoSubtleText` existait déjà dans
  `MotoTheme.xaml` — faux, ajoutée (reprise de `Txt2`, pas une teinte
  inventée).

**Statut** : compile et tourne, fenêtre de test séparée ("Interface
(maquette Claude Code)", palette `ai.claudeshell` /
`WindowManager.WindowKind.ClaudeShell`) — ne remplace RIEN de l'interface
principale, additif comme prévu. Confirmé par Tom : rendu visuel conforme,
menu ☰ (DisplayActionSheet) fonctionne, envoi de message + réponse simulée
fonctionnent. **Données 100% factices** (sessions, messages, stats
d'accueil, heatmap = seed codée en dur dans `ClaudeShellViewModel`, pas
branchée sur les vrais `ChatService`/`GlobalUsageEngine`) — c'est une
démo visuelle, pas un remplacement fonctionnel du panneau de chat réel
(`AiChatView`). Extensions listées par Qwen mais PAS faites : vraies
fenêtres détachées par panneau, puces de code interactives, drag complet
de la poignée de sidebar, raccourci Ctrl+B réel, rendu riche de la
transcription (actuellement en `Label`, pas en Markdown/HTML).

**Décision de Tom (03/09, même soir)** : reste une VITRINE, ne remplace pas
`MainPage` (ni en entier, ni juste Chat/Cowork/`AiChatView`) — trop de
surface réelle à reconstruire (explorateur, éditeur, terminal réel,
Cortex/Neural/plugins/git/debug...) pour un remplacement total ou même
partiel, risque de régression pas justifié. La voie choisie : piocher des
idées visuelles/UX dans ce shell et les appliquer une par une au VRAI
`MainPage`, même discipline que le reste de cette session (petit
correctif testé → commit, jamais un gros saut).

**Précision de Tom (même soir)** : ce n'est pas un renoncement au
remplacement complet, c'est un séquencement en 2 temps ("strangler
pattern" — faire grandir le neuf à côté de l'ancien plutôt qu'un
remplacement d'un coup) :
1. **Maintenant** : vitrine + polish incrémental du vrai `MainPage`.
2. **Plus tard** : une fois assez de morceaux du vrai `MainPage`
   (explorateur, éditeur, terminal, panneaux) ayant un équivalent visuel
   validé dans ce style, un remplacement complet par une interface avancée
   inspirée de Claude Code (pas une copie conforme) redevient une option
   réaliste — le risque aura été réduit pièce par pièce au lieu d'être pris
   d'un coup. Pas de date/critère chiffré fixé, juste la direction.

✅ **1er morceau réel du plan ci-dessus (03/09, même soir)** : panneau
"Tâches en arrière-plan" RÉEL, distinct de la démo de `ClaudeShellView`.
Suit les VRAIS appels IA (pas des phases/agents factices — MOTO n'en a pas
réellement aujourd'hui, voir plus haut) : `ChatService.Tasks`
(`ObservableCollection<ChatTaskRecord>`), alimenté par un point unique
`RunTrackedAsync` qui enveloppe à la fois `SendAsync` (panneau de chat) et
`AskWithCodeAsync` (bandeau IA du code) — pas de logique dupliquée aux 2
endroits. `ChatTaskRecord` (`Models/ChatTaskRecord.cs`) : Label, Model,
StartedUtc/EndedUtc, IsRunning, DurationLabel (recalculée par un minuteur
UI 1s tant que la tâche tourne). Vue `Views/BackgroundTasksView.xaml(.cs)`,
point d'entrée fenêtre spécialisée + palette `ai.backgroundtasks`, même
patron que les autres cette session. **Bug réel trouvé en testant** :
l'en-tête "X en cours" ne se mettait à jour que sur ajout/retrait de la
collection (`CollectionChanged`), pas quand une tâche EXISTANTE passait de
en-cours à terminée (`EndedUtc` change sur l'objet, pas sur la collection)
— corrigé en rattachant `RefreshCounts()` au même minuteur 1s que le tic
des durées. **Autre confusion de test réelle, pas un bug de ce chantier** :
Tom a d'abord testé avec 2 instances de l'app ouvertes en même temps
(Debug + Release, l'une périmée) — le message envoyé dans l'une
n'apparaissait pas dans la fenêtre Tâches de l'autre, panique évitée en
confirmant qu'une seule instance à la fois tournait. Confirmé ensuite,
propre : "0 en cours" / "1 terminée(s)" corrects après une vraie réponse
IA.

✅ **2e morceau du plan, même soir (commit à suivre)** : `GlobalUsageEngine
.RecordAiCall` était réellement du code mort (confirmé plus haut) —
branché via un nouveau point d'extension `ChatService.AiCallRecorder`
(même patron que `PluginCommandHandler`), appelé depuis `RunTrackedAsync`
après chaque appel IA réussi, câblé une fois par `MainPage
.ResolveExtensionServices()`. Le Tableau de bord global affiche maintenant
de vrais "Appels totaux"/"Tokens consommés"/"Top modèles" (confirmé par
Tom : 1 appel, 13 tokens, "Ollama / MOTO interne" après une question).
Estimation de tokens volontairement grossière (réponse.Length / 4, même
heuristique déjà utilisée par `MainPage.Panels.cs/RefreshHomeStats` pour
la tuile "Tokens" de l'Accueil — pas de vrai tokenizer, cohérence choisie
plutôt qu'une 2e estimation différente). En plus : un lien "Voir le
tableau de bord complet →" ajouté sous la carte de stats de l'écran
d'Accueil (`HomeView`), jusqu'ici uniquement accessible via la palette de
commandes cachée — confirmé par Tom, ouvre bien la même fenêtre
spécialisée. **Limite déjà connue et non corrigée** : `GlobalDashboardView`
ne se rafraîchit pas en direct (un seul appel à `Refresh()` dans
`SetEngine`) — il faut fermer/rouvrir la fenêtre pour voir des chiffres à
jour, même limite déjà documentée pour le panneau Tâches avant sa
correction (ici, pas corrigée, cadre différent : une fenêtre de
consultation ponctuelle, pas un suivi en direct).

✅ **1er des 2 soucis IA corrigé, même soir (commit à suivre)** : bouton
"copier" sur les blocs de code. `ChatMessage.Content` était rendu par un
SEUL `Label` plat (aucune distinction texte/code) — ajouté `ChatMessage
.Segments` (découpe simple sur les balises ``` , PAS un vrai parseur
Markdown/CommonMark, juste texte vs code) + `ChatSegmentSelector`
(`AiChatView.xaml.cs`) choisissant un template texte normal ou un bloc
code (police Consolas + bouton "Copier" via `Clipboard.SetTextAsync`,
même API déjà utilisée dans `AboutView.xaml.cs`). Confirmé par Tom de
bout en bout : rendu du bloc de code distinct, clic sur "Copier" +
collage confirmés (`print("Bonjour")`).

✅ **2e souci IA corrigé, même soir** : identité de l'IA locale. Cause
confirmée : `OllamaClient.GenerateAsync` n'envoyait JAMAIS de "system
prompt" à Ollama (juste `prompt` brut) — le modèle configuré par défaut
étant littéralement `qwen2.5-coder:7b`, il répondait avec sa propre
identité de base (Qwen/Alibaba Cloud), pas par bug mais par absence totale
de contexte. Corrigé en utilisant le VRAI champ `system` de l'API Ollama
(`/api/generate`) — pas un texte ajouté au prompt comme le faisait déjà
partiellement `AskWithCodeAsync` (pas touché ici, chantier séparé si
besoin). Paramètre `system` optionnel threadé `OllamaClient.GenerateAsync`
→ `MotoAiKernel.RouteAsync`/`TryOllamaAsync` → `ChatService.RouteAsync`
(nouvelle constante `InternalSystemPrompt`). Scope volontairement limité
au chemin interne (Ollama) — les providers externes (OpenAI/Anthropic/
Mistral, via `FallbackEngine`) ne sont pas concernés. Confirmé par Tom :
"Je suis MOTO AI, l'assistant intégré à MOTO Editor, créé par MOTO
Software..." — à la bonne personne ("je"), plus de confusion Qwen/3D.
Aucune garantie que TOUT modèle local suive parfaitement cette instruction
(dépend du modèle installé), mais fonctionne avec le modèle par défaut
testé.

## Sonde disponibilité "premium" (03/09, 9 agents parallèles, 54 features vérifiées)

Cartographie complète de l'état réel des 23 fonctionnalités visées pour la
version payante ([[moto-editor-premium-tier-plan]] côté mémoire Claude) —
rapport brut intégral : `Docs/probes/premium-readiness-probe-2026-09-03.json`.

**Bilan chiffré** : 5 REAL_WIRED (déjà réel et branché), 37 DEAD_CODE (code
réel, jamais appelé), 5 STUB (placeholder, ne fait rien), 4 PARTIAL, 3
NOT_FOUND (à concevoir depuis zéro : `ReleaseAgent`, `MigrationAgent`,
plugin `AutoRefactorPro` — fichier orphelin hors solution, `IMotoPlugin`
inexistant).

**Déjà réel et branché** : `ProactiveAnalyticsEngine`/`ProactiveSuggestionsEngine`/
`ContextualActionsEngine` (suggestions proactives + actions contextuelles),
`PluginRegistry` (plugin bundlé), `PerformanceProfiler` (barre de statut —
générique, pas spécifique IA).

**Trouvailles les plus exploitables (code réel + 1 seul point d'entrée UI
manquant)** :
- `DocEngine` génère RÉELLEMENT 6 fichiers Markdown à chaque ouverture de
  projet (`doc_on_project_open`=true par défaut) — confirmé par les
  fichiers `.moto/docs/*.md` présents sur disque avec un horodatage du
  jour même. Mais `DocPanelView.Load(report)` n'est jamais appelé : le
  panneau reste vide, et ses commandes (`ai.doc`/`help.doc`) sont absentes
  de la palette. Plus petit pas : appeler `DocPanel.Load(report)` juste
  après `_docEngine.GenerateAsync()` (MainPage.Panels.cs:648) + ajouter
  les 2 entrées de palette manquantes.
- `GitService` (Init/Stage/Commit/Push/Pull/Merge/Rebase/Branches/Diff/Log,
  tout réel via CLI git) + `GitPanelView` (boutons câblés en interne) —
  mais AUCUN case "git" dans `OpenSpecializedWindow`, aucune commande de
  palette : la vue entière est injoignable. Plus petit pas : un case
  "git" + une entrée de palette, même patron que GlobalDashboard/ThreadList.
- `MarketplaceClientPro`, `VerifiedPublisherService`,
  `PluginMalwareScanner`, la plupart des services Collab (`ReviewLaneView`,
  `CollabRoleService`...) : classes réelles, DI ok, zéro appelant — chacun
  nécessite littéralement UN bouton/case manquant, même famille que tous
  les "réveils faciles" faits ce soir.

**Confirmé une fois de plus** : `FeatureCatalog.cs` (déclarations
"AlreadyImplemented") reste non fiable — reconfirme
[[docs-orchestrator-claims-caveat]] côté mémoire.

**Pas touché ce soir** (sonde de lecture seule, aucun code changé) — punch-list
pour une prochaine session, à trier avec Tom (probablement via
AskUserQuestion, pas décidé unilatéralement).

✅ **1er élément de la punch-list fait le soir même** : panneau Documentation
réveillé (`DocEngine.DocumentationUpdated` → `DocPanel.Load`, palette
`ai.doc` ajoutée). **Bug trouvé EN TESTANT, corrigé aussi** : le bouton
"Ouvrir" par fichier ne faisait rien — `DocPanelView.OpenFileRequested`
était déclaré mais 0 abonné nulle part ; câblé vers `OpenInEditor(path)`
dans le CONSTRUCTEUR de MainPage (PAS dans `LoadWorkspace`, qui tourne à
chaque changement de dossier — `DocPanel` est un contrôle XAML statique
unique, y remettre l'abonnement l'aurait dupliqué à chaque réouverture de
projet).

✅ **Bug de fond résolu (03/09, session suivante, longue bissection) :
AUCUN fichier multi-ligne n'affichait jamais son contenu à l'ouverture.**
Deux causes distinctes, empilées :

1. **Ordre d'application incorrect** — `LoadDocumentIntoEditor`
   (MainPage.UI.cs) est appelée PLUSIEURS FOIS pour un seul fichier ouvert
   (2 à 4 fois : texte vide pendant le chargement différé/lazy, puis le
   vrai contenu une fois chargé). Chaque appel déclenche son propre
   `CodeEditorView.PushContentAsync` (EvaluateJavaScriptAsync), SANS
   séquencement entre eux — un push "vide" pouvait s'appliquer APRÈS le
   vrai contenu et l'écraser. Corrigé par un sémaphore (`_pushGate`) dans
   `CodeEditorView` qui force l'exécution strictement en FIFO (dans
   l'ordre de la DEMANDE, pas de la fin) — la dernière demande reste la
   dernière appliquée. `MainViewModel.LoadSelectedAsync` re-lève aussi
   `PropertyChanged(SelectedDocument)` une fois le contenu réellement
   chargé (forcé sur le thread UI), pour réutiliser l'abonnement existant
   qui recharge déjà l'éditeur au lieu d'ajouter un 2e mécanisme.

2. **La vraie cause principale, trouvée seulement après ce 1er correctif**
   (le contenu restait vide même avec un seul appel bien ordonné) :
   `Web.EvaluateJavaScriptAsync` (pont MAUI/WinUI vers le WebView) ÉCHOUE
   SILENCIEUSEMENT dès que le script contient un retour à la ligne échappé
   (`\r` ou `\n`) — reproduit et confirmé avec une chaîne aussi simple que
   `"Hello\nWorld"` (les chaînes SANS AUCUN saut de ligne, même très
   longues avec accents/emoji, s'appliquaient toujours très bien). Un vrai
   défaut de cette passerelle technique, indépendant de tout code déjà
   écrit dans ce dépôt — donc INVISIBLE jusqu'ici puisque rien n'avait
   avant ce soir ouvert un fichier fraîchement généré/multi-ligne par ce
   chemin précis pour de vrai. Contourné en encodant le contenu en Base64
   avant de l'envoyer au script (`setContentB64`, JS) puis en le décodant
   côté JS (`atob` + `TextDecoder('utf-8')`) — aucun caractère spécial en
   Base64, donc plus aucun risque de saut de ligne dans le script envoyé.
   Confirmé par Tom : README.md (47 lignes, tableau markdown, bloc de
   code) s'affiche intégralement, avec la coloration syntaxique.

**Portée réelle de ce correctif** : touche `CodeEditorView`, le composant
central utilisé pour ouvrir TOUT fichier dans MOTO Editor (pas seulement
les docs générées) — l'éditeur de code n'avait donc jamais correctement
affiché aucun fichier multi-ligne avant ce soir, un bug de fond bien plus
large que le simple panneau Documentation qui l'a révélé.

✅ **Petit bug trouvé et corrigé en testant ci-dessus** : `AiBar`
(bandeau IA flottant sur l'Accueil/l'Éditeur) restait affiché par-dessus
l'écran d'Accueil après fermeture du dernier fichier ouvert. Cause :
`AiBar.Show()` (appelée sur activation de la fenêtre,
`GlobalHotkeyService.Register` dans `MainPage.xaml.cs`, uniquement si un
document était ouvert à CE moment précis) n'avait aucune contrepartie pour
le cacher quand `Documents` redevient vide. Corrigé en appelant
`AiBar.Hide()` dans le même abonnement `Documents.CollectionChanged` qui
bascule déjà `Home.IsVisible`/`EditorPane.IsVisible`. Confirmé par Tom.

✅ **Chantier suivant, même soir (budget réinitialisé) : GitPanelView réveillée.**
Trouvée par la sonde disponibilité premium — `GitService` (Init/Stage/
Commit/Push/Pull/Fetch/Merge/Rebase/Checkout/branches/Status/Diff/Log/
Stash, tout réel via `git` CLI) et `GitPanelView` (boutons câblés en
interne) étaient entièrement construits mais totalement injoignables —
aucun case dans `OpenSpecializedWindow`, aucune entrée de palette. Point
d'entrée ajouté (fenêtre spécialisée "Git" + palette `git.panel`), même
patron que GlobalDashboard/ThreadList/Tâches en arrière-plan.

**2 vrais bugs trouvés EN TESTANT (pas devinés), tous deux corrigés** :
- `GitService` n'avait AUCUNE méthode qui passait un dossier de travail à
  `_terminal.ExecuteAsync` — toutes les commandes auraient tourné dans le
  dossier par défaut du processus, pas le projet ouvert (risque réel une
  fois un vrai bouton "push" ajouté). Ajouté `GitService.SetWorkspace
  (path)` + un helper interne `ExecAsync` qui l'utilise partout (remplace
  les ~24 appels directs à `_terminal.ExecuteAsync`), appelé à la fois
  dans `ResolveExtensionServices()` et dans `LoadWorkspace`
  (MainPage.Panels.cs) pour couvrir le chargement initial ET les
  changements de dossier ultérieurs.
- Accents mal affichés dans les noms de fichiers Git ("Cha\303\256ne" puis,
  une fois ce 1er souci corrigé, "ChaÃ®ne") — 2 causes empilées : (1) git
  échappe par défaut tout nom de fichier non-ASCII en séquences octales
  (`core.quotepath`, comportement documenté de git lui-même) — corrigé en
  injectant `-c core.quotepath=false` sur chaque commande dans `ExecAsync` ;
  (2) `TerminalService.ExecuteAsync` ne précisait aucun encodage pour lire
  la sortie du process — .NET utilisait la page de code OEM/ANSI du
  système au lieu d'UTF-8. Corrigé en fixant `StandardOutputEncoding`/
  `StandardErrorEncoding` sur `Encoding.UTF8` — **scope limité à cette
  méthode one-shot** (utilisée par `GitService` et consorts), PAS à
  `Start()` (terminal interactif plus bas dans le même fichier : cmd.exe y
  émet ses propres bannières en page de code OEM, les changer les aurait
  cassées, hors scope ici).

Confirmé par Tom : branche réelle ("main"), vrais fichiers modifiés/
untracked de CE dépôt affichés correctement, accents corrects après les 2
correctifs.

**Résolu (03/09) — panneau Terminal caché en bas de l'écran.** Signalé par
Tom ("caché, il faut scroller pour le voir"). Écarté d'abord : `RootGrid`
(`MainPage.xaml`) est un `Grid` simple, sans `ScrollView` ancêtre — pas un
vrai mécanisme de défilement MAUI. Cause réelle trouvée dans `App.xaml.cs`
(`OnWindowsWindowCreated`, persistance de session ajoutée le 02/09) : la
POSITION restaurée était bien revérifiée contre l'écran actuel (tolérance
100px), mais la TAILLE ne l'était jamais — une hauteur mémorisée plus
grande que l'écran actuel (autre moniteur, redimensionnement manuel)
rouvrait systématiquement une fenêtre trop grande, poussant le bas
(Terminal, bouton fermer, barre de statut) hors champ. Corrigé : largeur/
hauteur plafonnées à `DisplayArea.WorkArea`, position Y ajustée pour que
`savedY + height` ne dépasse jamais le bas de la zone de travail. Vérifié
directement (traces temporaires dans le journal de restauration + fenêtre
testée en direct) puis confirmé par Tom après reconstruction. Au passage,
2 demandes de Tom traitées dans le même correctif :
- **Bouton "Terminal" dans la barre du haut** (`CustomMenuBarView.xaml`,
  même patron que Fichiers/Recherche/Collaboration) — le terminal n'était
  accessible que via les cartes "Actions suggérées". Réutilise la commande
  `"view.terminal"` déjà enregistrée (`RegisterMenuCommands`), aucun
  nouveau routage nécessaire.
- **Panneau "Actions suggérées" (`ProactiveActionsView`) fermable et
  déplaçable** : bouton ✕ (reste fermé tant que les suggestions ne
  changent pas — comparaison de clé, pas de dismiss permanent aveugle) +
  en-tête devenu poignée de déplacement libre (`PanGestureRecognizer` +
  `TranslationX`/`TranslationY`).

Commit `d256361`. **Piège découvert en testant** : une version de MOTO
Editor est installée séparément en MSIX/Store
(`C:\Program Files\WindowsApps\MotoSoftware.MOTOEditor_...\Moto.Editor.exe`,
AUMID propre) — totalement indépendante des dossiers `bin\Debug`/
`bin\Release` de ce dépôt et jamais reconstruite par nos correctifs. Le
raccourci de bureau de Tom pointe correctement vers `bin\Release\...`
(vérifié via le `.lnk`), mais toute méthode qui résout l'app par NOM
plutôt que par CHEMIN (ex. `open_application` en test, ou une recherche
Windows/tuile différente côté Tom) peut silencieusement ouvrir cette
version figée à la place — aucun rapport avec le code de ce dépôt.

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

**03/09 — "Ctrl+Maj+P bloqué après Documentation" investigué, non
reproduit (commit `8c490bf`)** : Tom avait signalé qu'après avoir ouvert
"Documentation" depuis la palette, impossible de refermer, et Ctrl+Maj+P
ne refonctionnait plus. Traces temporaires ajoutées sur
`ToggleCommandPalette`/`OnWindowsPreviewKeyDown` + compteur de
souscriptions (hypothèse : double abonnement via un re-déclenchement de
`Loaded`) — séquence rejouée par Tom, journal montrant un comportement
PARFAITEMENT correct (une seule souscription, `IsVisible` bascule
proprement) et confirmation que le raccourci refonctionne. Non reproduit,
probablement déjà réglé en cascade par un correctif antérieur de la même
session. En comparant `DocPanelView` aux autres overlays pendant
l'investigation : c'était le SEUL panneau du dock IA sans bouton ✕
(`CommandPaletteView`/`ProactiveActionsView` en ont déjà un) — correspond
au "impossible de refermer le menu" du rapport initial. Bouton ✕ ajouté,
même patron, confirmé par Tom.

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

**Repli automatique si Ollama est indisponible : vérifié, EXISTE DÉJÀ et
fonctionne (02/09).** Chaîne complète tracée : `ChatService.RouteAsync` →
si `preferInternal`, essaie `MotoAiKernel.RouteAsync` (Ollama, avec
`IsAvailableAsync()` + try/catch — jamais d'exception qui remonte) → si ça
échoue, `ChatService` bascule sur `FallbackEngine.GenerateAsync` (les
providers externes configurés dans Réglages > Clés API, via
`AiProviderManager.CompleteWithFallbackAsync`) → si ÇA échoue aussi,
message clair à l'utilisateur : "Aucun moteur IA disponible (Ollama et
fallback injoignables). Vérifie tes paramètres IA." Aucun crash, aucun
blocage à aucune étape. Seul détail cosmétique : `MotoAiKernel` a sa PROPRE
méthode privée `FallbackAsync` qui ne fait qu'un message d'échec (pas un
vrai repli) — le nom prête à confusion avec le `FallbackEngine` de
`ChatService` qui, lui, fait le vrai travail, mais ce n'est pas un bug,
juste deux noms proches pour deux choses différentes.

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

✅ **`ThreadListView` réveillée (03/09)** — `SwitchThread(thread)` (place le
thread choisi en tête de `Threads`, même convention "le plus récent en tête
= actif" que `CreateThread`/`EnsureThread` ; `Threads.Move` déclenche le
`CollectionChanged` déjà écouté pour lever `ActiveThreadChanged`, pas de 2e
mécanisme de notification) et `SearchThreads(query)` (filtre insensible à
la casse sur titre + contenu des messages, requête vide -> liste complète)
ajoutées à `ChatService`. `using System.Linq;` manquant aussi dans
`ThreadListView.xaml.cs` (`.FirstOrDefault()` sur la sélection). Point
d'entrée : fenêtre spécialisée "Conversations"
(`WindowManager.WindowKind.ThreadList`) + commande de palette
`ai.threadlist`, même patron minimal que `GlobalDashboardView` juste avant
— PAS intégrée dans `AiChatView` elle-même (voir limite ci-dessous, toujours
vraie pour partie). Confirmé par Tom : la liste affiche titre+heure, la
recherche filtre sans planter, "Nouvelle conversation" fonctionne.

**⚠️ Limites connues, pas corrigées (signalées à Tom)** :
- `Contexts` étant un sac global, joindre un fichier dans une surface (ex.
  panneau IA) puis envoyer depuis une AUTRE (accueil, bandeau flottant) sans
  avoir envoyé depuis la première fait voyager silencieusement la pièce
  jointe vers le mauvais message.
- Cliquer "nouvelle conversation" pendant qu'une réponse est en attente fait
  atterrir cette réponse dans un thread devenu invisible : un peu mieux
  depuis le 03/09 (la fenêtre "Conversations" ci-dessus permet de le
  retrouver et d'y revenir), mais toujours pas de sélecteur d'historique
  intégré directement DANS le panneau de chat principal — reste une fenêtre
  séparée à ouvrir via la palette, pas un clic sur place.

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
  (.cs)` était DIFFÉRENT — c'était la SEULE copie survivante du XAML de
  cette vue (le `.xaml.cs` normal existait, pas le `.xaml`) — ✅ renommée et
  réveillée le 03/09, voir plus bas.
- **Doublon de rangement déjà repéré, confirmé une 2e fois** : le motif
  "fichier au mauvais endroit avec un commentaire d'en-tête qui ment sur son
  propre chemin" a été retrouvé sur `StatusBarView` — le vrai code-behind de
  `Controls/StatusBarView.xaml` (bascule "Ultra-Lite") est en fait rangé sous
  `Views/StatusBarView.xaml.cs`, un dossier qui contient un `.xaml`
  totalement différent et sans code-behind à lui. À relocaliser avant de
  juger l'une ou l'autre vue.

✅ **`PerformanceStatusBarView` réveillée (02/09)** — branchée dans la vraie
barre de statut (`StatusBarPanelView`), 4 puces avant les compteurs
d'erreurs : 💾 mémoire, ⚙ CPU, 🧵 threads, ♻ GC (gen 0). **Attention, la
classification "réveil facile" ci-dessous était incomplète** : son code
appelait `_profiler.GetCurrentMode()`/`GetEstimatedFps()`, deux méthodes
qui N'EXISTENT PAS sur `PerformanceProfiler` (confirmé à la compilation,
pas un simple oubli d'API) — et "Mode"/"FPS" ne correspondent de toute
façon à rien de réel pour un éditeur XAML (pas de boucle de rendu à
mesurer, contrairement à un moteur de jeu). Remplacés par 2 vraies mesures
déjà échantillonnées par `PerformanceProfiler.SampleMetrics`
(`thread_count`, `gc_gen0`) plutôt que d'inventer des chiffres. Le calcul
du CPU% lui-même était aussi faux dans le code d'origine (modulo du temps
CPU total écoulé depuis le lancement — ne représente rien) : refait en
delta réel (temps CPU consommé / temps réel écoulé / nombre de cœurs).
**Leçon reconfirmée** : vérifier l'API réelle avant de faire confiance à
une classification "facile" — même règle que pour la palette de commandes
plus tôt le même jour.

**Vague du 02/09, tentative sur 7 "réveils faciles" d'un coup — moitié
confirmée, moitié re-classée plus dure :**

- ✅ **Compilent réellement maintenant** (testé, pas supposé) :
  `MotoAiPage` (manquait juste `MotoAiService.ApplyChangesAsync`, ajoutée —
  ⚠️ limite honnête : `AiResponse.FileChanges` n'est aujourd'hui JAMAIS
  rempli par `ExecuteAsync`, donc "Appliquer" ne fera rien tant que
  personne n'écrit l'extraction "réponse IA → changements de fichiers
  structurés" — chantier à part entière, pas fait ici), `XenoFeedbackOverlay`,
  `MarketplaceView`, `LanguageSelectorView` (ces 3 derniers : il manquait
  juste `using Microsoft.Maui.Controls.Shapes;`, mécanique).

  **Points d'entrée (02/09)** :
  - ✅ `MotoAiPage` : entrée "MOTO AI (mode Débutant/Expert)" ajoutée à la
    palette de commandes (`CommandPaletteEngine.cs`, id `ai.motopage`) →
    `OnMenuCommanded` → `Navigation.PushAsync(new Pages.MotoAiPage())`.
    Confirmé par Tom, l'écran s'ouvre.
  - ⏸️ `MarketplaceView` : PAS de point d'entrée, décision de Tom —
    `PluginGalleryView` (déjà réel et utilisé, bouton 🧱) affiche DÉJÀ
    installés+marketplace ensemble, risque de doublon confirmé avant
    d'agir. À reconsidérer seulement si un vrai besoin distinct apparaît.
  - ⏸️ `XenoFeedbackOverlay` : PAS de point d'entrée — son constructeur est
    utilisable sans dépendance, mais elle a besoin de `SetPipeline
    (XenoPipelineV5 pipeline)` pour afficher quoi que ce soit d'utile, et
    aucun déclencheur visible ("lancer un pipeline Xeno") n'existe
    aujourd'hui dans l'interface de MOTO Editor pour lui en fournir un —
    lui donner un bouton isolé n'aurait affiché qu'un écran vide. À
    reprendre le jour où un vrai déclencheur de pipeline Xeno existe.
  - ⏸️ `LanguageSelectorView` : PAS de point d'entrée non plus — en creusant
    plus loin que la simple compilation, ses 2 dépendances
    (`LanguageManager`, `MarketplaceLanguageClient`) ne sont enregistrées
    nulle part dans le conteneur DI, et `MarketplaceLanguageClient` appelle
    une vraie URL externe (`marketplace.moto-editor.dev`, jamais vérifiée
    comme existante). Lui donner un point d'entrée aujourd'hui aurait
    demandé de créer ces 2 services en plus, et de vérifier d'abord que
    cette adresse répond — plus gros que "juste un bouton", pas fait
    aujourd'hui.
- ❌ **Re-classées "pas une correction rapide"** (confirmé à la compilation,
  pas par supposition) : `HealthMonitorView` (le `.xaml.cs` attend un
  `MetricsLabel` qui n'existe pas — le vrai `.xaml` a `ScoreLabel`/
  `ScoreBar`/`IssueList` : les deux fichiers ont divergé, pas un oubli
  d'API), `SnippetCreatorView` (même famille : `TriggerLabel`/`StatusLabel`
  attendus, absents du vrai XAML, plus une propriété `init`-only assignée
  hors constructeur), `WhiteboardView` (API `ICanvas` du MAUI actuel n'a
  plus le même contrat que ce que le code suppose — `StrokeColor` sans
  accesseur `get`, `DrawString` avec une signature différente). Remises
  dans l'exclusion du `.csproj`, avec le motif exact au lieu du motif
  générique d'origine.

✅ **`GlobalDashboardView` réveillée (03/09)** — `.xaml` renommé (n'était
plus exclu du build ensuite), `using Microsoft.Maui.Controls.Shapes;`
manquant ajouté (CS0246 sur `RoundRectangle`), point d'entrée ajouté :
fenêtre spécialisée "Tableau de bord global" (`WindowManager.WindowKind
.GlobalDashboard`) + commande de palette `ai.globaldashboard`. **Bug réel
trouvé en testant, pas supposé** : la fenêtre s'ouvrait vide — `GlobalUsageEngine`
n'était JAMAIS enregistré dans le conteneur DI
(`MotoServiceCollectionExtensions.cs`), donc `GetService<GlobalUsageEngine>()`
renvoyait toujours `null` et `StartSession`/`RecordBuild`/`RecordDebugSession`
(appelés ailleurs dans `MainPage.UI.cs` depuis la v30) étaient des no-op
silencieux depuis leur écriture d'origine. En plus, sa résolution dans
`MainPage.UI.cs` (`InitializeGlobalUsage`, appelée dans le CONSTRUCTEUR de
`MainPage`) tournait avant que `Handler` soit disponible — même piège que
`_pluginGallery` (02/09) — donc même une fois enregistré en DI, `_globalUsage`
restait `null`. Corrigé en deux temps : ajout de
`services.AddSingleton<GlobalUsageEngine>(...)`, ET résolution dupliquée
dans `ResolveExtensionServices()` (tourne sur `Loaded`, donc après que
`Handler` existe). Confirmé par Tom : Temps de travail/Premier lancement/
Dernière activité s'affichent réellement. Fichiers/Lignes/IA/Exports
restent à 0 en toute honnêteté — aucun code de production n'appelle
`RecordFileCreated`/`RecordLines`/`RecordAiCall`/`RecordExport` nulle part ;
seuls `StartSession`/`StopSession` et `RecordBuild`/`RecordDebugSession`
existent. Câbler ces compteurs à la source est un chantier séparé, pas fait
ici.

**"Réveil facile" jamais retesté** (classification d'origine à prendre avec
prudence, comme les cas ci-dessus) : `BreakpointGutterOverlay`
et `InlayHintsOverlay` sont prêts mais orphelins (leur seul appelant prévu
est bloqué ailleurs, LSP ou dialogue de points d'arrêt à reconstruire).
`ThreadListView` : ✅ fait le 03/09, voir plus haut (section "ChatService —
API réelle").

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

✅ **CORRIGÉ (02/09), passe 2 — les couleurs "dérivées" au cas par cas.**
En creusant les hex restants après la passe mécanique (ci-dessous) :
- 2 nouveaux vrais jetons ajoutés à `MotoTheme.xaml`, pour des couleurs
  recopiées à la main de façon cohérente et clairement volontaire — pas
  "corrigées" vers un jeton existant (ça aurait aplati un effet voulu) :
  **`BgPanelInner`** (`#1A1B1F`, fond de la carte intérieure de 12 panneaux
  — Cortex/Neural/Platform/Workspace/AutoLink/ContextSuggestions/DocPanel/
  ExportMenu/PasswordGate/Performance/Presentation/RemoteConnect — une
  teinte distincte du cadre extérieur d'AddFloatingPanel, pour la
  profondeur) et **`Danger`** (`#DC2626`, bouton "Supprimer" de
  `ConfirmationOverlay` + marqueurs de points d'arrêt dans les 2 panneaux
  Debug — distinct d'`Error` qui reste réservé aux badges d'erreur).
- Vraies dérives corrigées vers un jeton existant (`AiChatView.xaml` :
  `#9aa0a6`→Txt2, `#1E1F24`→BgApp ; `StoryModeView.xaml` : `#1E1F24`→BgApp).
- Laissés tels quels, en connaissance de cause : `#0F1013` (NeuralView,
  un seul usage réel, boîte de résultat volontairement plus sombre — pas
  assez répété pour mériter un jeton) ; le dégradé `#171B21`/`#111419` de
  `HomeView.xaml` (2 arrêts de dégradé, pas des couleurs plates à
  tokeniser) ; la CSS embarquée dans `Controls/CodeEditorView.xaml.cs`
  (texte HTML/CSS dans une chaîne C#, pas une liaison XAML — les valeurs
  sont déjà les bonnes, juste pas via un jeton, chantier séparé si un jour
  utile). Build 0 erreur, confirmé identique à l'œil par Tom.

✅ **CORRIGÉ (02/09), passe 1 — mécanique.** Le chantier mécanique décrit ci-dessous (remplacer
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

## Modularité façon Zed/VS Code — état des lieux (02/09, sonde 5 domaines)

Demande de Tom : rapprocher MOTO de la modularité/architecture de Zed et
VS Code (fait-maison, sans dépendance, léger), la barre bleue restant
explicitement en pause. Sonde en lecture seule sur 5 domaines avant de
choisir où coder. Résumé digéré ci-dessous ; le détail complet (fichiers/
lignes cités) est dans le journal de la Workflow `wf_601f74fd-4fc`
(02/09) si besoin de retrouver une citation précise.

**Panneaux/docking** : la base (`AddFloatingPanel`, `MainPage.Panels.cs`)
est solide — redimensionner, glisser-réordonner, glisser entre docks,
changer de côté marchent tous réellement. ✅ CORRIGÉ (03/09, commit
`8ba5484`) : **bouton "détacher" (⧉)** ajouté à côté du ✕ de chaque
panneau (Platform/Cortex/Neural/Workspace/AiChat/Gallery/Analytics/Debug
— pas Recherche, overlay centré sans fenêtre spécialisée équivalente) —
sort le panneau dans sa propre fenêtre OS via `WindowManager`/
`OpenSpecializedWindow`, plomberie déjà existante, jusqu'ici accessible
uniquement par la commande cachée `/window <kind>`. Limite assumée : ouvre
une instance FRAÎCHE du panneau, pas littéralement celle affichée
déplacée — un vrai "glisser l'onglet hors fenêtre" reste un chantier
séparé, plus gros. ✅ CORRIGÉ AUSSI : l'exclusivité Cortex/Neural/
Workspace/Gallery, avant codée en dur dans 4 méthodes jumelles (chacune
recopiait la même liste "masquer les autres"), remplacée par
`ShowOnlyAiGroupPanel(panel)`, une seule liste à maintenir. Ce qui manque
encore : **aucune disposition n'est sauvegardée entre les sessions**
(tout revient aux valeurs XAML par défaut à chaque lancement) ; **aucun
regroupement en onglets** dans un même dock (les panneaux s'empilent
verticalement) ; **aucune scission en plusieurs vues côte à côte** (les
réglages "Split vertical/horizontal" existent dans le catalogue mais sont
décoratifs, aucun code split-pane derrière).

**Commandes et raccourcis** : ✅ Gap A (registre central) CORRIGÉ (02/09,
commit à suivre) — `OnMenuCommanded` (`MainPage.Routing.cs`) n'est plus un
`switch` figé : c'est maintenant `_commandRegistry.Execute(id)`, où
`_commandRegistry` (`Moto.Core.AI.Commands.CommandRegistry`, nouveau
fichier) est une vraie table `id → Action` remplie une fois au démarrage
par `RegisterMenuCommands()` (mêmes ~35 identifiants qu'avant, même
comportement, confirmé par Tom sur un échantillon : Ctrl+B, palette
"Paramètres", palette "Compiler", boutons Fichiers/Recherche/Collaboration).
`CommandPaletteEngine.BuildStaticCommands()` (catalogue affiché DANS la
palette, 25 entrées) reste, lui, une liste figée — non touché par ce
correctif, ce n'est pas la même chose que le registre d'EXÉCUTION. **3
mécanismes de raccourcis séparés et non unifiés** coexistent toujours
(`GlobalHotkeyService` à paramètres positionnels, `OnWindowsPreviewKeyDown`
pour Échap/Ctrl+Maj+P, et le champ `Shortcut` purement décoratif affiché
dans la palette — ✅ CORRIGÉ pour F5 (commit `c289746`, 02/09) : la touche
affichée pour "Compiler" n'avait jamais été câblée à rien (aucune trace de
`VirtualKey.F5` dans tout le dépôt avant ce correctif — trouvé en testant
le nouveau registre, pas une régression qu'il aurait causée). `GlobalHotkeyService.Register`
gagne un paramètre `onBuild`, même mécanisme que Ctrl+B, câblé sur
`_commandRegistry.Execute("run.build")`. Confirmé par Tom : "BUILD OK".
Les autres raccourcis de la palette restent décoratifs — ce correctif ne
traite QUE F5, pas le mécanisme général (toujours Gap C, non fait). Ce que
le nouveau registre ouvre pour PLUS TARD, pas fait maintenant (Gap B) : un
plugin pourrait un jour appeler `_commandRegistry.Register(...)` pour
ajouter sa propre commande sans toucher à ce fichier — la brique existe,
rien ne l'utilise encore de l'extérieur de MainPage. `OnGearMenuItemSelected`
(menu ⚙, 9 items) et `OnAiCommandSubmitted` (commandes slash) restent aussi
des switchs/if-chains séparés, non migrés vers ce registre. Deux briques
déjà écrites mais jamais branchées, prêtes à servir plus tard : la
catégorie `CommandCategory.Plugin` (déjà un libellé "🧩 Plugins" dans la
palette, jamais utilisée) et `Moto.Core/Behaviors/KeyboardShortcutBehavior.cs`
(Behavior XAML générique, zéro attachement nulle part).

**Réglages** : bonne surprise — le design pour qu'un plugin ajoute sa
propre section existe déjà et est documenté dans le SDK
(`IPlugin.Settings` → commentaire "auto-injectés dans SettingsCatalog
sous `plugin.{id}.{clé}`", un plugin d'exemple `SampleFormatPlugin`
déclare déjà 4 réglages selon ce contrat). Le pont n'a simplement jamais
été construit : `PluginRegistry.Register()` ajoute le plugin à une liste
et logue, sans jamais lire `plugin.Settings` ni écrire dans
`SettingsCatalog.All` (qui est une `List<SettingDefinition>` publique et
mutable — l'ajout serait trivial). Bonus : `SettingsEngine.Shared` (API
plate `Get/Set/GetBool/GetString/GetInt`, JSON sur disque) n'a aucune
notion de catalogue et accepte déjà n'importe quelle clé — c'est la
bonne fondation à réutiliser pour toute nouvelle persistance (voir
"Sauvegarde de session" ci-dessous), à l'inverse de l'API typée
inexistante qui bloque `SessionBookmarkService`.

**Plugins — le point le plus cassé de toute l'app.** ✅ AFFICHAGE CORRIGÉ
(02/09, commit `b6f0b9d`) puis ✅ **UN VRAI PLUGIN CHARGÉ ET FONCTIONNEL**
(02/09, commit `98494e9`) — voir le détail technique complet dans le
message de ce commit. Résumé :
- `Moto.Plugin.SDK` avait bien **2 interfaces `IPlugin` dans le même
  namespace** (confirmé en lisant les deux fichiers) — `IPlugin.cs`
  (Activate/Deactivate/RegisterCommand, **zéro utilisateur réel** dans le
  dépôt) supprimé ; `Contracts/IPlugin.cs` (SdkVersion/Settings/
  InitializeAsync/ExecuteCommandAsync, celui qu'implémente réellement
  `PluginBase`/`SampleFormatPlugin`) conservé comme SEUL contrat. Ciblait
  aussi `net8.0` au lieu de `netstandard2.0` (empêchait tout projet
  netstandard2.0 comme `Moto.Plugin.SampleFormat` de le référencer — sens
  interdit) ; corrigé + polyfill `IsExternalInit` ajouté (nécessaire pour
  les propriétés `init` sous netstandard2.0).
- `PluginRegistry.Register()` n'appelait ni `InitializeAsync` ni
  n'injectait `IPlugin.Settings` dans `SettingsCatalog` — la promesse
  documentée depuis longtemps dans le commentaire du contrat n'était tenue
  nulle part. `RegisterAsync` (nouveau) fait enfin les deux, via un vrai
  `PluginSettingsAccessor` (jusqu'ici seule une `FakeSettingsAccessor` de
  test existait).
- `SdkAdapter.cs` (pont `Moto.Plugin.SDK.IPlugin` ↔ `Moto.Core.Plugins.
  IPlugin`, bien écrit) était **exclu de la compilation** de `Moto.Core`
  depuis longtemps ("Moto.Plugin.SDK non référencé") — la mémoire du
  chantier disait ce fichier "déjà testé et fonctionnel" ; en réalité il
  n'a jamais compilé dans le vrai projet. Réveillé (référence ajoutée +
  un `using` manquant corrigé).
- `Moto.Plugin.SampleFormat` référençait le SDK comme **paquet NuGet
  publié** ("Moto.Plugin.SDK" 1.0.0) — jamais empaqueté ni publié nulle
  part, restauration vouée à l'échec. Remplacé par une `ProjectReference`
  directe (plugin BUNDLÉ avec l'éditeur, pas un plugin tiers externe).
- `SampleFormatPlugin` est désormais réellement enregistré au démarrage
  (`ResolveExtensionServices`) : ses 4 réglages apparaissent dans Réglages
  → Plugins, il compte dans "1 installé(s)" de la Galerie, et
  `/sample-format format|stats|help` répond réellement — testé depuis
  **2 surfaces différentes** (AiChatView "MOTO AI" ET le bandeau IA/
  Accueil) grâce à `ChatService.PluginCommandHandler`, un point d'extension
  unique câblé une seule fois plutôt que dupliqué par vue (un premier
  essai câblé seulement dans `OnAiCommandSubmitted` ratait la surface
  AiChatView — corrigé après un retour de test réel de Tom).

**Ce qui reste hors scope, confirmé par du vrai code, pas de la
supposition :**
- **Chargement dynamique d'un dossier `plugins/` externe** : toujours pas
  fait. Le plugin bundlé est référencé en dur (ProjectReference) — un
  vrai chargeur (`AssemblyLoadContext`, scan de dossier) reste un futur
  chantier séparé, plus gros (isolation/sandbox comprise).
- **4 autres "plugins" du dépôt sont bien plus cassés qu'un simple
  problème de solution** : `Moto.Plugins.MotoDarkPro` (thème payant, 5€),
  `Moto.Plugins.CortexBooster`, `Moto.Plugins.AutoRefactorPro`, et
  `Moto.Plugin.Template` implémentent tous une interface `IMotoPlugin`
  qui **n'existe nulle part** dans le dépôt, appellent des membres
  inexistants (`context.ShowMessage`, `context.Logger` sur un type qui ne
  les déclare pas) et passent des délégués au mauvais type à
  `RegisterCommand`. Pas de simples "projets hors solution" comme le
  disait un état des lieux précédent — ils ne compileraient pas même
  ajoutés à la solution, quel que soit l'état du SDK. `PluginInstallerService.cs`
  (le vrai chargeur dynamique, jamais instancié) cherche justement ce
  `IMotoPlugin` par nom de chaîne — cohérent avec ces 4 fichiers, mais
  personne n'a jamais écrit l'interface qu'ils ciblent tous.
- **`Moto.Plugin.PythonAssistant`** (LSP Python, `PluginBase`/bon contrat
  par ailleurs) référence `OmniSharp.Extensions.LanguageClient` 1.0.0 dont
  l'API utilisée dans le code (`LanguageClient`, `LanguageProtocol.Models`)
  **ne correspond pas à la version déclarée** — vérifié par un essai de
  compilation direct (CS0234, types introuvables), pas juste "pas dans la
  solution". `SampleFormatPlugin` reste donc le SEUL plugin du dépôt qui
  compile ET s'exécute réellement.
- `MarketplaceClient` interroge toujours en dur une URL jamais vérifiée et
  avale toute exception en silence. Les messages "Aucun plugin distant
  disponible"/"0 résultat(s)" (Galerie + recherche) ont été reformulés
  (02/09) pour dire honnêtement qu'aucun serveur de marketplace n'existe
  encore, plutôt que de laisser croire à une recherche cassée (Tom l'avait
  interprétée comme un bug).
- `Moto.Core/Extensions/ExtensionSystem.cs` (4e famille séparée, manifeste
  `extension.json`) et `PluginSandboxMinimalService.cs` (sandbox de
  sécurité, logique réelle) restent tous deux jamais instanciés, non
  touchés par ce chantier.

**Sauvegarde de session/workspace** : rien n'est mémorisé aujourd'hui —
ni dossier ouvert, ni fichiers ouverts, ni taille de fenêtre (codée en
dur 1360×860 dans `OnWindowsWindowCreated`), ni disposition des
panneaux. `WorkspaceStateService` (le seul mécanisme réel et câblé de
cette zone) ne persiste QUE l'ordre des sessions de *chat* sur l'écran
d'Accueil — à ne pas confondre malgré le nom. `SessionBookmarkService`
(onglets + curseur) est exclu du build, bloqué par la même API de
réglages typée inexistante que d'autres fichiers déjà documentés plus
haut. `SnapshotResumeService` (timer 30s) n'est jamais instancié ET ses
4 méthodes de collecte sont des coquilles vides (`=> new()`/`=> null`)
— `FeatureCatalog.cs` le déclare pourtant `AlreadyImplemented` (faux,
cohérent avec [[docs-orchestrator-claims-caveat]] : ne jamais prendre un
catalogue auto-déclaré pour argent comptant). `WorkspaceManager`
(dossiers-projets + favoris) est complet mais jamais câblé.

**Décision de Tom (02/09)** : ne pas tout attaquer d'un coup. 3 chantiers
proposés en retour de cette sonde, un choisi pour continuer — voir la
mémoire Claude du jour pour lequel a été retenu et son état d'avancement.

## Stratégie de vitesse à terme : rester .NET vs Rust/GPU (02-03/09)

Question de Tom : MOTO pourra-t-il un jour rivaliser en vitesse avec Zed
(natif Rust, démarrage <500ms, GPUI = rendu GPU direct maison) ? Recherche
sourcée lancée (Workflow `wf_d88202f7-9fe`, 4 angles + synthèse). Résumé
durable :

**Ce qui rend Zed rapide** : pas le langage Rust en lui-même — l'absence
de tout moteur de mise en page classique (pas de XAML, pas de page web) et
l'envoi direct des formes au GPU via GPUI (framework maison de Zed). Preuve
citée : Tauri est écrit en Rust mais n'est pas rapide comme Zed, car il
garde une webview (donc un moteur de layout classique) sous le capot.
Rust apporte 2 avantages réels à CETTE architecture précise (pas de
ramasse-miettes qui met le programme en pause, pas de compilation à chaud
au démarrage) mais ne les garantit pas tout seul.

**Fait concret vérifié DANS le code de MOTO (pas une supposition
externe)** : `Moto.Editor/Controls/CodeEditorView.xaml` utilise un
`<WebView x:Name="Web">` — l'éditeur de code de MOTO est un navigateur
Chromium (WebView2) complet, qui démarre juste pour afficher du texte.
C'est probablement le plus gros frein réel à la vitesse de MOTO, avant
tout le reste (JIT, XAML, DI — les causes génériques MAUI documentées
dans les tickets officiels dotnet/maui, ex. #9179, #31227).

**WinUI 3 utilise déjà le GPU** (DirectComposition), mais seulement pour
la composition finale (assembler les couches, animations fluides) — le
calcul de layout, la mesure du texte, le dessin des contrôles restent
largement CPU. C'est la vraie différence structurelle avec GPUI, pas un
simple retard d'optimisation.

**3 options retenues, avec coût/gain** :
1. **Optimiser l'existant** (ReadyToRun, trimming + bindings compilés,
   retarder le chargement IA au démarrage) — coût faible (jours-semaines),
   plafond dur ~1-1,5s (jamais moins en gardant MAUI/WinUI3 tel quel).
   NativeAOT sur MAUI Windows spécifiquement pas encore mûr (bugs
   documentés, support complet visé après .NET 10, sans date ferme).
2. **Remplacer UNIQUEMENT le WebView de CodeEditorView par du rendu GPU
   direct** (SkiaSharp), reste de l'app inchangé en C#/.NET — même logique
   que Zed, appliquée à l'endroit qui compte le plus. Précédent réel :
   Windows Terminal a fait ce même remplacement ciblé pour son rendu de
   texte, gain mesuré ×2 à ×10. Coût moyen (semaines à quelques mois).
3. **Réécriture complète façon Zed** (Rust + moteur GPU maison) — seule
   voie qui atteint vraiment le niveau de Zed, mais ~76 000 lignes de C#
   à refaire ; précédents réels (Zed, Lapce, JetBrains Fleet) = années-
   personnes d'ingénieurs systèmes confirmés, pas une personne assistée
   d'IA. Risque d'abandon documenté (réécritures gelées des années,
   double maintenance abandonnée ailleurs). Coût fort.

**Séquence recommandée** : Option 1 avant v1.0 (gain quasi gratuit) →
Option 2 au palier "élevé/bêta" de Tom (le vrai gain perceptible,
chantier borné à un seul contrôle) → Option 3 seulement au palier
"commercial" si traction suffisante, et à ce moment-là plutôt en
apprenant/recrutant du Rust qu'en comptant sur l'IA seule (terrain où
l'assistance IA est la moins fiable). Aucune décision prise à ce stade —
juste la carte pour en reparler au bon moment.

## Agents autonomes en tâche de fond — jalons 1 et 2 livrés (03-04/09)

Demandé par Tom après avoir vu deux sessions Claude Code se parler entre
elles pour vérifier qu'elles ne travaillaient pas sur le même fichier. Il
a choisi l'option la plus ambitieuse ("vrais agents autonomes, accès
fichiers/terminal, se notifient en direct") — plan conçu ET vérifié
contre le vrai code (workflow de conception, 3 architectures + jugement +
synthèse, `Docs/probes/agent-messaging-design-2026-09-03.json`) avant
d'écrire quoi que ce soit, découpé en 3 jalons croissants.

**Jalon 1 (commit `9bdc6bf`), livré et testé de bout en bout** : commande
`/agent <objectif>` dans le chat existant (AiChatView, bandeau IA,
Accueil), AUCUNE nouvelle interface. Nouveau namespace
`Moto.Core.AI.Autonomy` :
- `AgentAction`/`AgentActionKind`/`AgentActionParser` : `MotoAiKernel.
  RouteAsync` ne fait aucun appel d'outil structuré (juste du texte) —
  le parseur extrait une action typée (ReadFile/WriteFile/RunCommand/
  Finish) d'un format à balises tolérant, jamais d'exception (repli sur
  `Malformed`, compté comme un pas raté contre le budget de la boucle).
- `IAgentTool` + `ReadFileTool`/`WriteFileTool`/`RunCommandTool`/
  `FinishTool` : une fine enveloppe par capacité réelle, `IsMutating`
  dit à la boucle quels appels DOIVENT passer par la confirmation.
- `BackgroundAgentLoop` : boucle bornée perçoit→décide→agit→observe
  (garde nombre de pas / durée / 3 refus consécutifs). Appelle SANS
  CONDITION `AiConfirmationService.RequestAsync` (le vrai mécanisme déjà
  existant, réutilisé tel quel) avant tout outil mutant — même exigence
  que SelfRepairAgent. Le texte de confirmation est construit
  UNIQUEMENT à partir des champs littéraux de l'action, jamais de
  l'auto-description du modèle.
- `BackgroundAgentService` : point d'entrée DI, démarre un run en tâche
  détachée (`Task.Run`), expose `ObservableCollection<AgentRunRecord>
  Runs` (même convention que `ChatService.Tasks`/`ChatTaskRecord`).

Réutilise 2 valeurs de `ConfirmationAction` déjà présentes mais jamais
utilisées ailleurs (`ModifyCode`/`ExecuteCommand`, vérifié) plutôt que
d'en ajouter de nouvelles.

**3 bugs réels trouvés en construisant/testant (pas supposés)** :
1. `TerminalService.ExecuteAsync` n'acceptait aucun `CancellationToken`
   — une commande qui ne se termine jamais aurait bloqué la boucle
   au-delà de sa propre garde de durée. Nouvelle surcharge annulable
   (tue le process si annulé) ; l'ancienne délègue dessus avec
   `CancellationToken.None`, comportement inchangé pour GitService.
2. `ConfirmationHandler` (`MainPage.Extensions.cs`) n'était jamais
   marshalé vers le thread UI — inoffensif tant que seul un clic (déjà
   sur le thread UI) l'appelait, aurait planté au premier agent (tâche
   d'arrière-plan touchant `ConfirmationOverlay` hors thread UI).
   `MainThread.InvokeOnMainThreadAsync` corrige pour tous les appelants.
3. **Le plus intéressant** : `/agent crée un fichier hello.txt...`
   contient "crée" ET "projet" — les 2 mots que `AutoProjectBuilder.
   ShouldHandle` (`MainPage.Routing.cs`) utilise pour détecter une
   demande de génération de projet complet. Sur le chemin Accueil/
   bandeau IA (`OnAiCommandSubmitted`) — SÉPARÉ du chemin AiChatView
   (`ChatService.PluginCommandHandler`, atteint uniquement depuis
   `SendAsync`) — la commande se faisait détourner : un vrai projet
   "MotoProject" générique était créé sur le disque à la place, trouvé
   en testant avec Tom (capture d'écran), pas deviné. `/agent`
   intercepte maintenant en premier sur les DEUX chemins.

Confirmé par Tom en conditions réelles : popup de confirmation avec le
vrai chemin/contenu affichés, "Autoriser" cliqué, fichier vérifié
PRÉSENT sur le disque avec le bon contenu.

**Jalon 2 (commits `092795b` + `8a11ba2`), livré et testé avec 2 agents
réels sur le même fichier** :
- `AgentMessageBus` (nouveau) : pub/sub en mémoire, UNE seule instance
  partagée en DI entre tous les runs (`BackgroundAgentService.
  MessageBus`) — c'est ce qui permet à deux agents lancés séparément de
  se voir. Historique plafonné à 30 messages.
- `SendMessageTool` (nouveau `AgentActionKind.SendMessage`, jamais
  mutant) : un agent peut vraiment envoyer un message à un autre (ou à
  tous) via le bus. `BackgroundAgentLoop` injecte dans le prompt de
  chaque tour les notes reçues depuis le tour précédent, et détecte les
  conflits (deux agents qui touchent le même fichier) pour prévenir
  l'agent concerné.
- `AgentAuditLog` (nouveau) : un fichier NDJSON par workspace (nom haché
  SHA256), une ligne par événement (proposition/décision/exécution),
  flush immédiat — exploitable même après un crash.
- Bug latent réel corrigé (identifié en lisant le code, pas supposé) :
  `ConfirmationOverlay` n'a qu'UN SEUL `TaskCompletionSource` partagé,
  sans file d'attente — sans danger tant qu'un seul point d'entrée
  humain existait, mais deux agents demandant une confirmation en même
  temps l'auraient corrompue. `AiConfirmationService.RequestAsync`
  sérialise maintenant via un `SemaphoreSlim` (comportement inchangé
  pour un appelant unique).

**2 bugs réels trouvés PENDANT le test avec Tom (2 agents écrivant
chacun une ligne dans le même `notes.txt`), corrigés dans la foulée** :
1. Les agents (petit modèle local) répétaient indéfiniment la même
   écriture au lieu de reconnaître l'objectif atteint. Garde-fou
   déterministe ajouté dans `BackgroundAgentLoop` : suivi de la
   dernière mutation réussie (Kind+Path) — au 2e doublon, avertissement
   injecté dans le contexte du modèle ; au 3e, la boucle force elle-même
   l'arrêt (`Completed`) indépendamment de la réponse du modèle. Testé :
   arrêt confirmé après seulement 2 demandes de confirmation.
2. `WriteFile` remplace TOUT le contenu du fichier (jamais documenté
   nulle part) — deux agents écrivant chacun une ligne s'écrasaient l'un
   l'autre. Règles ajoutées au prompt : lire le fichier avant d'y
   ajouter du contenu, ne jamais répéter une action qui a réussi.

**3e bug réel, trouvé en creusant une remarque de Tom ("les stats ne
s'additionnent pas")** : `HandleAgentCommand` ajoutait bien les messages
de progression de l'agent au thread de chat, mais — contrairement à
tous les autres points d'entrée du chat — n'appelait jamais
`RefreshHomeStats()` ensuite. Les tuiles Sessions/Messages/Tokens/
Patterns appris de l'Accueil ne bougeaient donc jamais pendant qu'un
agent travaillait. Corrigé (commit `8a11ba2`), testé : les tuiles
bougent bien en direct maintenant.

Cause distincte, PAS un bug, remise à plus tard (choix explicite de
Tom) : ces stats ne sont sauvegardées nulle part (`ChatService.Threads`
est en mémoire seulement, sans fichier de sauvegarde) — donc elles
repartent forcément à 0 à chaque relance de l'app. Un vrai système de
persistance des conversations serait un chantier séparé.

Limite connue, non corrigée (prévue au jalon 3) : sans dossier de
travail ouvert, les agents écrivent dans le dossier de l'exécutable
(`bin/Release/…`) faute de racine de workspace — sans danger mais pas
idéal.

**Jalon 3 (prochain, pas commencé)** : panneau "Agents en cours"
(première vraie UI de ce chantier) + confinement des chemins au dossier
du projet + confirmations groupées.

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
