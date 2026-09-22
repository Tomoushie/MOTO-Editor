# Rapport de conversion vers les jetons visuels

> **Généré automatiquement — analyse seule, aucun fichier modifié.**
> Source : `scripts/visual-tokens-dryrun.ps1` · Périmètre : 71 fichiers XAML réellement compilés
> Échelle cible : `Docs/design/Langage-visuel-spec.md` (option B, corps 13 px)

Ce rapport existe pour rendre la conversion **relisible**. Les cas marqués
« à trancher » ne se décident pas par table : ils dépendent de l'élément
(un 11 px de badge et un 11 px de texte courant ne deviennent pas le même rôle).

## 1. Tailles de police

**331 occurrences utilisent déjà un jeton** (DynamicResource/StaticResource) — elles ne doivent PAS être touchées.

| Valeur trouvée | Occurrences | Rôle proposé | Taille cible |
|---|---|---|---|
| 7 px | 1 | Small | 12 px |
| 11 px | 94 | Micro|Small ⚠ | à trancher |
| 11.5 px | 8 | Micro|Small ⚠ | à trancher |
| 18 px | 13 | Heading|Title ⚠ | à trancher |
| 24 px | 1 | Title|Display ⚠ | à trancher |

## 2. Rayons d'arrondi

| Valeur trouvée | Occurrences | Jeton proposé |
|---|---|---|
| 0 | 2 | 0 (inchangé) |
| 6 | 4 | RadiusSm (6) |
| 8 | 39 | RadiusMd (8) |
| 10 | 1 | RadiusMd (8) |
| 12 | 44 | RadiusLg (12) |
| 999 | 1 | ⚠ NON MAPPÉ |

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
| 10 | 27 | SpaceSm (8) | 2 |
| 12 | 35 | SpaceSm (8) | 4 |
| 14 | 30 | SpaceMd (16) | -2 |
| 16 | 19 | SpaceMd (16) | 0 |
| 18 | 1 | SpaceMd (16) | 2 |
| 20 | 10 | SpaceMd (16) | 4 |
| 22 | 2 | SpaceLg (24) | -2 |
| 24 | 3 | SpaceLg (24) | 0 |

## 4. Fichiers les plus touchés (ordre de traitement suggéré)

| Fichier | Valeurs à convertir |
|---|---|
| `Moto.Editor\Views\Claude\ClaudeShellView.xaml` | 60 |
| `Moto.Editor\Controls\AiComposerBarView.xaml` | 26 |
| `Moto.Editor\Pages\AiSettingsPage.xaml` | 24 |
| `Moto.Editor\Views\AiChatView.xaml` | 15 |
| `Moto.Editor\Views\BeginnerModesView.xaml` | 15 |
| `Moto.Editor\Views\DebugPanelProView.xaml` | 14 |
| `Moto.Editor\Views\PerformanceView.xaml` | 13 |
| `Moto.Editor\Views\GitPanelView.xaml` | 13 |
| `Moto.Editor\Views\ThemePreviewView.xaml` | 12 |
| `Moto.Editor\Views\DevOpsDashboardView.xaml` | 12 |
| `Moto.Editor\Controls\InfoOverlay.xaml` | 12 |
| `Moto.Editor\Views\DocPanelView.xaml` | 12 |
| `Moto.Editor\Views\HomeView.xaml` | 10 |
| `Moto.Editor\Views\DebugPanelView.xaml` | 10 |
| `Moto.Editor\Controls\EditorPaneView.xaml` | 10 |
| `Moto.Editor\Views\CortexView.xaml` | 9 |
| `Moto.Editor\Views\NeuralView.xaml` | 9 |
| `Moto.Editor\Views\AboutProView.xaml` | 9 |
| `Moto.Editor\Views\AgentRunsView.xaml` | 9 |
| `Moto.Editor\Views\AboutXenoView.xaml` | 9 |
| `Moto.Editor\Views\ReviewLaneView.xaml` | 8 |
| `Moto.Editor\Views\CommandPaletteView.xaml` | 8 |
| `Moto.Editor\Views\AboutView.xaml` | 8 |
| `Moto.Editor\Views\AIWorkspaceView.xaml` | 8 |
| `Moto.Editor\Views\FileExplorerView.xaml` | 7 |
| `Moto.Editor\Views\PlatformView.xaml` | 7 |
| `Moto.Editor\Views\CollabPanelView.xaml` | 7 |
| `Moto.Editor\Views\ContextSuggestionsView.xaml` | 7 |
| `Moto.Editor\Views\SidebarView.xaml` | 7 |
| `Moto.Editor\Views\ExportMenuView.xaml` | 6 |
| `Moto.Editor\Views\ProactiveActionsView.xaml` | 6 |
| `Moto.Editor\Views\ConfirmationOverlay.xaml` | 6 |
| `Moto.Editor\Views\LanguageSelectorView.xaml` | 6 |
| `Moto.Editor\Views\PresentationView.xaml` | 5 |
| `Moto.Editor\Views\EvolutionPanelView.xaml` | 5 |
| `Moto.Editor\Pages\MotoAiPage.xaml` | 5 |
| `Moto.Editor\Views\PasswordGateView.xaml` | 5 |
| `Moto.Editor\Views\CustomMenuBarView.xaml` | 5 |
| `Moto.Editor\Views\AutoLinkView.xaml` | 5 |
| `Moto.Editor\Views\AgentMarketplaceView.xaml` | 5 |
| `Moto.Editor\Views\AnalyticsDashboardView.xaml` | 5 |
| `Moto.Editor\Views\PerformanceStatusBarView.xaml` | 5 |
| `Moto.Editor\Views\ProactivePanel.xaml` | 5 |
| `Moto.Editor\Controls\ExecutionLocationMenu.xaml` | 5 |
| `Moto.Editor\Views\ImageViewerView.xaml` | 5 |
| `Moto.Editor\Views\LivePreviewView.xaml` | 5 |
| `Moto.Editor\Views\GlobalDashboardView.xaml` | 4 |
| `Moto.Editor\Views\RemoteConnectView.xaml` | 4 |
| `Moto.Editor\Views\XenoFeedbackOverlay.xaml` | 4 |
| `Moto.Editor\Views\MarketplaceView.xaml` | 4 |
| `Moto.Editor\Views\MigrationOverlay.xaml` | 4 |
| `Moto.Editor\Views\PluginGalleryView.xaml` | 4 |
| `Moto.Editor\Views\TerminalPanelView.xaml` | 4 |
| `Moto.Editor\Views\SearchView.xaml` | 4 |
| `Moto.Editor\MainPage.xaml` | 4 |
| `Moto.Editor\Views\SettingsWindowView.xaml` | 4 |
| `Moto.Editor\Pages\BeginnerAssistantPage.xaml` | 3 |
| `Moto.Editor\Views\GearMenuView.xaml` | 3 |
| `Moto.Editor\Views\StoryModeView.xaml` | 3 |
| `Moto.Editor\Views\BackgroundTasksView.xaml` | 3 |
| `Moto.Editor\Views\AiMonitoringView.xaml` | 3 |
| `Moto.Editor\Views\ThreadListView.xaml` | 2 |
| `Moto.Editor\Views\StatusBarPanelView.xaml` | 2 |
| `Moto.Editor\Controls\MotoLogoView.xaml` | 2 |
| `Moto.Editor\Views\AiCommandBarView.xaml` | 2 |
| `Moto.Editor\Controls\SkeletonLoader.xaml` | 2 |
| `Moto.Editor\Controls\ActivityBarView.xaml` | 1 |

## 5. Totaux

| Catégorie | Convertibles par table | Valeurs distinctes | Hors table (à la main) |
|---|---|---|---|
| FontSize | 117 | 5 | — |
| Rayons | 91 | 6 | 0 rayons composés (4,4,0,0) |
| Espacements | 317 (valeurs uniques) | 16 | 257 valeurs composées (14,10) |
| **Total** | **525** | | **257** |

Les **couleurs hexadécimales** (138 occurrences) ne sont pas traitées par ce
rapport : leur conversion dépend de la décision D2 (accent orange ou bleu)
et demande un jugement au cas par cas, pas une table.

