// Moto.Editor/MainPage.Panels.cs (v29 — extraction des panneaux IA)
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.ApplicationModel;
using Moto.Core.AI.Cortex;
using Moto.Core.AI.Neural;
using Moto.Core.AI.Workspace;
using Moto.Core.Doc;
using Moto.Core.Settings;
using Moto.Editor.Views;

namespace Moto.Editor
{
    /// <summary>
    /// Partial class : gestion des panneaux IA (Cortex, Neural, Workspace, Galerie, Analytics).
    /// </summary>
    public partial class MainPage
    {
        // ------------------------------------------------------------------
        // Panneaux IA v3
        // ------------------------------------------------------------------
        private void WireAiPanels()
        {
            _workspacePanel.ApplyRequested += OnWorkspaceApply;
            _neuralPanel.CodeGenerated += code =>
            {
                var doc = _viewModel.SelectedDocument;
                if (doc != null)
                {
                    doc.Text = doc.Text + "\n\n" + code;
                    EditorPane.EditorText = doc.Text;
                }
            };
            _cortexPanel.ModeChanged += mode =>
            {
                SettingsEngine.Shared.Set("cortex_mode", mode.ToString());
                StatusBar.SetStatus($"🧠 Mode Cortex : {mode}");
            };
        }

        private void OnCortexClicked(object sender, EventArgs e)
        {
            _cortexPanel.IsVisible = !_cortexPanel.IsVisible;
            _neuralPanel.IsVisible = false;
            _workspacePanel.IsVisible = false;
            _pluginGallery.IsVisible = false;
            _analyticsDashboard.IsVisible = false;
            if (_cortexPanel.IsVisible && _viewModel.SelectedDocument != null)
                _cortexPanel.LoadSuggestions(_viewModel.SelectedDocument.Path, _viewModel.SelectedDocument.Text);
            RefreshAiDockColumnWidth();
        }

        private void OnNeuralClicked(object sender, EventArgs e)
        {
            _neuralPanel.IsVisible = !_neuralPanel.IsVisible;
            _cortexPanel.IsVisible = false;
            _workspacePanel.IsVisible = false;
            _pluginGallery.IsVisible = false;
            _analyticsDashboard.IsVisible = false;
            RefreshAiDockColumnWidth();
        }

        private void OnWorkspaceClicked(object sender, EventArgs e)
        {
            _workspacePanel.IsVisible = !_workspacePanel.IsVisible;
            _cortexPanel.IsVisible = false;
            _neuralPanel.IsVisible = false;
            _pluginGallery.IsVisible = false;
            _analyticsDashboard.IsVisible = false;
            if (_workspacePanel.IsVisible) _workspacePanel.Analyze();
            RefreshAiDockColumnWidth();
        }

        private void OnGalleryClicked()
        {
            _pluginGallery.IsVisible = !_pluginGallery.IsVisible;
            _cortexPanel.IsVisible = false;
            _neuralPanel.IsVisible = false;
            _workspacePanel.IsVisible = false;
            _analyticsDashboard.IsVisible = false;
            if (_pluginGallery.IsVisible) _pluginGallery.LoadGallery();
            RefreshAiDockColumnWidth();
        }

        /// <summary>
        /// ★ AJOUT (30/08, refonte Zen) : AiDockPanel (colonne 0 — Cortex/Neural/
        /// Workspace/Gallery/Analytics/Debug/Platform + AiHost/ChatHost/ThreadHost)
        /// est masqué par défaut (voir MainPage.xaml) pour éviter la "zone noire"
        /// toujours visible même vide, repérée par Tom. Sa colonne ("Auto") se
        /// replie donc à 0 automatiquement tant qu'il est masqué. Ré-affiché ici dès
        /// qu'au moins un des panneaux qu'il héberge est visible.
        /// </summary>
        private void RefreshAiDockColumnWidth()
        {
            bool any = AiHost.IsVisible || ChatHost.IsVisible || ThreadHost.IsVisible
                || _platformPanel.IsVisible || _cortexPanel.IsVisible || _neuralPanel.IsVisible
                || _workspacePanel.IsVisible || _pluginGallery.IsVisible || _analyticsDashboard.IsVisible
                || _debugPanel.IsVisible;
            AiDockPanel.IsVisible = any;
        }

        private void OnWorkspaceApply(Moto.Core.AI.Workspace.WorkspaceSuggestion suggestion)
        {
            if (!string.IsNullOrWhiteSpace(suggestion.FilePath) && File.Exists(suggestion.FilePath))
            {
                _viewModel.OpenFilePath(suggestion.FilePath);
                if (suggestion.Line > 0)
                    EditorPane.GoToLine(suggestion.Line);
                StatusBar.SetStatus($"🏗 {suggestion.Title}");
            }
            else
            {
                StatusBar.SetStatus($"💡 {suggestion.Title}");
            }
        }

        /// <summary>Titre affiché dans l'en-tête de chaque panneau ancré (PanelHost).</summary>
        private static string TitleFor(ContentView panel) => panel switch
        {
            PlatformView => "🖥️ Plateforme",
            CortexView => "🧠 Cortex",
            NeuralView => "🤖 Neural",
            AIWorkspaceView => "🧩 Workspace",
            PluginGalleryView => "🧱 Plugins",
            AnalyticsDashboardView => "📊 Analytics",
            DebugPanelView => "🐞 Debug",
            _ => panel.GetType().Name
        };

        /// <summary>
        /// ★ CORRECTION (30/08) : les panneaux flottaient tous au même endroit
        /// (par-dessus le contenu, même marge) et se superposaient entre eux — repéré
        /// par Tom. Ancrés maintenant dans PanelHost (colonne 3, déjà existante), un
        /// par un, avec un en-tête titré + bouton ✕ pour fermer (aucun panneau flottant
        /// de ce projet n'avait de bouton fermer jusqu'ici). La visibilité de l'en-tête
        /// suit automatiquement celle du panneau (liaison IsVisible) : tout le code
        /// existant qui fait `_xPanel.IsVisible = ...` continue de fonctionner tel quel.
        /// </summary>
        private void AddFloatingPanel(ContentView panel)
        {
            var close = new Button
            {
                Text = "✕", WidthRequest = 28, HeightRequest = 24, FontSize = 12,
                Padding = 0, BackgroundColor = Colors.Transparent,
                TextColor = (Color)Application.Current!.Resources["Txt2"]
            };
            close.Clicked += (s, e) => panel.IsVisible = false;

            var header = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            header.Add(new Label
            {
                Text = TitleFor(panel), FontSize = 13, FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center,
                TextColor = (Color)Application.Current!.Resources["Txt1"]
            });
            header.Add(close, 1);

            var wrapper = new Border
            {
                Stroke = (Color)Application.Current!.Resources["BorderCol"],
                BackgroundColor = (Color)Application.Current!.Resources["BgPanel"],
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = 8,
                Content = new VerticalStackLayout { Spacing = 6, Children = { header, panel } }
            };
            wrapper.SetBinding(IsVisibleProperty, new Binding(nameof(IsVisible), source: panel));

            panel.IsVisible = false;
            PanelHost.Children.Add(wrapper);
        }

        // ------------------------------------------------------------------
        // LoadWorkspace
        // ------------------------------------------------------------------
        private void LoadWorkspace(string path)
        {
            _currentRoot = path;
            _chatService.WorkspaceRoot = path;
            _aiService.SetWorkspace(path);
            ExplorerPanel.LoadFolder(path);

            // ★ AJOUT (30/08, refonte Zen) : l'arborescence (colonne 2, à droite) est
            // masquée par défaut — demandé par Tom : "non ouvert par défaut, jusqu'à
            // ce qu'on importe une location". Un dossier vient d'être importé avec
            // succès (ce point est atteint par TOUS les chemins d'import : chip
            // "Projet logiciel", bouton Importer, AutoProjectBuilder) → on l'affiche.
            ExplorerPanel.IsVisible = true;
            Sidebar.IsVisible = false;

            StatusBar.SetLocked(_lock.IsLocked(path));

            _aiSettings = new Moto.Core.Settings.AiSettingsService(SettingsEngine.Shared, path);

            _platformPanel.SetWorkspace(path);
            if (SettingsEngine.Shared.GetBool("platform_auto_detect"))
                _platformPanel.Analyze();

            _cortex?.Dispose();
            _workspace?.Dispose();
            _docEngine?.Dispose();

            _cortex = new CortexEngine(path);
            _neural = new NeuralMode(path, new CortexMemory(path));
            _workspace = new AIWorkspace(path);
            _docEngine = new DocEngine(path);

            Home.SetCoreServices(_cortex, _workspaceState);

            RebindPanels();

            _ = Task.Run(() => _neural.Train());
            _ = _workspace.InitializeAsync().ContinueWith(_ =>
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (_workspacePanel.IsVisible) _workspacePanel.Analyze();
                }));

            if (SettingsEngine.Shared.GetBool("doc_on_project_open"))
                _ = _docEngine.GenerateAsync();

            StatusBar.SetStatus($"🧠 Cortex + 🧬 Neural + 🏗 Workspace initialisés.");
        }

        /// <summary>Retire de PanelHost l'en-tête (Border) qui enveloppe ce panneau.</summary>
        private void RemoveFloatingPanel(ContentView panel)
        {
            // panel.Parent = VerticalStackLayout (Content du Border) ; son propre
            // Parent = le Border ajouté à PanelHost.Children (voir AddFloatingPanel).
            if (panel.Parent?.Parent is Border wrapper)
                PanelHost.Children.Remove(wrapper);
        }

        private void RebindPanels()
        {
            // ★ CORRECTION (30/08) : la détection "déjà ajouté" testait
            // `panel.Parent is Grid` (vrai avant, quand les panneaux flottaient
            // directement dans RootGrid). Ils sont maintenant enveloppés dans un
            // Border ajouté à PanelHost (VerticalStackLayout) — voir AddFloatingPanel.
            if (_cortexPanel.Parent != null)
            {
                RemoveFloatingPanel(_cortexPanel);
                _cortexPanel = new CortexView(_cortex);
                AddFloatingPanel(_cortexPanel);
            }
            if (_neuralPanel.Parent != null)
            {
                RemoveFloatingPanel(_neuralPanel);
                _neuralPanel = new NeuralView(_neural);
                AddFloatingPanel(_neuralPanel);
            }
            if (_workspacePanel.Parent != null)
            {
                RemoveFloatingPanel(_workspacePanel);
                _workspacePanel = new AIWorkspaceView(_workspace);
                AddFloatingPanel(_workspacePanel);
            }
            WireAiPanels();
        }

        // ★ LSP Roslyn (OmniSharp) : mis de côté pour cette passe (voir Moto.Core.csproj),
        // ce bloc de câblage se raccrochait à LanguageServerManager, exclu de la build.

        // ------------------------------------------------------------------
        // Stats réelles
        // ------------------------------------------------------------------
        private void RefreshHomeStats()
        {
            try
            {
                int threads = 0, messages = 0, chars = 0;
                if (_chatService.Threads != null)
                {
                    threads = _chatService.Threads.Count;
                    foreach (var t in _chatService.Threads)
                    {
                        messages += t.Messages?.Count ?? 0;
                        foreach (var m in t.Messages)
                            chars += m.Content?.Length ?? 0;
                    }
                }
                var tokens = chars / 4;
                var cortex = _cortex?.GetStats();
                Home.SetStats(
                    values: new[]
                    {
                        threads.ToString(),
                        messages.ToString(),
                        FormatCompact(tokens),
                        (cortex?.TotalPatterns ?? 0).ToString()
                    },
                    titles: new[]
                    {
                        "Sessions",
                        "Messages",
                        "Tokens",
                        "Patterns appris"
                    });
            }
            catch { }
        }

        private static string FormatCompact(int n) =>
            n >= 1_000_000 ? (n / 1_000_000.0).ToString("0.0M") :
            n >= 1_000 ? (n / 1_000.0).ToString("0.0K") :
            n.ToString();
    }
}
