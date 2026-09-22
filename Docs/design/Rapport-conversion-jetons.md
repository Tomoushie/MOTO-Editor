# Rapport de conversion vers les jetons visuels

> **Généré automatiquement — analyse seule, aucun fichier modifié.**
> Source : `scripts/visual-tokens-dryrun.ps1` · Périmètre : 71 fichiers XAML réellement compilés
> Échelle cible : `Docs/design/Langage-visuel-spec.md` (option B, corps 13 px)

Ce rapport existe pour rendre la conversion **relisible**. Les cas marqués
« à trancher » ne se décident pas par table : ils dépendent de l'élément
(un 11 px de badge et un 11 px de texte courant ne deviennent pas le même rôle).

## 1. Tailles de police

**33 occurrences utilisent déjà un jeton** (DynamicResource/StaticResource) — elles ne doivent PAS être touchées.

| Valeur trouvée | Occurrences | Rôle proposé | Taille cible |
|---|---|---|---|
| 7 px | 1 | Small | 12 px |
| 9 px | 4 | Small | 12 px |
| 10 px | 39 | Small | 12 px |
| 10.5 px | 1 | Small | 12 px |
| 11 px | 94 | Micro|Small ⚠ | à trancher |
| 11.5 px | 8 | Micro|Small ⚠ | à trancher |
| 12 px | 122 | Body | 13 px |
| 12.5 px | 11 | Body | 13 px |
| 13 px | 43 | Body | 13 px |
| 14 px | 48 | Subheading | 14 px |
| 15 px | 10 | Subheading | 14 px |
| 16 px | 12 | Heading | 16 px |
| 17 px | 1 | Heading | 16 px |
| 18 px | 13 | Heading|Title ⚠ | à trancher |
| 20 px | 2 | Title | 20 px |
| 21 px | 1 | Title | 20 px |
| 22 px | 1 | Title | 20 px |
| 24 px | 1 | Title|Display ⚠ | à trancher |
| 26 px | 3 | Display | 28 px |
| 28 px | 1 | Display | 28 px |
| 30 px | 2 | Display | 28 px |

## 2. Rayons d'arrondi

| Valeur trouvée | Occurrences | Jeton proposé |
|---|---|---|
| 0 | 2 | 0 (inchangé) |
| 6 | 2 | RadiusSm (6) |
| 7 | 2 | RadiusSm (6) |
| 8 | 36 | RadiusMd (8) |
| 9 | 5 | RadiusMd (8) |
| 10 | 12 | RadiusMd (8) |
| 12 | 49 | RadiusLg (12) |
| 14 | 4 | RadiusLg (12) |
| 15 | 1 | RadiusLg (12) |
| 16 | 4 | RadiusLg (12) |
| 17 | 1 | RadiusLg (12) |
| 24 | 2 | RadiusLg (12) |
| 75 | 1 | RadiusPill |

## 3. Espacements (valeurs uniques seulement)

Les valeurs composées (`Padding="14,10"`) ne se convertissent pas
mécaniquement : elles demandent un choix. Seules les valeurs uniques sont
listées ici. Un écart affiché de 0 signifie que la valeur EST déjà sur
l'échelle ; toute autre valeur sera **corrigée** vers le jeton le plus
proche — c'est précisément l'effet recherché.

| Valeur trouvée | Occurrences | Jeton le plus proche | Écart |
|---|---|---|---|
| 0 | 20 | **inchangé** (0 est volontaire) | — |
| 1 | 5 | SpaceXs (4) | -3 |
| 2 | 12 | SpaceXs (4) | -2 |
| 3 | 2 | SpaceXs (4) | -1 |
| 4 | 40 | SpaceXs (4) | 0 |
| 6 | 44 | SpaceXs (4) | 2 |
| 8 | 65 | SpaceSm (8) | 0 |
| 9 | 2 | SpaceSm (8) | 1 |
| 10 | 29 | SpaceSm (8) | 2 |
| 12 | 35 | SpaceSm (8) | 4 |
| 14 | 30 | SpaceMd (16) | -2 |
| 16 | 19 | SpaceMd (16) | 0 |
| 18 | 1 | SpaceMd (16) | 2 |
| 20 | 10 | SpaceMd (16) | 4 |
| 22 | 3 | SpaceLg (24) | -2 |
| 24 | 3 | SpaceLg (24) | 0 |

## 4. Fichiers les plus touchés (ordre de traitement suggéré)

| Fichier | Valeurs à convertir |
|---|---|
| `Moto.Editor\Views\Claude\ClaudeShellView.xaml` | 80 |
| `Moto.Editor\Pages\AiSettingsPage.xaml` | 33 |
| `Moto.Editor\Controls\AiComposerBarView.xaml` | 30 |
| `Moto.Editor\Views\BeginnerModesView.xaml` | 29 |
| `Moto.Editor\Views\GitPanelView.xaml` | 26 |
| `Moto.Editor\Views\AboutProView.xaml` | 24 |
| `Moto.Editor\Views\DevOpsDashboardView.xaml` | 24 |
| `Moto.Editor\Views\AiChatView.xaml` | 22 |
| `Moto.Editor\Views\AboutView.xaml` | 21 |
| `Moto.Editor\Views\HomeView.xaml` | 21 |
| `Moto.Editor\Views\AboutXenoView.xaml` | 21 |
| `Moto.Editor\Views\GearMenuView.xaml` | 19 |
| `Moto.Editor\Controls\EditorPaneView.xaml` | 19 |
| `Moto.Editor\Views\AgentRunsView.xaml` | 19 |
| `Moto.Editor\Views\DocPanelView.xaml` | 19 |
| `Moto.Editor\Views\CortexView.xaml` | 18 |
| `Moto.Editor\Controls\InfoOverlay.xaml` | 18 |
| `Moto.Editor\Views\AIWorkspaceView.xaml` | 16 |
| `Moto.Editor\Views\DebugPanelProView.xaml` | 16 |
| `Moto.Editor\Controls\ExecutionLocationMenu.xaml` | 16 |
| `Moto.Editor\Views\DebugPanelView.xaml` | 15 |
| `Moto.Editor\Views\ThemePreviewView.xaml` | 14 |
| `Moto.Editor\Views\FileExplorerView.xaml` | 14 |
| `Moto.Editor\Views\PerformanceView.xaml` | 14 |
| `Moto.Editor\Views\ReviewLaneView.xaml` | 13 |
| `Moto.Editor\Views\ContextSuggestionsView.xaml` | 13 |
| `Moto.Editor\Views\NeuralView.xaml` | 12 |
| `Moto.Editor\Views\PlatformView.xaml` | 12 |
| `Moto.Editor\Views\SidebarView.xaml` | 10 |
| `Moto.Editor\Views\BackgroundTasksView.xaml` | 10 |
| `Moto.Editor\Views\AutoLinkView.xaml` | 10 |
| `Moto.Editor\Views\AgentMarketplaceView.xaml` | 10 |
| `Moto.Editor\Views\CustomMenuBarView.xaml` | 9 |
| `Moto.Editor\Views\CollabPanelView.xaml` | 9 |
| `Moto.Editor\Views\ProactiveActionsView.xaml` | 9 |
| `Moto.Editor\Views\CommandPaletteView.xaml` | 9 |
| `Moto.Editor\Views\RemoteConnectView.xaml` | 9 |
| `Moto.Editor\Views\EvolutionPanelView.xaml` | 9 |
| `Moto.Editor\Views\AiMonitoringView.xaml` | 9 |
| `Moto.Editor\Views\ConfirmationOverlay.xaml` | 9 |
| `Moto.Editor\Views\ExportMenuView.xaml` | 8 |
| `Moto.Editor\Views\ProactivePanel.xaml` | 8 |
| `Moto.Editor\Pages\BeginnerAssistantPage.xaml` | 8 |
| `Moto.Editor\Views\PresentationView.xaml` | 8 |
| `Moto.Editor\Views\SearchView.xaml` | 7 |
| `Moto.Editor\Views\ImageViewerView.xaml` | 7 |
| `Moto.Editor\Views\XenoFeedbackOverlay.xaml` | 7 |
| `Moto.Editor\Views\TerminalPanelView.xaml` | 7 |
| `Moto.Editor\Views\MigrationOverlay.xaml` | 7 |
| `Moto.Editor\Pages\MotoAiPage.xaml` | 7 |
| `Moto.Editor\Views\SettingsWindowView.xaml` | 6 |
| `Moto.Editor\Views\LanguageSelectorView.xaml` | 6 |
| `Moto.Editor\Views\PasswordGateView.xaml` | 6 |
| `Moto.Editor\Views\ThreadListView.xaml` | 6 |
| `Moto.Editor\Views\LivePreviewView.xaml` | 6 |
| `Moto.Editor\Views\AnalyticsDashboardView.xaml` | 5 |
| `Moto.Editor\Views\MarketplaceView.xaml` | 5 |
| `Moto.Editor\Views\PerformanceStatusBarView.xaml` | 5 |
| `Moto.Editor\Views\StoryModeView.xaml` | 5 |
| `Moto.Editor\Views\PluginGalleryView.xaml` | 4 |
| `Moto.Editor\Views\GlobalDashboardView.xaml` | 4 |
| `Moto.Editor\MainPage.xaml` | 4 |
| `Moto.Editor\Views\AiCommandBarView.xaml` | 3 |
| `Moto.Editor\Controls\CustomMenuBarView.xaml` | 3 |
| `Moto.Editor\Controls\MotoLogoView.xaml` | 2 |
| `Moto.Editor\Views\StatusBarPanelView.xaml` | 2 |
| `Moto.Editor\Controls\SkeletonLoader.xaml` | 2 |
| `Moto.Editor\Controls\ActivityBarView.xaml` | 1 |

## 5. Totaux

| Catégorie | Convertibles par table | Valeurs distinctes | Hors table (à la main) |
|---|---|---|---|
| FontSize | 418 | 21 | — |
| Rayons | 121 | 13 | 0 rayons composés (4,4,0,0) |
| Espacements | 320 (valeurs uniques) | 16 | 259 valeurs composées (14,10) |
| **Total** | **859** | | **259** |

Les **couleurs hexadécimales** (138 occurrences) ne sont pas traitées par ce
rapport : leur conversion dépend de la décision D2 (accent orange ou bleu)
et demande un jugement au cas par cas, pas une table.

