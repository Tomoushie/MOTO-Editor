// Moto.Editor/MainPage.Panels.cs (v29 — extraction des panneaux IA)
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
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
        }

        private void OnNeuralClicked(object sender, EventArgs e)
        {
            _neuralPanel.IsVisible = !_neuralPanel.IsVisible;
            _cortexPanel.IsVisible = false;
            _workspacePanel.IsVisible = false;
            _pluginGallery.IsVisible = false;
            _analyticsDashboard.IsVisible = false;
        }

        private void OnWorkspaceClicked(object sender, EventArgs e)
        {
            _workspacePanel.IsVisible = !_workspacePanel.IsVisible;
            _cortexPanel.IsVisible = false;
            _neuralPanel.IsVisible = false;
            _pluginGallery.IsVisible = false;
            _analyticsDashboard.IsVisible = false;
            if (_workspacePanel.IsVisible) _workspacePanel.Analyze();
        }

        private void OnGalleryClicked()
        {
            _pluginGallery.IsVisible = !_pluginGallery.IsVisible;
            _cortexPanel.IsVisible = false;
            _neuralPanel.IsVisible = false;
            _workspacePanel.IsVisible = false;
            _analyticsDashboard.IsVisible = false;
            if (_pluginGallery.IsVisible) _pluginGallery.LoadGallery();
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

        private void AddFloatingPanel(ContentView panel)
        {
            RootGrid.Children.Add(panel);
            Grid.SetRow(panel, 1);
            Grid.SetColumnSpan(panel, 4);
            panel.HorizontalOptions = LayoutOptions.End;
            panel.VerticalOptions = LayoutOptions.Start;
            panel.Margin = new Thickness(0, 40, 60, 0);
            panel.IsVisible = false;
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

        private void RebindPanels()
        {
            if (_cortexPanel.Parent is Grid root1)
            {
                root1.Children.Remove(_cortexPanel);
                _cortexPanel = new CortexView(_cortex);
                AddFloatingPanel(_cortexPanel);
            }
            if (_neuralPanel.Parent is Grid root2)
            {
                root2.Children.Remove(_neuralPanel);
                _neuralPanel = new NeuralView(_neural);
                AddFloatingPanel(_neuralPanel);
            }
            if (_workspacePanel.Parent is Grid root3)
            {
                root3.Children.Remove(_workspacePanel);
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
