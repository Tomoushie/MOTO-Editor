// Moto.Editor/MainPage.UI.cs (v30 — câblage GlobalUsageEngine complet)
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using CommunityToolkit.Maui.Storage;
using Moto.Core.Export;
using Moto.Core.Remote;
using Moto.Core.Collab;
using Moto.Core.Settings;
using Moto.Editor.Models;
using Moto.Editor.Services;

namespace Moto.Editor
{
    /// <summary>
    /// Partial class : tous les handlers UI (toolbar, build, run, sandbox, license, lock).
    /// Aucune fonctionnalité supprimée — uniquement extraite de MainPage.xaml.cs.
    /// </summary>
    public partial class MainPage
    {
        // ── ★ v30 : Global Usage Engine ──
        private Moto.Core.Analytics.GlobalUsageEngine? _globalUsage;

        /// <summary>
        /// Initialise le GlobalUsageEngine.
        /// À appeler après InitializeMainPageExtensions() dans le constructeur.
        /// </summary>
        private void InitializeGlobalUsage()
        {
            try
            {
                var services = Handler?.MauiContext?.Services
                    ?? Application.Current?.Handler?.MauiContext?.Services;
                if (services == null) return;

                _globalUsage = services.GetService<Moto.Core.Analytics.GlobalUsageEngine>();
                _globalUsage?.StartSession();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlobalUsage] Erreur init : {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Toolbar handlers
        // ------------------------------------------------------------------
        private bool _pickingFolder;

        /// <summary>
        /// ★ CORRECTION (30/08) : plusieurs clics rapides sur "Projet logiciel"
        /// ouvraient chacun leur propre fenêtre d'explorateur Windows (repéré par
        /// Tom) — FolderPicker.Default.PickAsync() n'empêche pas les appels
        /// concurrents à lui tout seul. Garde de ré-entrance ajoutée.
        /// </summary>
        private async void OnImportClicked(object sender, EventArgs e)
        {
            if (_pickingFolder) return;
            _pickingFolder = true;
            try
            {
                var result = await FolderPicker.Default.PickAsync();
                if (!result.IsSuccessful) return;
                HandleImportedFolder(result.Folder.Path);
            }
            finally
            {
                _pickingFolder = false;
            }
        }

        private void HandleImportedFolder(string folderPath)
        {
            var report = _import.Analyze(folderPath);
            if (_lock.IsLocked(folderPath))
            {
                PasswordGate.Lock(folderPath);
                PasswordGate.Unlocked += () => LoadWorkspace(folderPath);
                return;
            }
            LoadWorkspace(folderPath);
            StatusBar.SetStatus($"Import : {report.DetectedIde} / {report.ProjectKind}");
        }

        private async void OnBuildClicked(object sender, EventArgs e)
        {
            StatusBar.SetStatus("Compilation…");
            var result = await _build.BuildAsync(_currentRoot);
            var errors = result.Diagnostics.Count(d => d.Severity == "error");
            var warnings = result.Diagnostics.Count(d => d.Severity == "warning");
            StatusBar.SetCounts(errors, warnings);
            StatusBar.SetStatus(result.Success ? "Build OK." : "Build en échec.");
            if (_viewModel.SelectedDocument != null)
                _viewModel.SelectedDocument.ErrorCount = errors;

            // ★ v30 : Track le build
            _globalUsage?.RecordBuild();
        }

        // ★ AJOUT (01/09, revue croisée — dock du bas Terminal) : conserve le
        // gestionnaire abonné pour pouvoir s'en désabonner avant d'en reposer un
        // neuf (voir OnPlayClicked). Fuite préexistante à cette session, restée
        // invisible tant que TerminalLines n'avait aucun écran pour l'afficher —
        // désormais visible (relancer "Play" plusieurs fois aurait dupliqué
        // chaque ligne de sortie autant de fois que de clics).
        private Action<string> _runOutputHandler;

        private void OnPlayClicked(object sender, EventArgs e)
        {
            if (_runOutputHandler != null)
                _run.OutputReceived -= _runOutputHandler;
            _runOutputHandler = line => MainThread.BeginInvokeOnMainThread(() =>
                _viewModel.TerminalLines.Add(new TerminalLine { Text = line }));
            _run.OutputReceived += _runOutputHandler;

            _viewModel.IsTerminalVisible = true;
            _run.Run(_currentRoot);
            StatusBar.SetStatus("Exécution…");

            // ★ v30 : Track la session debug
            _globalUsage?.RecordDebugSession();
        }

        private void OnStopClicked(object sender, EventArgs e)
        {
            _run.Stop();
            StatusBar.SetStatus("Arrêté.");
        }

        private async void OnSandboxClicked(object sender, EventArgs e)
        {
            if (!_inSandbox)
            {
                _realRoot = _currentRoot;
                _sandboxPath = _sandbox.Create(_realRoot, "test");
                _inSandbox = true;
                _currentRoot = _sandboxPath;
                ExplorerPanel.LoadFolder(_sandboxPath);
                StatusBar.SetSandbox(true);
            }
            else
            {
                var apply = await DisplayAlert("Sandbox",
                    "Appliquer les modifications au projet réel ?", "Appliquer", "Jeter");
                if (apply) _sandbox.ApplyToSource(_sandboxPath, _realRoot);
                else _sandbox.Discard(_sandboxPath);
                _inSandbox = false;
                _currentRoot = _realRoot;
                ExplorerPanel.LoadFolder(_realRoot);
                StatusBar.SetSandbox(false);
            }
        }

        private async void OnLicenseClicked(object sender, EventArgs e)
        {
            var choice = await DisplayActionSheet("Choisir une licence", "Annuler", null, _license.AvailableLicenses);
            if (choice == null || choice == "Annuler") return;

            var author = await DisplayPromptAsync("Licence", "Nom de l'auteur :");
            if (string.IsNullOrWhiteSpace(author)) return;

            foreach (var f in _license.Generate(choice, author, Path.GetFileName(_currentRoot)))
                File.WriteAllText(Path.Combine(_currentRoot, f.Path), f.Content);
            StatusBar.SetStatus($"LICENSE ({choice}) générée.");
        }

        private async void OnLockClicked(object sender, EventArgs e)
        {
            if (_lock.IsLocked(_currentRoot))
            {
                var pwd = await DisplayPromptAsync("Sécurité", "Mot de passe actuel :");
                if (pwd != null && _lock.Verify(_currentRoot, pwd))
                {
                    _lock.RemovePassword(_currentRoot);
                    StatusBar.SetLocked(false);
                }
            }
            else
            {
                var pwd = await DisplayPromptAsync("Sécurité", "Définir un mot de passe :");
                if (!string.IsNullOrWhiteSpace(pwd))
                {
                    _lock.SetPassword(_currentRoot, pwd);
                    StatusBar.SetLocked(true);
                }
            }
        }

        // ------------------------------------------------------------------
        // Panneaux toggle
        // ------------------------------------------------------------------
        private void OnToggleAiBarClicked(object sender, EventArgs e) => AiBar.Toggle();
        // ★ RETRAIT (02/09, état des lieux) : OnSettingsClicked supprimé — plus aucun
        // bouton/geste ne l'appelait depuis le 31/08 (SettingsWindow l'a remplacé),
        // mais SettingsMenu restait construit et abonné pour rien. Voir CreateHome.
        private void OnPresentationClicked(object sender, EventArgs e) => PresentationPanel.IsVisible = !PresentationPanel.IsVisible;
        private void OnRemoteClicked(object sender, EventArgs e) => RemotePanel.IsVisible = !RemotePanel.IsVisible;
        private void OnCollabClicked(object sender, EventArgs e) => CollabPanel.IsVisible = !CollabPanel.IsVisible;

        private void OnPlatformClicked(object sender, EventArgs e)
        {
            if (!_platformPanel.IsVisible) _platformPanel.Analyze();
            _platformPanel.IsVisible = !_platformPanel.IsVisible;
        }

        // ------------------------------------------------------------------
        // Présentation / Remote / Collab
        // ------------------------------------------------------------------
        private void OnPresentationGenerate(PresentationRequest req)
        {
            req.TargetPath = Path.Combine(
                _currentRoot ?? Path.GetTempPath(),
                $"Presentation_{req.ProjectName}");
            var result = _presentationEngine.Generate(req);
            PresentationPanel.ShowStatus(result.Message);
            StatusBar.SetStatus(result.Message);
        }

        private async void OnRemoteConnect(RemoteKind kind, string host, int port, string user, string token)
        {
            _remoteClient?.Dispose();
            _remoteClient = kind == RemoteKind.Ssh ? new SshRemoteClient() : new WebSocketRemoteClient();
            _remoteClient.MessageReceived += msg =>
                MainThread.BeginInvokeOnMainThread(() => StatusBar.SetStatus($"📥 {msg}"));
            var ok = await _remoteClient.ConnectAsync(host, port, user, token);
            RemotePanel.ShowStatus(ok ? $"✅ Connecté à {host}:{port}" : "❌ Connexion échouée.");
            StatusBar.SetStatus(ok ? "Remote connecté." : "Remote échoué.");
        }

        private async void OnCollabJoin(string name, string hostPort)
        {
            var parts = hostPort.Split(':');
            var host = parts[0];
            var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 8888;
            _collabSession.Self.Name = name;
            var ok = await _collabSession.JoinAsync(host, port, name);
            if (ok)
            {
                StatusBar.SetStatus($"👥 Connecté à {hostPort}");
                _collabSession.RemoteMessage += msg =>
                    MainThread.BeginInvokeOnMainThread(() => CollabPanel.AddChat(msg));
                _collabSession.RemotePatch += patch =>
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        var doc = _viewModel.SelectedDocument;
                        if (doc != null)
                        {
                            doc.Text = _collabSession.Patches.Apply(doc.Text, patch);
                            EditorPane.EditorText = doc.Text;
                        }
                    });
                var timer = new System.Timers.Timer(2000);
                timer.Elapsed += async (s, e) =>
                {
                    var online = _collabSession.Presence.Online();
                    MainThread.BeginInvokeOnMainThread(() =>
                        CollabPanel.SetPeers($"👥 {online.Count} en ligne : " +
                            string.Join(", ", online.Select(p => p.Name))));
                    await _collabSession.BroadcastPresenceAsync();
                };
                timer.Start();
            }
            else
            {
                StatusBar.SetStatus("❌ Session collab échouée.");
            }
        }

        private async void OnCollabChat(string msg)
        {
            CollabPanel.AddChat($"Toi : {msg}");
            await _collabSession.SendMessageAsync(msg);
        }

        // ------------------------------------------------------------------
        // Paramètres
        // ------------------------------------------------------------------
        private void ApplyLayoutSettings()
        {
            var s = SettingsEngine.Shared;
            StatusBar.ApplySettings(s);
            // ★ CORRECTION (30/08, refonte Zen) : "pp_dock" (Left/Right) n'est exposé
            // nulle part dans SettingsMenuView — réglage mort, jamais atteignable par
            // Tom. La colonne 0 est désormais fixe (dock IA, demandé "façon VS Code" à
            // gauche) : l'explorateur ne peut plus docker à gauche sans se superposer
            // au dock IA. Sa colonne (arborescence, à droite) est maintenant fixe.
            // ★ CORRECTION (01/09, "changer de côté") : "fixe" ne veut plus dire
            // "toujours colonne 2" depuis ApplySidePanelLayout (MainPage.Panels.cs) —
            // un Grid.SetColumn(ExplorerPanel, 2) codé en dur ICI aurait silencieusement
            // désynchronisé l'explorateur du dock IA (les 2 finissant superposés dans
            // la même colonne) dès qu'on rouvre les Réglages ou qu'on quitte le mode
            // plein écran de l'éditeur (OnMaximizeToggled appelle cette méthode) APRÈS
            // avoir inversé les panneaux. Remplacé par le vrai réappliqueur d'état, qui
            // replace aussi le dock IA et les 2 poignées de façon cohérente.
            ApplySidePanelLayout();
        }

        // ★ RETRAIT (02/09, état des lieux) : OnSettingChanged (ancien gestionnaire,
        // "theme"/"minimap"/"terminal"/"openproviders") supprimé — n'était déclenché
        // QUE par SettingsMenu.SettingChanged (menu Réglages mort, voir CreateHome),
        // donc jamais appelé depuis le 31/08. Le vrai chemin vivant pour thème/
        // police/mini-map est SettingsApplier (voir WireSettings + Settings/
        // SettingsApplier.cs, qui garde l'explication complète du choix "Clair"/
        // "Dynamique" sans effet). "terminal_show" est repris par
        // SettingsWindow.RealSettingChanged (aussi dans WireSettings).
        //
        // ⚠️ Effet de bord repéré en supprimant ceci : le cas "openproviders" était
        // le SEUL point d'entrée de tout le dépôt vers Pages/AiSettingsPage.xaml.cs
        // (grep confirmé) — cette page (config des providers IA externes) est donc
        // orpheline depuis le 31/08 elle aussi, pas seulement le menu qui l'ouvrait.
        // Pas retouché ici : à trancher avec Tom (redondante avec le catalogue de
        // 420 réglages de SettingsWindowView, ou vrai besoin d'un nouveau point
        // d'entrée ?) plutôt que de deviner. Noté dans CLAUDE.md.

        // ------------------------------------------------------------------
        // Navigation historique
        // ------------------------------------------------------------------
        private void OpenInEditor(string path)
        {
            _viewModel.OpenFilePath(path);
            if (!string.IsNullOrWhiteSpace(_currentPath) && _currentPath != path)
            {
                _historyBack.Push(_currentPath);
                _historyForward.Clear();
            }
            _currentPath = path;
            LoadDocumentIntoEditor(_viewModel.SelectedDocument);
        }

        private void LoadDocumentIntoEditor(EditorDocument doc)
        {
            if (doc == null) return;
            EditorPane.SetBreadcrumb(doc.Path);
            EditorPane.EditorText = doc.Text;
            _currentPath = doc.Path;
            if (_cortex != null && doc.Path != null)
                _cortexPanel.LoadSuggestions(doc.Path, doc.Text);
        }

        private void OnNavBack()
        {
            if (_historyBack.Count == 0) return;
            _historyForward.Push(_currentPath);
            var path = _historyBack.Pop();
            _currentPath = path;
            _viewModel.OpenFilePath(path);
            LoadDocumentIntoEditor(_viewModel.SelectedDocument);
        }

        private void OnNavForward()
        {
            if (_historyForward.Count == 0) return;
            _historyBack.Push(_currentPath);
            var path = _historyForward.Pop();
            _currentPath = path;
            _viewModel.OpenFilePath(path);
            LoadDocumentIntoEditor(_viewModel.SelectedDocument);
        }

        private void OnMaximizeToggled()
        {
            _maximized = !_maximized;
            EditorPane.SetMaximizeIcon(_maximized);
            // ★ RETRAIT (02/09, état des lieux) : ChatHost/ThreadHost supprimés
            // (stubs morts, voir MainPage.xaml) — ce cycle Maximiser/Restaurer était
            // justement ce qui les faisait réapparaître en permanence.
            ExplorerPanel.IsVisible = !_maximized;
            RefreshExplorerHandleVisibility();

            if (_maximized)
            {
                // ★ CORRECTION (30/08, refonte Zen) : 3 colonnes désormais (dock IA,
                // centre, arborescence) au lieu de 4 — span complet = 3.
                Grid.SetColumn(EditorPane, 0);
                Grid.SetColumnSpan(EditorPane, 3);
            }
            else
            {
                // Colonne centrale : 2 → 1 (voir MainPage.xaml, refonte Zen).
                Grid.SetColumn(EditorPane, 1);
                Grid.SetColumnSpan(EditorPane, 1);
                ApplyLayoutSettings();
            }
            StatusBar.SetStatus(_maximized ? "Éditeur en plein écran." : "Layout restauré.");
        }
    }
}
