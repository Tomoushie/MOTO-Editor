// Moto.Editor/MainPage.Routing.cs (v29 corrigé — espaces supprimés + /window déplacé)
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Analytics;
using Moto.Core.AI.Builders;
using Moto.Core.Export;
using Moto.Core.Settings;
using Moto.Editor.Services;

namespace Moto.Editor
{
    /// <summary>
    /// Partial class : routeurs (menus custom, activity bar, commandes slash).
    /// </summary>
    public partial class MainPage
    {
        // ------------------------------------------------------------------
        // Routeur des menus custom
        // ------------------------------------------------------------------
        private void OnMenuCommanded(string id)
        {
            switch (id)
            {
                case "file.opendir": _viewModel.OpenFileCommand.Execute(null); break;
                case "file.openfile": _viewModel.OpenFileCommand.Execute(null); break;
                case "file.save": _viewModel.SaveCommand.Execute(null); break;
                case "file.import": OnImportClicked(null, null); break;
                case "file.export": ExportMenu.IsVisible = !ExportMenu.IsVisible; break;
                case "file.lock": OnLockClicked(null, null); break;

                case "edit.search": AiBar.Toggle(); break;
                case "edit.commands": AiBar.Toggle(); break;

                case "view.explorer": ToggleSide(isExplorer: true); break;
                case "view.sidebar": ToggleSide(isExplorer: false); break;
                case "view.aipanel": AiHost.IsVisible = !AiHost.IsVisible; RefreshAiDockColumnWidth(); break;
                case "view.terminal": _viewModel.IsTerminalVisible = !_viewModel.IsTerminalVisible; break;
                case "view.diagnostics": _viewModel.IsDiagnosticsVisible = !_viewModel.IsDiagnosticsVisible; break;
                case "view.maximize": OnMaximizeToggled(); break;
                case "view.theme": ThemeService.SetDark(); break;

                case "nav.back": OnNavBack(); break;
                case "nav.forward": OnNavForward(); break;

                case "run.build": OnBuildClicked(null, null); break;
                case "run.play": OnPlayClicked(null, null); break;
                case "run.stop": OnStopClicked(null, null); break;
                case "run.sandbox": OnSandboxClicked(null, null); break;

                case "ai.cortex": OnCortexClicked(null, null); break;
                case "ai.neural": OnNeuralClicked(null, null); break;
                case "ai.workspace": OnWorkspaceClicked(null, null); break;
                case "ai.autolink": AutoLinkPanel.IsVisible = !AutoLinkPanel.IsVisible; break;
                case "ai.context": ContextPanel.IsVisible = !ContextPanel.IsVisible; break;
                case "ai.evolution": StatusBar.SetStatus("🧬 Evolution…"); break;
                case "ai.story": StatusBar.SetStatus("📚 Story Mode…"); break;
                case "ai.health": StatusBar.SetStatus("🩺 Health…"); break;
                case "ai.timemachine": StatusBar.SetStatus("🕘 Time Machine…"); break;
                case "ai.doc": DocPanel.IsVisible = !DocPanel.IsVisible; break;
                case "ai.platform": OnPlatformClicked(null, null); break;
                case "ai.presentation": OnPresentationClicked(null, null); break;
                case "ai.remote": OnRemoteClicked(null, null); break;
                case "ai.collab": OnCollabClicked(null, null); break;
                case "ai.gallery": OnGalleryClicked(); break;

                case "term.open": _viewModel.IsTerminalVisible = true; break;
                case "help.doc": DocPanel.IsVisible = true; break;
                case "help.about": StatusBar.SetStatus("MOTO Editor v0.5 — AI Workspace"); break;
            }
        }

        // ------------------------------------------------------------------
        // Activity bar
        // ------------------------------------------------------------------
        /// <summary>
        /// ★ CORRECTION (30/08) : chaque panneau ne fermait que QUELQUES autres
        /// panneaux (listes codées en dur dans OnCortexClicked/OnGalleryClicked...),
        /// et "ai" (AiHost) / "collab" (CollabPanel) / "settings" (SettingsMenu)
        /// n'en fermaient AUCUN — repéré par Tom : ouvrir Cortex après IA laissait
        /// les deux superposés. Un seul point centralisé ferme maintenant TOUJOURS
        /// tout le reste avant d'afficher le panneau demandé.
        /// </summary>
        private void OnActivitySelected(string id)
        {
            if (id == "explorer") { ToggleSide(isExplorer: true); return; }

            bool showAi = id == "ai" && !AiHost.IsVisible;
            bool showCortex = id == "cortex" && !_cortexPanel.IsVisible;
            bool showCollab = id == "collab" && !CollabPanel.IsVisible;
            bool showSettings = id == "settings" && !SettingsMenu.IsVisible;
            // ★ AJOUT (30/08, 2e passe) : "Recherche" ouvrait le bandeau IA sans
            // rapport — ouvre maintenant une vraie recherche de fichiers par nom
            // (voir SearchView.xaml.cs), sur le même patron que les autres panneaux.
            bool showSearch = id == "search" && !_searchPanel.IsVisible;

            AiHost.IsVisible = false;
            _cortexPanel.IsVisible = false;
            _neuralPanel.IsVisible = false;
            _workspacePanel.IsVisible = false;
            _pluginGallery.IsVisible = false;
            _analyticsDashboard.IsVisible = false;
            _searchPanel.IsVisible = false;
            CollabPanel.IsVisible = false;
            SettingsMenu.IsVisible = false;

            switch (id)
            {
                case "ai": AiHost.IsVisible = showAi; break;
                case "cortex":
                    _cortexPanel.IsVisible = showCortex;
                    if (showCortex && _viewModel.SelectedDocument != null)
                        _cortexPanel.LoadSuggestions(_viewModel.SelectedDocument.Path, _viewModel.SelectedDocument.Text);
                    break;
                case "collab": CollabPanel.IsVisible = showCollab; break;
                case "search": _searchPanel.IsVisible = showSearch; break;
                case "gallery":
                    _pluginGallery.IsVisible = !_pluginGallery.IsVisible;
                    if (_pluginGallery.IsVisible) _pluginGallery.LoadGallery();
                    break;
                case "settings": SettingsMenu.IsVisible = showSettings; break;
            }

            // ★ AJOUT (30/08, refonte Zen) : la colonne 0 (dock IA) est repliée à 0
            // par défaut (l'ancienne "zone noire" toujours visible même vide, repérée
            // par Tom) — on la rouvre/referme selon qu'un panneau y est visible.
            RefreshAiDockColumnWidth();
        }

        /// <summary>
        /// ★ CORRECTION (30/08, refonte Zen) : l'arborescence (colonne 2, à droite
        /// désormais) est masquée par défaut (colonne "Auto" → 0px tant que son
        /// contenu est invisible). "Fichiers" bascule maintenant ouvert/fermé au lieu
        /// de simplement échanger Explorer/Sidebar (utile puisqu'il n'y a plus
        /// d'icône dédiée toujours visible pour refermer le volet).
        /// </summary>
        private void ToggleSide(bool isExplorer)
        {
            if (isExplorer)
            {
                bool willOpen = !ExplorerPanel.IsVisible;
                ExplorerPanel.IsVisible = willOpen;
                Sidebar.IsVisible = false;
            }
            else
            {
                ExplorerPanel.IsVisible = false;
                Sidebar.IsVisible = true;
            }
        }

        // ------------------------------------------------------------------
        // Routeur des commandes IA (slash commands)
        // ------------------------------------------------------------------
        private async void OnAiCommandSubmitted(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // ★ /analytics : rapport + export + dashboard
            if (text.StartsWith("/analytics", StringComparison.OrdinalIgnoreCase))
            {
                await HandleAnalyticsCommandAsync(text.Substring("/analytics".Length).Trim());
                return;
            }

            // ★ /window <kind> : ouvre une fenêtre spécialisée (v29)
            if (text.StartsWith("/window ", StringComparison.OrdinalIgnoreCase))
            {
                var kind = text.Substring("/window ".Length).Trim().ToLowerInvariant();
                OpenSpecializedWindow(kind);
                return;
            }

            // ★ /rollback-settings
            if (text.StartsWith("/rollback-settings", StringComparison.OrdinalIgnoreCase))
            {
                var settingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MotoEditor", "settings.json");
                var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsRollbackEngine>.Instance;
                var rollbackEngine = new SettingsRollbackEngine(logger);
                var rollbackResult = rollbackEngine.RollbackToLastBackup(settingsPath);
                StatusBar.SetStatus(rollbackResult.Success
                    ? $"✅ {rollbackResult.Message}"
                    : $"❌ {rollbackResult.Message}");
                RefreshHomeStats();
                return;
            }

            // ★ /action <id>
            if (text.StartsWith("/action ", StringComparison.OrdinalIgnoreCase))
            {
                HandleContextualAction(text.Substring("/action ".Length).Trim());
                return;
            }

            // ★ /actions
            if (text.StartsWith("/actions", StringComparison.OrdinalIgnoreCase))
            {
                ShowContextualActions();
                return;
            }

            // ★ /ai-settings
            if (text.StartsWith("/ai-settings", StringComparison.OrdinalIgnoreCase))
            {
                await HandleAiSettingsCommandAsync(text.Substring("/ai-settings".Length).Trim());
                return;
            }

            // ★ /export <format>
            if (text.StartsWith("/export", StringComparison.OrdinalIgnoreCase))
            {
                var format = ExportEngine.ParseFormat(text);
                if (format.HasValue && _viewModel.SelectedDocument != null)
                {
                    var doc = _viewModel.SelectedDocument;
                    var result = _exportEngine.Export(new ExportRequest
                    {
                        SourcePath = doc.Path,
                        Content = doc.Text,
                        Title = doc.Title,
                        Author = "MOTO Editor",
                        Format = format.Value
                    });
                    StatusBar.SetStatus(result.Message);
                }
                else
                {
                    StatusBar.SetStatus("Usage : /export md|docx|pdf|html|odt|rtf|json|csv|txt");
                }
                RefreshHomeStats();
                return;
            }

            // ★ /neural <intent>
            if (text.StartsWith("/neural", StringComparison.OrdinalIgnoreCase))
            {
                var intent = text.Substring("/neural".Length).Trim();
                if (string.IsNullOrWhiteSpace(intent) || _neural == null)
                {
                    StatusBar.SetStatus("Usage : /neural <intention>");
                    RefreshHomeStats();
                    return;
                }
                StatusBar.SetStatus($"🧬 Neural : {intent}…");
                var code = await Task.Run(() => _neural.Generate(intent));
                var doc = _viewModel.SelectedDocument;
                if (doc != null)
                {
                    doc.Text = doc.Text + "\n\n" + code;
                    EditorPane.EditorText = doc.Text;
                    StatusBar.SetStatus($"🧬 {code.Split('\n').Length} lignes générées.");
                }
                RefreshHomeStats();
                return;
            }

            // ★ /cortex
            if (text.StartsWith("/cortex", StringComparison.OrdinalIgnoreCase))
            {
                if (_cortex == null)
                {
                    StatusBar.SetStatus("Cortex non initialisé.");
                    return;
                }
                var stats = _cortex.GetStats();
                StatusBar.SetStatus(
                    $"🧠 Cortex : {stats.TotalHabits} hab · " +
                    $"{stats.TotalPatterns} patterns · " +
                    $"{stats.TotalCorrections} corrections");
                RefreshHomeStats();
                return;
            }

            // ── Commandes normales ──
            App.Breadcrumb($"OnAiCommandSubmitted — entrée : \"{text}\"");
            AiBar.SetBusy(true);
            try
            {
                if (AutoProjectBuilder.ShouldHandle(text))
                {
                    App.Breadcrumb("OnAiCommandSubmitted — route : AutoProjectBuilder");
                    var root = string.IsNullOrWhiteSpace(_currentRoot)
                        ? Path.Combine(Environment.GetFolderPath(
                            Environment.SpecialFolder.MyDocuments), "MotoProjects")
                        : _currentRoot;
                    var result = await _projectBuilder.BuildAsync(text, root);
                    if (result.Success)
                    {
                        var dir = _projectBuilder.ComputeProjectDir(text, root);
                        LoadWorkspace(dir);
                    }
                    StatusBar.SetStatus(result.Summary);
                    RefreshHomeStats();
                    return;
                }

                App.Breadcrumb("OnAiCommandSubmitted — route : chat (avant SendAsync)");
                await _chatService.SendAsync(text);
                App.Breadcrumb("OnAiCommandSubmitted — chat.SendAsync OK");

                // ★ CORRECTION (30/08) : la réponse était calculée et comptée dans les
                // stats (Threads/Messages) mais jamais affichée nulle part — repéré par
                // Tom ("ne génère rien du tout"). Aucune vue de conversation n'existe
                // encore dans ce dépôt (ChatHost/ThreadHost du dock droit sont des
                // placeholders jamais câblés) ; en attendant cette vue dédiée, on ouvre
                // la réponse comme un onglet via le mécanisme d'ouverture de fichier déjà
                // existant et testé (_viewModel.OpenFilePath), plutôt que de construire un
                // nouveau modèle de "document en mémoire sans fichier" en urgence.
                var reply = _chatService.CurrentThread?.Messages?.LastOrDefault(m => m.Role == "ai")?.Content;
                App.Breadcrumb($"OnAiCommandSubmitted — reply longueur={reply?.Length ?? -1}");
                if (!string.IsNullOrWhiteSpace(reply))
                {
                    var repliesDir = Path.Combine(Path.GetTempPath(), "MotoEditor-Reponses-IA");
                    Directory.CreateDirectory(repliesDir);
                    var replyPath = Path.Combine(repliesDir, $"Reponse-IA-{DateTime.Now:yyyyMMdd-HHmmss}.md");
                    File.WriteAllText(replyPath, reply);
                    _viewModel.OpenFilePath(replyPath);
                    if (_viewModel.SelectedDocument != null)
                        _viewModel.SelectedDocument.Text = reply;
                    StatusBar.SetStatus("✔ Réponse IA générée.");
                    App.Breadcrumb("OnAiCommandSubmitted — onglet ouvert avec succès");
                }
                else
                {
                    StatusBar.SetStatus("⚠ L'IA n'a rien répondu.");
                }
            }
            catch (Exception ex)
            {
                // ★ AJOUT (30/08) : ce bloc n'avait AUCUN catch — une exception ici
                // (ex. Ollama injoignable levant au lieu de renvoyer un texte de repli)
                // se propage hors d'un "async void" et ne montre RIEN à l'écran (juste
                // le filet WinUI global qui journalise), ce qui ressemble exactement à
                // "je tape, rien ne se passe" (repéré par Tom).
                App.LogCrash("OnAiCommandSubmitted", ex);
                StatusBar.SetStatus("⚠ Erreur IA : " + ex.Message);
            }
            finally
            {
                AiBar.SetBusy(false);
                RefreshHomeStats();
            }
        }

        // ------------------------------------------------------------------
        // ★ v29 : Commande /analytics (méthode séparée, propre)
        // ------------------------------------------------------------------
        private async Task HandleAnalyticsCommandAsync(string args)
        {
            if (_analytics == null)
            {
                StatusBar.SetStatus("Analytics non disponible.");
                return;
            }

            var sub = args.ToLowerInvariant();

            switch (sub)
            {
                case "top":
                    var top = _analytics.GetTopPaletteCommands(5);
                    StatusBar.SetStatus("🏆 Top 5 : " +
                        string.Join(" · ", top.Select(c => $"{c.ItemId.Split('.').Last()} ({c.ExecutedCount})")));
                    break;

                case "underperform":
                    var under = _analytics.GetUnderperformingSuggestions(3);
                    StatusBar.SetStatus("⚠️ À améliorer : " +
                        string.Join(" · ", under.Select(s => s.ItemId)));
                    break;

                case "export":
                    var report = _analytics.GetReport();
                    var allStats = _analytics.GetAllStats();
                    var exportData = new
                    {
                        GeneratedUtc = DateTime.UtcNow,
                        Report = report,
                        Stats = allStats
                    };
                    var json = System.Text.Json.JsonSerializer.Serialize(exportData,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    var exportDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "MotoEditor");
                    Directory.CreateDirectory(exportDir);
                    var path = Path.Combine(exportDir, $"analytics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
                    await File.WriteAllTextAsync(path, json);
                    StatusBar.SetStatus($"✅ Exporté : {Path.GetFileName(path)}");
                    break;

                case "dashboard":
                    if (_analyticsDashboard != null)
                    {
                        _analyticsDashboard.IsVisible = !_analyticsDashboard.IsVisible;
                        if (_analyticsDashboard.IsVisible)
                            _analyticsDashboard.SetAnalytics(_analytics);
                    }
                    else
                    {
                        StatusBar.SetStatus("Dashboard non disponible.");
                    }
                    break;

                default:
                    StatusBar.SetStatus(_analytics.GetReport());
                    break;
            }
        }
    }
}
