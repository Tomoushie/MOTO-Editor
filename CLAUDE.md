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

**Ligne de base de compilation, mesurée le 22/09 (à utiliser comme
référence anti-régression)** : `Debug` **et** `Release` sur
`net8.0-windows10.0.19041.0` construisent à **0 erreur · 479
avertissements**. ⚠️ Les « ~300 warnings » annoncés par la présentation
projet (`Docs/Documents/…`) sont faux : la dette réelle est de **479**.
Un lot visuel ne doit jamais faire monter ce nombre. Build de contrôle :
`dotnet build Moto.Editor/Moto.Editor.csproj -f net8.0-windows10.0.19041.0`
(~20-30 s une fois la restauration faite ; la toute première restauration
peut prendre plusieurs minutes).

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

## Paliers de qualité de Tom — 2 catégories, 6 niveaux (échelle du 22/09)

L'ancienne échelle à 5 niveaux (cheap → faible → moyen → élevé → Commercial)
est remplacée : Tom évalue désormais **deux axes séparés**, chacun sur
6 niveaux — `cheap → faible → moyen → élevé → vendable → triple A`.

| Axe | Position au 22/09 | Références visées |
|---|---|---|
| **Visuel** | **cheap** | Zen Code, VS Code, JetBrains |
| **Backend / Structure** | **élevé** | — |

**Écart à combler en priorité : le VISUEL** (2 crans sous le backend). Tom
juge le rendu actuel « cheap » alors que l'architecture est déjà « élevé ».

- **Visuel** : hiérarchie, densité, typographie, espacements, états
  (survol/actif/désactivé), animations, cohérence des rayons/ombres, qualité
  des icônes, lisibilité des listes et des formulaires. Objectif final :
  indiscernable d'un IDE moderne payant.
  ⚠️ **Recalibrage important** : la présentation projet
  (`Docs/Documents/Présentation_détaillée_du_projet.html`, §3) affirme que le
  visuel est à « **Moyen en cours** » et que « cheap » et « faible » sont
  « dépassés ». C'est **plus optimiste que l'évaluation de Tom lui-même** —
  ne pas se fier à l'auto-évaluation de ce document.
- **Backend / Structure** : architecture, moteurs réels, 0 erreur de build,
  fonctionnalités réellement branchées. Le passage à « vendable » exige que
  **tout ce qui est annoncé fonctionne** (aujourd'hui, mesuré : 12 réglages
  opérants sur 324 déclarés, LSP/DAP/CRDT absents, cluster ONNX mort — voir
  bug #4 ci-dessous).
- **triple A** (les deux axes) : niveau VS Code / Zed / JetBrains en
  visuel **et** en profondeur fonctionnelle.

Objectif énoncé par Tom (22/09) : atteindre un produit **en production**,
« triple A moderne », équivalent à Zen Code / VS Code / JetBrains sur les
deux axes. C'est un objectif long, à découper — ne pas traiter comme un
chantier unique.

### Chantier visuel — état d'avancement (22/09)

Tom a donné son feu vert pour avancer EN AUTONOMIE sur le visuel **et** le
backend (« je te laisse continuer… je te répondrai une fois que tu as
terminé »). Les décisions ci-dessous ont donc été tranchées seul, chacune
documentée et réversible ; elles sont listées en questions dans le rapport
de fin de session.

| # | Décision | Choix retenu | Réversible en |
|---|---|---|---|
| D1 | Corps de texte | **13 px** (VS Code et JetBrains sont à 13 ; l'app était à 12) | 1 ligne de jeton |
| D2 | Accent | **#007ACC** (annoncé par la présentation ET QWEN.md ; les 3 références sont bleues). L'ancien orange est gardé sous `AccentWarm` | 1 ligne de jeton |
| D3 | Police | `Segoe UI Variable, Segoe UI` — **appliquée partout** via un style implicite de `Label` (seuls 9 fichiers sur 71 en déclaraient une, donc deux typographies cohabitaient). Inter embarquée non retenue pour l'instant | enlève 1 style |
| D4 | Interligne | **activé** (`LineHeight` sur les rôles ≥ 13 px) | par rôle |

- ✅ **Phase 0 — socle livré** (commit `71f6c21`, lot validé par
  `scripts/visual-lot-verify.ps1`) : 10 rôles typographiques (contrainte
  vérifiée : **MAUI 8 n'a pas `FontWeight`**, seule `FontAttributes` existe),
  5 jetons de rayon, espacements `Thickness` utilisables, 4 tailles d'icône,
  anneau de focus, et **`Disabled`/`Focused` ajoutés** (ils étaient absents de
  tous les styles de bouton).
- ✅ **Écran pilote — Accueil (`HomeView.xaml`) converti** aux jetons.
- ✅ **Phase 1 mécanique TERMINÉE** (commits `065a1c0`, `4c91651`, + passe complète) :
  **293 tailles de police et 177 rayons** convertis sur **47 fichiers**, via
  `scripts/visual-tokens-apply.ps1` (dry-run par défaut, `-Apply` pour écrire).
  État final mesuré sur le périmètre compilé : il ne reste QUE
  - les **5 valeurs de police ambiguës** (7, 11, 11.5, 18, 24 — 117 occurrences) que
    l'outil laisse volontairement : trancher « 11 px de badge » vs « 11 px de texte
    courant » par table produirait un contraste faux quelque part ;
  - des `RoundRectangle N` **déjà normalisés aux valeurs canoniques** (6/8/12),
    exprimés en nombre et non en jeton — volontaire, voir ci-dessous ;
  - les couleurs (138) et les espacements composés, qui demandent un jugement au cas par cas.
  ⚠️ **Piège réel rencontré et évité** : `StrokeShape="RoundRectangle {StaticResource …}"`
  est à NE PAS FAIRE — la valeur est passée telle quelle à un `TypeConverter` qui
  attend du texte, et un mélange texte + extension de balisage compile peut-être mais
  rend faux EN SILENCE, sans qu'aucun contrôle automatique ne le voie. L'outil
  normalise donc vers la valeur numérique canonique du jeton.
- ⏭️ **Reste** : les 5 valeurs de police ambiguës (jugement à l'œil), les 138
  couleurs, les espacements composés, puis Phase 2 (composants), Phase 3 (écrans),
  Phase 4 (mouvement et animations — aucun jeton de mouvement créé pour l'instant).
- ⚠️ **Aucun test visuel automatique** : le garde-fou garantit qu'un lot ne
  casse rien (0 erreur, avertissements ≤ 479, périmètre, aucun comportement
  touché) mais **pas** que le résultat soit joli — seul l'œil de Tom juge.

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
4. **Réglages : 12 opérants sur 324 déclarés — soit 3,7 %.** Mesuré le 22/09
   par `scripts/settings-coverage.ps1` (rapport :
   `Docs/design/Couverture-reglages.md`), sur le **périmètre réellement
   compilé** (562 fichiers .cs ; les 98 fichiers exclus du build sont
   écartés, sinon on compterait comme « opérant » un réglage lu par du code
   mort — c'est le cas des `ai.embedded.*`, lus par le cluster ONNX non
   compilé).
   Le chiffre « 4 » qui figurait ici venait de la seule lecture de
   `SettingsApplier.ApplyAll()` : il n'applique effectivement que 4 clés
   (`theme_mode`, `buffer_font_size`, `minimap_show`, `lsp_diagnostics`),
   mais 8 autres sont lues ailleurs dans du code compilé
   (`context_engine_enabled`, `doc_auto_update`, `doc_on_project_open`,
   `ollama_endpoint`, `ollama_model`, `ollama_timeout_seconds`,
   `platform_auto_detect`, `power_mode`).
   **Les 312 inertes se répartissent par catégorie** — et le plus gros
   cluster correspond à des **interfaces qui EXISTENT déjà mais ignorent leur
   configuration** : `Fenêtre & Layout` 50 (onglets `tabs_*`, barre de titre
   `tb_*`, barre de statut `sb_*`, aperçus `preview_*`), `Panneaux` 44
   (explorateur `pp_*`, panneau Git `gp_*`, panneaux agent/chat/debug/outline
   `ap_*`/`cp_*`/`dp_*`/`op_*`), puis AI 31, Agent 28, Éditeur 25,
   Terminal 22, Apparence 17, Version Control 17, Recherche & Fichiers 17.
   C'est **le plus grand écart « affiché mais inactif » de l'app**, et le
   verrou direct du palier « élevé → vendable » (la règle étant « tout ce qui
   est annoncé fonctionne »). Effort : non pas 312 chantiers isolés, mais
   quelques familles cohérentes à câbler sur de l'UI existante.
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

## Réglages → IA Locale (02/09, depuis "Docs/product/Idées-à-implémenter.txt")

`Docs/product/Idées-à-implémenter.txt` (fichier de Tom) mélange une vision très
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
`Contexts` (ObservableCollection\<ChatContextItem\>, pièces jointes — **vitre
d'affichage de la conversation ACTIVE depuis le 22/09** : la source de vérité
est `_pendingByThread`, une file PAR conversation ; voir la limite corrigée
plus bas), `PreferInternal` (bool), `CreateThread()`, `AddFile(path)`,
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
- ✅ **CORRIGÉ (22/09) — sac de pièces jointes global.** `Contexts` était une
  collection UNIQUE partagée par toutes les conversations : joindre un fichier
  dans une conversation, en changer (`SwitchThread` place le thread choisi en
  tête, donc change le thread actif), puis envoyer faisait voyager la pièce
  jointe vers le mauvais message. Les pièces jointes vivent maintenant **par
  conversation** (`ChatService._pendingByThread`, clé = instance de
  `ChatThread` — ce modèle ne redéfinit ni `Equals` ni `GetHashCode`, vérifié),
  et `SendAsync` consomme la file du thread **qui reçoit le message**, plus le
  sac affiché. `Contexts` reste **la même instance observable** (celle que lie
  `AiChatView.ContextList`) : elle n'est plus la source de vérité, elle affiche
  la conversation active. Aucune signature publique modifiée, aucune liaison
  XAML touchée, **cas mono-conversation strictement inchangé**.
  C'était le **prérequis bloquant de la vue fractionnée** (deux conversations
  affichées en même temps ne peuvent pas partager un seul sac). Compile à
  0 erreur / 479 avertissements (ligne de base inchangée), mais **non testé
  comportementalement** : `ChatService` vit dans le projet MAUI `Moto.Editor`
  (Exe), non référençable depuis `Moto.Tests` qui ne référence que `Moto.Core`
  — à valider par Tom (joindre un fichier, changer de conversation, vérifier
  que la pièce jointe reste dans la bonne).
- Cliquer "nouvelle conversation" pendant qu'une réponse est en attente fait
  atterrir cette réponse dans un thread devenu invisible : un peu mieux
  depuis le 03/09 (la fenêtre "Conversations" ci-dessus permet de le
  retrouver et d'y revenir), mais toujours pas de sélecteur d'historique
  intégré directement DANS le panneau de chat principal — reste une fenêtre
  séparée à ouvrir via la palette, pas un clic sur place.

## Barre de titre bleue Windows — chantier "rendu 100% custom" EN COURS (08/09)

**Mise à jour du 22/09 : ce chantier n'est PAS en pause, contrairement à ce
que disait la version précédente de cette section.** Tom a accepté le 08/09 de
passer au "rendu 100% custom" après épuisement des 6 tentatives
"coopératives" — 6 commits du 08/09 (`174ebd0` → `73f7ab1`) documentent la
séquence complète. HEAD est toujours sur `73f7ab1`.

**Cause réelle de la bande bleue, toujours valable** (pas une hypothèse,
établie le 02/09) : réglage Windows 11 "Afficher la couleur d'accentuation sur
les barres de titre" — documenté par Microsoft (fixer TOUTES les couleurs de
la titlebar est recommandé mais pas garanti) et par un ticket GitHub toujours
ouvert et jamais résolu (`microsoft-ui-xaml#9374`, même symptôme).

### Les 4 tentatives "coopératives" supplémentaires (08/09), toutes échouées

Toutes testées **EN DIRECT** (build Debug relancé pour de vrai, capture
d'écran + journal `Breadcrumb` relu) — aucune n'est une supposition, et toutes
échouent avec le **même symptôme** :

| # | commit | approche | résultat |
|---|---|---|---|
| 3 | `174ebd0` | `ExtendsContentIntoTitleBar=false` (jamais réactivé, y compris dans `window.HandlerChanged`) + `SetBorderAndTitleBar(false,false)`, posés une seule fois, sans faire cohabiter les 2 approches (contrairement au 02/09 qui crashait) | pas de crash cette fois, redimensionnement au bord OK, **bande bleue inchangée** → revert propre |
| 4 | `bbe5e94` | + `DwmSetWindowAttribute` (P/Invoke `dwmapi.dll`) : `DWMWA_CAPTION_COLOR`/`BORDER_COLOR`/`TEXT_COLOR` | `hr=0` (S_OK, acceptés pour de vrai, 8 réapplications observées) **et pourtant bande inchangée** → gardé (recommandation officielle Microsoft, isolé dans son try/catch, aucun effet de bord) |
| 5 | `5e5b33e` | combinaison 3+4, avec `ApplyDwmAttributeColors` extraite en méthode séparée pour ne jamais reposer `ExtendsContentIntoTitleBar=true` | mêmes `hr=0`, bande TOUJOURS inchangée → revert propre |
| 6 | `e4ccb91` | `DWMWA_SYSTEMBACKDROP_TYPE=DWMSBT_NONE` — hypothèse : un backdrop Mica auto-choisi par DWM serait le déclencheur documenté par #9374 (0 occurrence de `Mica`/`Acrylic`/`SystemBackdrop` dans le dépôt avant ça, vérifié) | `hr=0`, bande TOUJOURS inchangée → gardé |

**Enseignement dur, ne plus retester ces 4 API** : Windows **accepte** la
demande (`hr=0`) puis peint quand même l'accentuation par-dessus. Le paint a
lieu au niveau du compositeur DWM, indépendamment d'`AppWindowTitleBar` ET
d'`OverlappedPresenter`. Ceci **confirme** l'interdiction déjà écrite dans
`QWEN.md` §9 (« ne plus jamais retenter `SetBorderAndTitleBar` ») — la
tentative 3 l'a re-vérifiée sur un stack qui ne crashait plus.

### Incrément 1 — sans bordure PERMANENTE + zones de redimensionnement (`e4a92ed`)

La fenêtre est désormais sans bordure **permanente et jamais réversible**
(choix explicite, contrepartie acceptée du rendu custom) :
- `App.xaml.cs` (`window.HandlerChanged` ET `OnWindowsWindowCreated`) :
  `ExtendsContentIntoTitleBar=false` partout, `SetBorderAndTitleBar(false,false)`
  sur l'`OverlappedPresenter` (avec un log d'avertissement si le présentateur
  n'est pas du type attendu).
- **`ApplyTitleBarColors` n'est plus appelée du tout** — elle reposerait
  `ExtendsContentIntoTitleBar=true` et annulerait le mode. Remplacée partout
  par `ApplyDwmAttributeColors` seule (couleurs DWM + backdrop + coins).
- ⚠️ **Dette de code réelle, vérifiée le 22/09** :
  `SnapLayoutsHelper.ApplyTitleBarColors` n'a plus **AUCUN appelant** dans
  tout `Moto.Editor` (recherche complète) mais son commentaire XML affirme
  encore qu'elle « reste donc le correctif "sûr" en usage » — texte écrit à
  l'étape `bbe5e94` puis rendu faux par `e4a92ed`, jamais relu depuis. À
  corriger (ou à exclure proprement du build) quand le chantier sera stabilisé.
- **Zones de sécurité** (le terme de Tom) recréées : `ConfigureResizeBorders`
  (nouveau) enregistre les 4 zones `TopBorder`/`LeftBorder`/`BottomBorder`/
  `RightBorder` — sans bordure native, Windows n'a plus AUCUNE zone de
  redimensionnement. Épaisseur 6 DIP (valeur standard Windows) convertie par
  `DragZoneHelper`, recalculée à chaque `AppWindow.Changed` (`DidSizeChange`).
  Top/Bottom couvrent toute la largeur, Left/Right s'arrêtent entre les deux
  pour éviter un double enregistrement de zone. `NonClientRegionKind` n'a
  **pas** de valeur "coin" (vérifié sur learn.microsoft.com, WASDK 1.8 — pas
  une supposition) : les coins fonctionnent par intersection de 2 bordures.
- `DWMWA_WINDOW_CORNER_PREFERENCE=DWMWCP_ROUND` ajouté, pour ne pas perdre
  les coins arrondis Windows 11 maintenant que la fenêtre est sans bordure.

### Incrément 2 — plein écran manuel F11 (`73f7ab1`)

`SnapLayoutsHelper.ToggleFullScreen` (`AppWindowPresenterKind.FullScreen`) +
**F11** via `GlobalHotkeyService` (nouveau paramètre `onToggleFullScreen`,
même patron que Ctrl+B/F5), câblé dans `MainPage.xaml.cs`. **0
`VirtualKey.F11` dans tout le dépôt** avant ce commit (vérifié).
**Piège réel évité** : `SetPresenter(Overlapped)` en sortie de plein écran
recrée un présentateur par défaut AVEC bordure →
`SetBorderAndTitleBar(false,false)` est réappliqué immédiatement après, sinon
toute la personnalisation sans-bordure était perdue à chaque F11.

### Ce qui est vérifié, et ce qui reste ouvert

**Vérifié en direct le 08/09** : `ConfigureResizeBorders` calcule des rects
cohérents avec la vraie taille de fenêtre (journal : `taille=1632x1263
épaisseur=6px`) ; double-clic sur la zone de titre → maximiser puis restaurer
corrects ; bouton Maximiser custom correct ; F11 → plein écran réel (barre des
tâches masquée, contrairement à Maximiser) et sortie propre avec sans-bordure
bien réappliqué ; coins arrondis acceptés par DWM (`hr=0`) ; aucune exception
sur toute la séquence (launch → maximiser → restaurer → plein écran → fermer),
fermeture propre par Alt+F4. **Constat en passant** : la bande bleue est
**ABSENTE en plein écran** (elle revient en mode fenêtré) — cohérent avec les
6 tentatives, mais pas une piste de correctif (on ne va pas forcer le plein
écran en permanence).

**Toujours OUVERT (à ne pas croire réglé)** :
- La bande bleue elle-même (cosmétique, inchangée) en mode fenêtré.
- **Glissé de redimensionnement pixel-précis aux bords/coins NON confirmé par
  un humain** — limite de l'outil de capture utilisé (bordure de 6px plus fine
  que la précision d'un clic-glissé estimé sur une capture compressée), pas du
  code. À faire tester par Tom.
- **Snap Windows (Survol du bouton Maximiser + flèches) non vérifié** après le
  passage en sans-bordure.
- Animations minimiser/restaurer (non traitées).
- DPI multi-écrans : `DragZoneHelper` utilise une densité globale, pas
  par-écran.
- Détail historique du chantier : mémoire Claude
  `moto-editor-titlebar-msix-investigation`.

### Décision de Tom (22/09) — le réglage Windows n'est pas une solution produit

Tom a désactivé lui-même, sur sa machine, le réglage Windows 11 « Afficher
la couleur d'accentuation sur les barres de titre et les bordures de
fenêtre » — le réglage identifié comme cause racine depuis le 08/09. Ce
test confirme le diagnostic, mais **ne règle rien pour un client** : MOTO
Editor ne peut pas demander à chaque utilisateur de modifier un réglage
Windows global à l'installation. Décision explicite : poursuivre le
chantier "rendu 100% custom" jusqu'à élimination réelle de la bande, côté
logiciel uniquement, plutôt que de considérer le sujet clos.
Vu la taille du travail restant (probablement gestion native de
`WM_NCCALCSIZE`/`WM_NCHITTEST`, plus risqué que les incréments 1-2), passera
par l'Orchestrator plutôt qu'en retouche directe — voir section Rust/vitesse
ci-dessous, décision liée.

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
  (`Docs/product/Roadmap.md`), pas encore construit. `RoslynLanguageServerClient.cs`
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

## Contraintes d'API visuelles — MAUI 8 (vérifié le 22/09, à ne pas supposer)

Vérifié dans la source officielle MAUI **8.0.100** (version réellement
résolue : `Microsoft.Maui.Controls.Core/8.0.100`, lue dans
`Moto.Editor/obj/project.assets.json` — pas la 10.0.20 qui traîne aussi dans
le cache NuGet et induit en erreur) :

- `Label` expose `FontSize`, **`FontAttributes` (`None`/`Bold`/`Italic`) —
  c'est la SEULE graisse disponible**, `LineHeight`, `CharacterSpacing`,
  `TextTransform`, `FontFamily`, `FontAutoScalingEnabled`, `Opacity`.
- **`FontWeight` N'EXISTE PAS** en MAUI 8 (ajouté plus tard, en MAUI 10).
  Toute hiérarchie typographique doit donc reposer sur **taille + Gras ou
  non + couleur/opacité + espacement des lettres**, jamais sur des graisses
  numériques type 400/500/600. Concevoir « comme sur le web » est une
  impasse ici.

**Ceci explique enfin une exclusion qui n'était pas comprise** : les 7 vues
qui utilisent `FontWeight="SemiBold"`/`"Bold"` sur des `TextBlock`
(`AdminDashboardView`, `AdvancedAiSettingsView`, `ModelConsentDialog`,
`PerformanceDashboardView`, `RefactorPanel`, `SubscriptionOverlay`,
`StatusBarView`) sont **toutes** dans la liste `MauiXaml Remove` du
`.csproj`. Vérifié : `FontWeight` n'apparaît **que** dans des fichiers
exclus du build — corrélation parfaite. Elles ont été écrites en XAML
**WinUI** dans un projet MAUI (comme `TextBlock`, qui n'existe pas non plus
en MAUI) : elles ne compileraient pas. Ce n'est donc pas un oubli de
câblage, c'est du code non portable.

Voir `Docs/design/Langage-visuel-spec.md` pour l'échelle typographique
construite sur cette contrainte.

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
VS Code (fait-maison, sans dépendance, léger), la barre bleue étant alors
laissée de côté (elle a depuis son propre chantier, voir la section
"rendu 100% custom" plus haut). Sonde en lecture seule sur 5 domaines avant de
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

### Décision de Tom (22/09)

- **Option 3 (réécriture Rust complète) confirmée pour plus tard** : Tom
  aimerait le faire "de toute façon", mais explicitement **à la fin,
  quand le logiciel sera opérationnel** — pas maintenant. Aucun changement
  à la séquence recommandée ci-dessus, juste une confirmation actée.
- **Option 2 (remplacer le WebView de `CodeEditorView` par un rendu
  SkiaSharp direct) approuvée pour démarrer**, sans attendre le palier
  "élevé/bêta" — motivation de Tom : battre Zed en légèreté/rapidité.
  Chantier de taille "coût moyen" (semaines à quelques mois) : passera par
  l'Orchestrator (codegen substantiel), pas en retouche directe. Cadrage
  écrit fait le 22/09 : `Docs/design/Cadrage-CodeEditor-SkiaSharp.md`
  (périmètre = 2 fichiers seulement, contrat public à 7 membres, vrai coût
  = réimplémenter saisie/sélection/undo — pas le rendu Skia lui-même —
  découpage en 5 incréments proposé). Choix du découpage à trancher par
  Tom avant le premier envoi à l'Orchestrator.
- Lié : le chantier "rendu 100% custom" de la barre de titre (voir section
  dédiée plus haut) suit la même logique — même motivation de légèreté,
  même passage prévu par l'Orchestrator.

## Agents autonomes en tâche de fond — jalons 1, 2 et 3 livrés (03-04/09)

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

**Jalon 3 (commit `ffafe40`), livré et testé de bout en bout** :
- `AgentRunsView` (nouveau) : PREMIÈRE vraie interface de ce chantier
  (jalon 1/2 n'en avaient aucune, tout passait par le chat). Liste les
  `AgentRunRecord` en direct (statut/durée/objectif), bouton "Arrêter"
  par run, aperçu des messages entre agents, bouton pour ouvrir le
  dossier des journaux NDJSON. Ouverture via `Ctrl+Maj+P` → "Agents en
  cours" (même patron que les autres fenêtres spécialisées : `WindowKind.
  AgentRuns`, `OpenSpecializedWindow("agentruns")`).
- `AgentPathResolver.IsWithinRoot` (nouveau) : confinement — refuse tout
  ReadFile/WriteFile dont le chemin résolu sort du dossier du projet
  (ou du dossier courant si aucun workspace n'est ouvert), AVANT même de
  proposer une confirmation à l'humain.
- Indice de commande dangereuse (rm/del, git push --force, format/
  diskpart, curl|sh) affiché dans la confirmation — jamais un blocage,
  la décision reste entièrement humaine.
- `AgentGlobalBudget` (nouveau) : plafond de 200 appels IA partagé entre
  TOUS les agents de la session, au-delà du `maxSteps` propre à chaque run.

**Bug réel trouvé EN CONCEVANT ce panneau** (jamais vécu avant, faute
d'interface) : `BackgroundAgentService.Start` enveloppait la boucle dans
`Task.Run` — invisible sans UI, mais un vrai panneau liant `Status`/
`Steps` en direct y aurait planté ou mal affiché (MAUI exige que les
objets liés à l'UI soient modifiés sur le thread UI). Corrigé en
retirant `Task.Run` : `RunAsync`, toujours démarré depuis le thread UI,
reprend ses `await` sur le `SynchronizationContext` de l'UI — même
mécanisme déjà éprouvé par `ChatService.RunTrackedAsync`, aucune
gymnastique de marshaling ajoutée.

Point du plan original volontairement écarté (décision explicite de
Tom) : les "confirmations groupées" pour un changement multi-fichiers
n'ont pas de prise avec l'architecture actuelle (un agent ne propose
qu'UNE action à la fois) — les ajouter demanderait de réécrire la boucle
pour qu'un agent puisse proposer plusieurs actions d'un coup, un
chantier à part entière, pas fait.

Testé de bout en bout avec Tom (04/09) : panneau qui s'ouvre et se met à
jour en direct, bouton Arrêter (y compris annulation en plein milieu
d'une écriture — le run se termine proprement en "Annulé", sans
plantage), confinement qui refuse automatiquement un chemin hors projet
(`C:\Windows\test.txt`), avertissement affiché sur une commande `del`,
bouton d'ouverture du dossier des journaux. Petit oubli trouvé au
premier test (statut affiché en anglais brut, `Status.ToString()`) et
corrigé dans la foulée (`StatusLabel`, libellés français).

**Correctif hors-jalon (commit `e145714`), traité tout de suite après —
Tom a choisi de l'adresser plutôt que de le laisser en limite connue** :
`HandleAgentCommand` passait `_currentRoot ?? string.Empty`, qui
atterrissait sur `Directory.GetCurrentDirectory()` (le dossier de
l'exécutable) sans workspace ouvert. Remplacé par `GetWorkspaceRoot()`,
la convention DÉJÀ établie ailleurs dans l'appli pour ce même cas
(`AutoProjectBuilder`, `AiSettingsService`, dossier des plugins) :
`Documents\MotoProjects`. Rien de nouveau inventé — juste réaligné sur
l'existant. Testé avec Tom : `/agent` sans projet ouvert écrit bien dans
`Documents\MotoProjects`.

Ce chantier des 3 jalons prévus (plus ce correctif) est maintenant CLOS.
Toute suite (persistance des runs entre sessions, planification de tâches récurrentes,
etc.) serait un nouveau chantier, pas une continuation de celui-ci.

## Agents de diagnostic (Syntax/Complexity/Consistency/Pattern) — livré (04/09)

Demandé par Tom (liste relayée d'un autre outil, vérifiée avant de
construire — voir mémoire dédiée). Découverte clé : MOTO Editor contient
déjà une famille `ISpecializedAgent`/`SpecializedAgentRegistry`
(sécurité, secrets/PII, TODO, format, complexité approximative,
squelettes de tests…) — entièrement câblée en DI mais **jamais résolue
ni appelée par aucune UI** (code mort confirmé). Décision : diagnostics
d'abord, action (Refactor/Test/Doc, qui écriraient des fichiers) plus
tard comme préréglages du système `/agent` déjà confirmé-gaté — pas de
nouveau mécanisme de sécurité à inventer pour ceux-là.

`Moto.Core/Moto.AI/Agents/DiagnosticAgents.cs` (nouveau) : `SyntaxAgent`
(équilibre accolades/parenthèses/crochets), `ComplexityAgent` (points de
décision, toujours un constat rendu), `ConsistencyAgent` (casse des noms
de méthodes, limité à un seul fichier), `PatternAgent` (imbrication
profonde + nombres magiques, toujours "info"). Tous des `HeuristicAgent`
(jamais de LLM, jamais mutants — donc jamais de confirmation à
demander : ils ne font que lire/signaler). `StripStringsAndComments`
(nouveau, dans `HeuristicAgent`) : tokenizer léger qui ignore chaînes/
commentaires pour réduire les faux positifs — "PAS de parsing profond"
reste la règle.

Commande `/diagnose [chemin]` (sans argument : fichier actif de
l'éditeur) — dispatche 9 agents (les 4 ci-dessus + 5 existants
réveillés). `DependencyRiskAgent`/`TestFlakinessAgent`/
`AgentCostEstimatorAgent` exclus délibérément (CodeSnippet d'une autre
forme, produirait des constats absurdes sur du code source).

**3 bugs réels trouvés en câblant ce code mort à une vraie commande pour
la première fois** :
1. Dépendance circulaire dans `AgentCostEstimatorAgent` (son constructeur
   demandait le registre, qui a besoin de tous les agents — dont lui —
   pour se construire) : faisait échouer TOUTE résolution du registre.
   Corrigé via `IServiceProvider` résolu tardivement dans `ExecuteAsync`.
2. Le rapport n'apparaissait nulle part tant qu'AiChatView n'était pas
   ouvert (`/agent` a le même défaut, masqué par sa popup de
   confirmation). `ShowAiReplyAsTab` (nouveau, factorisé depuis le
   mécanisme déjà existant pour une réponse IA normale) ouvre maintenant
   la réponse comme onglet fichier pour `/agent` ET `/diagnose`.
3. Cet onglet s'ouvrait bien mais restait VIDE : `OpenFilePath` crée le
   document avec `Text=""` puis le sélectionne, ce qui déclenche
   `LoadDocumentIntoEditor` AVANT que le texte correct soit posé dessus.
   Un second appel explicite corrige — bénéficie aussi à la réponse IA
   normale au passage.

Testé de bout en bout avec Tom sur un vrai fichier du dépôt
(`MotoKernel.cs`) : rapport correct et lisible.

### Bandeau IA flottant invisible sur les fichiers longs (commit `7daeda6`)

Trouvé par Tom EN TESTANT `/diagnose` : la barre IA flottante (`AiBar`)
n'apparaissait qu'en défilant jusqu'au bout du fichier ouvert. 2 bugs
réels empilés :
1. `CodeEditorView` héberge un `WebView` (coloration syntaxique) — sous
   Windows, un WebView a sa PROPRE fenêtre native ("airspace") qui
   s'affiche TOUJOURS par-dessus le reste du XAML, quel que soit l'ordre
   de déclaration dans le Grid (limitation connue de la plateforme, pas
   un bug d'ordre de calque classique — un simple z-index ne peut pas le
   résoudre). Corrigé en réservant ~110px en bas de `CodeEditorView`
   (`EditorPaneView.xaml`, hauteur d'`AiBar` + sa marge) où le WebView ne
   s'étend jamais, quel que soit le défilement du code à l'intérieur.
2. Une fois cet espace réservé, un second bug est devenu visible :
   `AiBar` n'apparaissait dedans qu'après 30s à 1min, au hasard d'une
   prochaine réactivation de la FENÊTRE (alt-tab, etc.) — seul
   déclencheur existant (`GlobalHotkeyService.Register`,
   `onWindowActivated`). Ouvrir un fichier ne la montrait jamais par
   elle-même. Corrigé : le `CollectionChanged` de
   `_viewModel.Documents` (qui la cachait déjà à la fermeture du dernier
   fichier) l'affiche maintenant aussi à l'ouverture d'un document.

Testé avec Tom sur un vrai fichier long (`CLAUDE.md`, 800+ lignes) :
apparaît instantanément, y compris en haut du fichier.

### `/refactor`, `/test`, `/doc` + 3 pannes tierces trouvées en testant (commits `fa415b3`, `bc71d3a`, `ac6dfce`, `828b232`)

Phase B du chantier : la partie qui écrit réellement des fichiers.
PAS de nouveaux agents autonomes séparés — `HandlePresetAgentCommand`
(`MainPage.Extensions.cs`) construit une instruction pré-écrite et
délègue tel quel à `HandleAgentCommand`, donc hérite automatiquement de
tout ce qui existe déjà pour `/agent` (confirmation humaine,
confinement de chemin, budget global, panneau "Agents en cours").
Câblé aux deux points d'entrée existants, même priorité que `/agent`
et `/diagnose`.

En testant avec Tom le 05/09, 3 pannes réelles trouvées EN CASCADE —
**aucune dans ce code-là** :

1. **Le dépôt ne compilait plus du tout.** Un chantier tiers en cours
   (non committé, probablement un autre outil IA que Tom utilise en
   parallèle) avait renommé `_ollama`→`_localAi` dans
   `MotoAiKernel.cs` sans mettre à jour son usage (`CS0103`), et un
   nouveau fichier `LocalModelService.cs` appelait
   `SettingsEngine.GetDouble`, qui n'existait pas. Confirmé avec Tom
   avant de toucher à du code tiers en cours (`AskUserQuestion`) →
   corrigé au minimum, sans changer l'intention de l'autre chantier
   (commit `bc71d3a`).
2. **Une fois compilable, plus aucune action réelle ne sortait des
   agents** (0 seconde, "Aucun échange" dans le panneau, rien dans le
   journal d'audit). Cause : le nouveau chemin `_localAi` →
   `AiProviderManager.CompleteWithFallbackAsync` est un SQUELETTE —
   moteur interne toujours en échec (jamais implémenté, juste un
   commentaire disant qu'il devrait l'être un jour) et aucun
   fournisseur externe configuré sur CETTE instance précise (une page
   de réglages ailleurs configure une AUTRE instance sans lien).
   Confirmé avec Tom (`AskUserQuestion`) → `TryOllamaAsync` rebranché
   sur un appel Ollama direct (`_ollamaDirect`, nouveau champ,
   identique au mécanisme déjà éprouvé jalons 1-3) ; `_localAi` laissé
   intact pour le chantier tiers (commit `ac6dfce`).
3. **Plantage réel de toute l'application** pendant un test, cause
   identifiée dans `%TEMP%\moto-editor-crash.log` :
   `ArgumentException` sur `AppWindowTitleBar.set_ExtendsContentIntoTitleBar`
   (`SnapLayoutsHelper.ApplyTitleBarColors`), déclenché par un
   changement de focus de fenêtre — SANS RAPPORT avec `/agent`, même
   famille que l'enquête "barre bleue" déjà en pause (voir mémoire
   dédiée). Cause exacte non élucidée (hors périmètre) ; try/catch
   ajouté pour que ce raté ponctuel ne fasse plus planter toute l'app
   (commit `828b232`). Confirmé par Tom : comportement visuel
   inchangé après coup (barre bleue toujours présente, connue,
   séparée).

**Confirmé de bout en bout par Tom** après ces 3 correctifs :
`/agent crée un fichier confirmation-test.txt contenant "ok"` → popup
de confirmation, autorisation, fichier créé.

**4e panne, trouvée le même jour — théorie "fichier trop gros" ci-dessus
FAUSSE, corrigée ici** : `/refactor` échouait aussi sur un fichier de 14
lignes (`TEST-REFACTOR-A-SUPPRIMER.cs`), donc ce n'était pas une
question de taille. Cause réelle trouvée en lisant le journal d'audit
directement (`%LOCALAPPDATA%\MotoEditor\AgentAudit\*.ndjson`) : le
garde-fou anti-répétition ne couvrait QUE les actions qui écrivent
(WriteFile/RunCommand) — un modèle local pouvait relire le même fichier
(ReadFile) indéfiniment sans jamais proposer d'écriture, sans que rien
ne l'arrête, jusqu'à épuiser son budget de pas. Corrigé (commit
`08194a6`) : garde-fou symétrique pour les actions non-mutantes (rappel
qui se durcit à chaque répétition, poussant vers WriteFile/Finish) +
journal étendu (`malformed`/`non_mutating_step`) pour voir enfin ce que
le modèle répond à CHAQUE pas, pas seulement ceux qui écrivent. Testé
par Tom : `/refactor` produit maintenant une vraie proposition
d'écriture avec confirmation sur ce fichier.

**Pannes 5 à 9, même journée, commit `c5ae7e8`** — toutes des défauts de
TOLÉRANCE du parseur (`AgentActionParser.cs`) face à qwen2.5-coder:7b,
jamais un bug de câblage, chacune diagnostiquée en lisant le journal
d'audit NDJSON en clair avant d'écrire le correctif :
5. `SUMMARY:` parfois placée À L'INTÉRIEUR du bloc `CONTENT: <<< ... >>>`
   au lieu de juste après → écrite telle quelle dans le fichier cible
   (casserait la compilation d'un vrai fichier de code). Filet dans
   `ExtractDelimitedContent` : une dernière ligne de contenu qui
   ressemble à `SUMMARY:` est retirée et récupérée comme résumé.
6. `Finish` ne laissait AUCUNE trace dans le journal — indiscernable
   d'une vraie fin de tâche. Journalisé comme les autres pas
   (`BackgroundAgentLoop.cs`).
7. Verbes d'action inventés collés au vocabulaire de l'objectif
   (`RefactorFile`, `RefactorCode`, `RefactorContent`...) au lieu du nom
   d'outil exact. Remplacé la liste exacte par un patron large : tout
   verbe commençant par "refactor", ou contenant write/update/modify,
   vaut `WriteFile`.
8. La formulation des instructions ("PATH: chemin/relatif au projet")
   ressemblait à un chemin littéral à cause du "/" — recopiée telle
   quelle comme PATH, repoussée à raison par le confinement de chemin.
   Reformulée avec un exemple sans ambiguïté (`BackgroundAgentLoop.cs`).
9. Tous les champs parfois sur une seule ligne séparés par " | ", et/ou
   délimiteurs `<<< >>>` du contenu parfois complètement omis — les deux
   faisaient disparaître du texte silencieusement. Champs "|"-joints
   séparés en vraies lignes avant le parsing (sans toucher un "|"
   légitime dans du code) ; contenu sans délimiteurs accepté, s'arrête
   dès qu'une autre balise commence.

**CONFIRMÉ PAR TOM (06/09)** : après ces 9 correctifs cumulés, `/refactor`
sur `TEST-REFACTOR-A-SUPPRIMER.cs` aboutit à une vraie proposition
d'écriture bien formée, popup de confirmation, autorisation, et un
fichier réellement réécrit — vérifié directement sur disque (pas
seulement via le popup). Le mécanisme confirmation → écriture réelle
fonctionne de bout en bout. Ce fichier de test (et le `confirmation-test.txt`
du test `/agent`) a été **retiré du dépôt le 22/09** (artefacts non
versionnés) — les recréer au besoin pour un prochain test. Non résolu, à
garder en tête : pas testé
sur un modèle local plus costaud (qwen2.5-coder:14b, Qwen3.8-27B,
gpt-oss:20.9B, tous disponibles) — qwen2.5-coder:7b reste petit et peut
révéler de nouvelles variantes de format non encore vues.

## Références

- Mémoire Claude (`~/.claude/projects/E--Corpus/memory/`) : chercher les
  fichiers `moto-editor-*` pour le détail complet de chaque chantier
  (panneaux modulaires, réglages, revue du panneau IA, barre de titre...).
- `QWEN.md` (racine du dépôt) : équivalent pour Qwen, utilisé par Tom pour
  lui donner du contexte manuellement (pas de lecture automatique).
  **Versionné dans git depuis le 22/09** — il ne l'était pas avant et
  n'existait qu'en copie locale, donc à risque. Corrigé le même jour après
  vérification dans le vrai code : il annonçait « .NET 10 /
  `net10.0-windows10.0.19041.0` » alors que **tout le dépôt cible
  `net8.0`** (vérifié dans les `.csproj`), et « 97 réglages » alors que le
  catalogue en compte **297**. Quand l'un des deux fichiers change un fait
  durable, mettre à jour l'autre.
- **Organisation de `Docs/` (réorganisée le 22/09 — à connaître avant de
  chercher un document)**. La racine de `Docs/` ne contient plus que
  `README.md` (vitrine, c'est ce que voit GitHub car il n'existe AUCUN
  README à la racine du dépôt) et `index.md` (sommaire). Tout le reste est
  rangé en 4 sous-dossiers thématiques :
  - `Docs/architecture/` — `Architecture.md` (ex-`Architecture.txt`),
    `Arborescence.md` (ex-`.txt`), `Modules-overview.md` (ex-`Licence.md` :
    ce fichier n'a **jamais** contenu de licence, seulement un schéma ASCII ;
    il portait un nom trompeur), `Moteurs-IA-internes.md`
    (ex-`AI-Internal-Engine.txt`), + les 4 `Moto.Editor.*` d'architecture
    (architecture, module-dependencies, memory-model, threading-model).
  - `Docs/specs/` — les 11 specs `Moto.Editor.*` (code-style,
    dev-guidelines, naming-conventions, crypto-spec, security-model,
    logging-spec, error-codes, performance-guide, testing-strategy,
    update-manifest-spec, update-failure-handling).
  - `Docs/process/` — `CONTRIBUTING.md`, `RELEASE-CHECKLIST.md`,
    `Flux-bout-en-bout.md`, + les 4 `Moto.Editor.*` de process
    (directory-structure, build-pipeline, release-process,
    installation-flow).
  - `Docs/product/` — `FEATURES.md`, `Roadmap.md` (ex-`Roadmap.txt`),
    `Idées-à-implémenter.txt`, `AI-OPTIMIZATIONS.md`,
    `Résumé-compressé.md`.
  - Sous-dossiers de contenu inchangés : `Docs/Zen/` (ex-`Docs/Pour
    Claude/`), `Docs/Documents/` (présentation/vente), `Docs/inspirations/`
    (captures + maquette « Claude shell »), `Docs/probes/` (sondes).
  ⚠️ **Le dépôt public n'a toujours AUCUN fichier `LICENSE`** — un fichier
  nommé « Licence.md » existait mais ne contenait pas de licence. Question
  juridique ouverte, à trancher par Tom.
- **7 fichiers supprimés de `Docs/` le 22/09 (validé par Tom, récupérables
  dans l'historique git)** : `Moto.Editor.contribution-guide.md` (contenu
  **répété 3 fois**, bug de génération, et doublon de `CONTRIBUTING.md`),
  `Moto.Editor.slnf` (**cassé** : référençait `Moto.Editor.sln` alors que la
  solution s'appelle `MotoEditor.sln`), `Moto.Editor.workspace.json`,
  `Moto.Editor.projectmap.json`, `Moto.Editor.modules.json` (les 3 à **0
  référence** nulle part — artefacts de session orchestrateur),
  `RELEASE-CHECKLIST-AUTOMATED.md` (doublon partiel + documentait une
  release « v40 » autour du cluster ONNX **mort**), `Agent-Integrated.md`
  (décrivait une architecture **WinForms** — faux, l'app est MAUI — et 4 des
  8 fichiers qu'il citait avaient disparu).
- **5 notes techniques supprimées le 22/09** (Chaîne de confiance complète,
  Configuration des secrets, Créer une release corrective,
  Installateur-Structure, Tests) — suppression validée par Tom.
- État des lieux brut du 02/09 (4 agents, panel-architecture-audit /
  visual-debt-audit / dead-feature-inventory / zed-inspired-explorer-entry-
  point) : sortie complète encore disponible dans le dossier de tâches de la
  session Claude Code de ce jour-là si un détail précis manque ici — ce
  fichier en est le résumé digéré, pas la sortie brute.
