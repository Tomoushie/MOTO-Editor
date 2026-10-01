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
using Moto.Editor.Settings;

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
                EditorPane.WorkspaceRoot = _sandboxPath;
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
                EditorPane.WorkspaceRoot = _realRoot;
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
            // ★ AJOUT (28/09) : la barre d'onglets de l'éditeur reçoit enfin ses réglages
            // (famille « Fenêtre & Layout / Tab Bar », clés tabs_*) — elle n'en appliquait
            // AUCUN jusque-là (tabs_show, tabs_bar_buttons, tabs_nav_buttons,
            // tabs_file_icons, tabs_show_diagnostics, tabs_close_position, tabs_show_close).
            EditorPane.ApplySettings(s);
            // ★ AJOUT (01/10, lot auto_*) : auto_indent s'applique au démarrage via le même point
            // d'accroche (EditorPane.ApplyAutoSettings → CodeEditorView.SetAutoIndent). Sans ce
            // chemin, le réglage ne serait posé que sur changement live, et auto_indent=false ne
            // survivrait pas à un redémarrage (le JS du WebView repart sur AUTO_INDENT=true).
            EditorPane.ApplyAutoSettings(s);
            // ★ AJOUT (01/10) : la barre de titre reçoit elle aussi ses réglages
            // (famille « Fenêtre & Layout / Title Bar », clés tb_*) — tb_menus,
            // tb_project_items, tb_branch_name et tb_button_layout n'étaient lus par
            // AUCUN code compilé jusque-là. Même point d'accroche que StatusBar et
            // EditorPane : démarrage, chaque changement de réglage, retour de plein écran.
            MenuBar.ApplySettings(s);
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
            // ★ AJOUT (01/10) : l'explorateur de fichiers reçoit enfin ses réglages (famille
            // « Panneaux / Project Panel », clés pp_*) — aucun n'était lu par du code compilé
            // jusque-là (constat mesuré par scripts/settings-coverage.ps1). Même point d'accroche
            // que StatusBar / EditorPane / MenuBar ci-dessus : démarrage, chaque changement de
            // réglage et retour de plein écran.
            ExplorerPanel.ApplySettings(s);
            // ★ AJOUT (01/10) : le panneau Git reçoit enfin ses réglages (famille « Panneaux /
            // Git Panel », clés gp_*) — les 15 clés étaient déclarées au catalogue et lues par
            // AUCUN code compilé. Le panneau est construit par la fenêtre spécialisée « Git » :
            // il faut donc le retrouver via cette fenêtre (instance unique, WindowManager) et
            // non via une référence directe, sinon le réglage ne prendrait qu'à la réouverture.
            var gitPanel = _windowManager?.Get(Moto.Editor.Windows.WindowKind.Git) is { Page: var gitPage }
                ? FindGitPanel(gitPage)
                : null;
            if (gitPanel is not null)
                ApplyGitPanelSettings(gitPanel, s);
            // ★ AJOUT (01/10, git_integration) : décocher « Git Integration » doit fermer une
            // fenêtre Git DÉJÀ ouverte — sinon l'affichage mentirait au moment même du
            // changement (réglage décoché, panneau visible), cas d'école de « réglage qui
            // affiche une valeur fausse ». Close est sans effet si la fenêtre est fermée
            // (WindowManager.Close → Get null → no-op), donc sans risque aux autres passages
            // de cette méthode (démarrage, retour de plein écran).
            if (!Settings.GitSettings.Integration(s) && _windowManager?.Get(Moto.Editor.Windows.WindowKind.Git) is not null)
                _windowManager.Close(Moto.Editor.Windows.WindowKind.Git);
            ApplyPanelGeometrySettings(s);
            // ★ AJOUT (01/10) : géométrie/dock des familles « Panneaux » ap_* (Agent Panel,
            // qui est en réalité le panneau de chat IA — voir DockPanelSettings) et cp_*
            // (Collaboration Panel). Aucune de ces clés n'était lue par du code compilé :
            // la fenêtre Réglages les affichait, rien ne les appliquait.
            ApplyAgentAndCollabPanelSettings(s);
            // ★ AJOUT (01/10) : le dock Terminal reçoit lui aussi ses réglages (famille
            // « Terminal », clés terminal_*) — police des lignes et de la saisie,
            // hauteur du dock. Même point d'accroche que StatusBar / EditorPane /
            // MenuBar / ExplorerPanel / panneau Git : démarrage, chaque changement de
            // réglage et retour de plein écran.
            ApplyTerminalSettings(s);
        }

        /// <summary>★ AJOUT (01/10) : dernière hauteur posée par terminal_default_height.</summary>
        private double _appliedTerminalHeight = -1;

        /// <summary>
        /// ★ AJOUT (01/10) : applique les réglages d'AFFICHAGE de la famille
        /// <c>terminal_*</c> sur le dock du bas (<c>TerminalPanel</c>, contrôle
        /// statique de MainPage.xaml) :
        ///   - <c>terminal_default_height</c> : hauteur du dock. Le XAML part de 220,
        ///     le défaut DÉCLARÉ est 320 — c'est ce que la fenêtre Réglages affiche,
        ///     donc c'est lui qui s'applique au premier passage. On ne réécrit que si
        ///     la valeur RÉGLÉE a changé : sinon, réappliquer à chaque passage
        ///     annulerait le geste de l'utilisateur sur la poignée
        ///     <c>BottomDockResizeHandle</c> (patron identique à <c>pp_width</c> et
        ///     <c>ap_width</c>) ;
        ///   - <c>terminal_font_size</c> / <c>terminal_font_family</c> : ressources
        ///     <c>TerminalFontSize</c>/<c>TerminalFontFamily</c> de la vue, en
        ///     <c>DynamicResource</c> — la mise à jour touche d'un coup les lignes de
        ///     sortie (DataTemplate, inatteignable depuis le code-behind : un x:Name
        ///     posé dans un DataTemplate est instancié une fois PAR LIGNE) et le
        ///     champ de saisie.
        /// </summary>
        private void ApplyTerminalSettings(SettingsEngine s)
        {
            // Garde de démarrage : ApplyLayoutSettings est appelée depuis WireSettings(),
            // avant que la vue soit montée — même parade NullReferenceException que
            // ApplyAgentAndCollabPanelSettings (constatée le 01/10 sur ap_height).
            if (TerminalPanel is null) return;

            var height = TerminalSettings.DefaultHeight(s);
            if (Math.Abs(height - _appliedTerminalHeight) > 0.01)
            {
                _appliedTerminalHeight = height;
                TerminalPanel.HeightRequest = height;
            }

            TerminalPanel.Resources["TerminalFontSize"] = (double)TerminalSettings.FontSize(s);
            TerminalPanel.Resources["TerminalFontFamily"] = TerminalSettings.FontFamily(s);

            // ★ AJOUT (01/10) : terminal_breadcrumbs pilote le titre de l'en-tête via
            // MainViewModel.TerminalTitle (le VM recalcule depuis le répertoire réel du
            // shell) — sans ce retour, basculer le réglage ne se verrait qu'au prochain
            // démarrage de shell.
            _viewModel.SetTerminalBreadcrumbs(TerminalSettings.Breadcrumbs(s));
        }

        /// <summary>★ AJOUT (01/10) : dernières valeurs posées par les réglages ap_* / cp_*.</summary>
        private double _appliedAiDockWidth = -1;
        private double _appliedAiChatHeight = -1;
        private double _appliedCollabWidth = -1;
        private bool _appliedCollabLeft;

        /// <summary>
        /// ★ AJOUT (01/10) : applique la GÉOMÉTRIE et le DOCK des panneaux agent (ap_*) et
        /// collaboration (cp_*). Appelée depuis ApplyLayoutSettings — donc au démarrage, à chaque
        /// changement de réglage et au retour de plein écran, exactement comme StatusBar /
        /// EditorPane / MenuBar / ExplorerPanel juste au-dessus.
        ///
        /// ⚠️ Le côté du dock IA (ap_dock) réutilise le mécanisme EXISTANT _panelsSwapped +
        /// ApplySidePanelLayout (celui du menu ⚙ « Disposition des panneaux ») — il n'y a
        /// volontairement PAS de 2e système de placement, qui laisserait les poignées
        /// désynchronisées de leur dock.
        /// </summary>
        private void ApplyAgentAndCollabPanelSettings(SettingsEngine s)
        {
            // ⚠️ CORRECTION (01/10) : ApplyLayoutSettings est appelée depuis WireSettings(),
            // donc AVANT WirePanels() (MainPage.xaml.cs) qui construit _aiChatPanel. Au tout
            // premier passage, ce champ est donc encore null : sans cette garde, le démarrage
            // levait une NullReferenceException AVANT même que la fenêtre existe (trouvé par
            // scripts/visual-lot-verify.ps1, contrôle 4 « démarrage réel »). On sort simplement :
            // le premier ApplyLayoutSettings utile passera après WirePanels, et
            // RealSettingChanged couvre les changements ultérieurs.
            if (_aiChatPanel is null) return;

            // ── ap_dock : côté du dock qui héberge le panneau IA ────────────────
            var wantAiLeft = DockPanelSettings.DockLeft(s);
            if (wantAiLeft != !_panelsSwapped)
            {
                _panelsSwapped = !wantAiLeft;
                ApplySidePanelLayout();
            }

            // ── ap_width : largeur du dock IA ───────────────────────────────────
            // ⚠️ Comme pour pp_width : on ne réécrit que si la valeur RÉGLÉE a changé, sinon
            // réappliquer le réglage à chaque passage annulerait le geste de l'utilisateur
            // sur la poignée d'étirement (qui écrit directement dans WidthRequest).
            var aiWidth = DockPanelSettings.Width(s);
            if (Math.Abs(aiWidth - _appliedAiDockWidth) > 0.01)
            {
                _appliedAiDockWidth = aiWidth;
                AiDockPanel.WidthRequest = aiWidth;
            }

            // ── ap_height : hauteur visée pour le panneau de chat ──────────────
            // AiChatView.FitToViewport recalcule sa hauteur à chaque changement de mise en page
            // (la colonne peut contenir d'autres panneaux au-dessus) : poser HeightRequest ici
            // ne ferait que donner la valeur de DÉPART, ce qui est bien ce qu'annonce le libellé
            // (« Hauteur par défaut »). On ne l'impose donc que si le réglage a changé, pour ne
            // pas écraser le recalcul permanent de la vue.
            var aiHeight = DockPanelSettings.Height(s);
            if (Math.Abs(aiHeight - _appliedAiChatHeight) > 0.01)
            {
                _appliedAiChatHeight = aiHeight;
                _aiChatPanel.HeightRequest = aiHeight;
            }

            // ── cp_width : largeur du panneau collaboration ─────────────────────
            var collabWidth = DockPanelSettings.CollabWidth(s);
            if (Math.Abs(collabWidth - _appliedCollabWidth) > 0.01)
            {
                _appliedCollabWidth = collabWidth;
                CollabPanel.WidthRequest = collabWidth;
            }

            // ── cp_dock : côté du panneau collaboration ─────────────────────────
            // Le panneau est un OVERLAY flottant ancré sur la colonne centrale (voir MainPage.xaml,
            // avec les 2 corrections d'alignement du 31/08) : changer de côté, c'est donc échanger
            // son HorizontalOptions, pas le déplacer de colonne. On garde la colonne 1 (bord
            // stable, correction du 31/08) et la marge basse (demande explicite de Tom), seuls
            // l'ancrage horizontal et la marge opposée changent.
            var collabLeft = DockPanelSettings.CollabDockLeft(s);
            if (collabLeft != _appliedCollabLeft)
            {
                _appliedCollabLeft = collabLeft;
                CollabPanel.HorizontalOptions = collabLeft ? LayoutOptions.Start : LayoutOptions.End;
                CollabPanel.Margin = collabLeft ? new Thickness(20, 0, 0, 10) : new Thickness(0, 0, 20, 10);
            }

            // ── agent_font_size : taille du texte du panneau IA (lot agent_*) ────
            // Ressource AgentFontSize posée sur AiChatView, consommée en
            // DynamicResource par les 3 éléments de texte (bulles + saisie) — même
            // mécanisme exact que TerminalFontSize pour le dock du bas. Le défaut
            // déclaré (13) est IDENTIQUE à FontSizeBody : avec le réglage intact,
            // aucun pixel ne bouge (imposé par la règle « jamais d'affichage faux »).
            // On ne réécrit que si la valeur a changé (patron _applied* de la classe).
            var agentFontSize = Settings.AgentSettings.FontSize(s);
            if (Math.Abs(agentFontSize - _appliedAgentFontSize) > 0.01)
            {
                _appliedAgentFontSize = agentFontSize;
                _aiChatPanel.Resources["AgentFontSize"] = (double)agentFontSize;
            }
        }

        /// <summary>★ AJOUT (01/10, lot agent_*) : dernière taille posée par agent_font_size.</summary>
        private double _appliedAgentFontSize = -1;

        /// <summary>
        /// ★ AJOUT (01/10) : parcourt l'arbre des éléments d'une fenêtre pour retrouver le
        /// panneau Git. Nécessaire parce que la fenêtre spécialisée « Git » est construite À LA
        /// DEMANDE par OpenSpecializedWindow (MainPage.Extensions.cs) et que MainPage n'en garde
        /// aucune référence de champ — la seule source de vérité est le WindowManager. Parcours
        /// volontairement borné (profondeur) pour ne pas dépendre d'une structure d'arbre précise.
        ///
        /// ⚠️ N'utilise PAS <c>Element.LogicalChildren</c> : l'API est marquée obsolète par MAUI
        /// (« Hot Reload seulement ») et ferait monter la ligne de base d'avertissements du dépôt.
        /// <see cref="Microsoft.Maui.IVisualTreeElement.GetVisualChildren"/> est l'API publique
        /// et pérenne — c'est elle que MAUI recommande dans le message d'obsolescence lui-même.
        /// </summary>
        private static Views.GitPanelView? FindGitPanel(Microsoft.Maui.IVisualTreeElement? root, int depth = 0)
        {
            if (root is null || depth > 12) return null;
            if (root is Views.GitPanelView panel) return panel;

            foreach (var child in root.GetVisualChildren())
                if (FindGitPanel(child, depth + 1) is { } found)
                    return found;

            return null;
        }

        /// <summary>
        /// ★ AJOUT (01/10) : applique les réglages gp_* au panneau Git, plus l'abonnement à son
        /// clic (réglage <c>gp_click_behavior</c>). L'abonnement est posé UNE SEULE FOIS par vue
        /// (marqueur sur la vue elle-même) : ApplyLayoutSettings est appelée à chaque changement
        /// de réglage, un += non gardé aurait accumulé les abonnements et ouvert plusieurs fois
        /// le même fichier.
        /// </summary>
        private void ApplyGitPanelSettings(Views.GitPanelView panel, SettingsEngine s)
        {
            panel.ApplySettings(s);

            if (!_gitPanelWired.Add(panel)) return;
            panel.FileOpenRequested += path => MainThread.BeginInvokeOnMainThread(() => OpenInEditor(path));
        }

        /// <summary>★ AJOUT (01/10) : panneaux Git déjà câblés (un seul en pratique, mais la
        /// fenêtre peut être fermée puis rouverte — le nouveau panneau doit être câblé à son tour).</summary>
        private readonly System.Collections.Generic.HashSet<Views.GitPanelView> _gitPanelWired = new();

        /// <summary>
        /// ★ AJOUT (01/10) : ouvre la fenêtre « Git » au démarrage quand <c>gp_starts_open</c> est
        /// coché (défaut déclaré : non — l'ouverture ne change donc rien sur une installation
        /// neuve). Appelée par OnPageLoaded, seule à savoir que l'application vient de démarrer :
        /// <c>ApplyLayoutSettings</c> est aussi appelée au retour de plein écran, et y ouvrir la
        /// fenêtre la rouvrirait à chaque F11, ce qu'aucun libellé n'annonce.
        /// </summary>
        private void ApplyGitStartupSetting()
        {
            if (!GitPanelSettings.StartsOpen(SettingsEngine.Shared)) return;
            if (_gitService is null) return;
            OpenSpecializedWindow("git");
        }

        /// <summary>★ AJOUT (01/10) : dernière largeur d'explorateur posée par le réglage pp_width.</summary>
        private double _appliedExplorerWidth = -1;

        /// <summary>
        /// ★ AJOUT (01/10) : les réglages pp_* qui portent sur la COLONNE de l'explorateur
        /// (pp_width, pp_dock). Ils ne sont pas traités dans FileExplorerView parce que celle-ci
        /// ne possède ni la colonne de la grille racine ni la poignée de redimensionnement —
        /// ces éléments vivent dans MainPage.xaml.
        /// </summary>
        private void ApplyPanelGeometrySettings(SettingsEngine s)
        {
            // ── pp_width : largeur du panneau projet ────────────────────────────
            // Appliquée à ExplorerPanel ET Sidebar ensemble : ils occupent la même colonne, une
            // largeur divergente ferait sauter la colonne au basculement Fichiers/Sessions (même
            // raison que dans OnExplorerResizePanUpdated).
            // ⚠️ On ne réécrit que si la valeur DÉCLARÉE a changé : la poignée de redimensionnement
            // écrit directement dans WidthRequest sans passer par le store, donc réappliquer le
            // réglage à chaque passage ici annulerait un geste de l'utilisateur — comportement
            // voulu (le réglage reste la source de vérité), mais seulement quand il change.
            var width = PanelSettings.Width(s);
            if (Math.Abs(width - _appliedExplorerWidth) > 0.01)
            {
                _appliedExplorerWidth = width;
                ExplorerPanel.WidthRequest = width;
                Sidebar.WidthRequest = width;
            }

            // ── pp_dock : côté de l'explorateur ─────────────────────────────────
            // Le catalogue déclare "Right" par défaut, qui correspond à l'état normal
            // (_panelsSwapped == false). « Left » revient donc exactement à ce que fait déjà la
            // commande ⚙ « Disposition des panneaux » : on réutilise ce chemin (_panelsSwapped +
            // ApplySidePanelLayout) plutôt que d'en créer un second, qui aurait pu laisser les
            // deux poignées désynchronisées de leur dock.
            var wantLeft = PanelSettings.DockLeft(s);
            if (wantLeft != _panelsSwapped)
            {
                _panelsSwapped = wantLeft;
                ApplySidePanelLayout();
            }

            ApplyGitWindowPlacement(s);
        }

        /// <summary>
        /// ★ AJOUT (01/10) : position et largeur de la fenêtre « Git » (réglages <c>gp_dock</c> et
        /// <c>gp_width</c>). Ces deux clés portent sur la FENÊTRE, pas sur le contenu du panneau :
        /// c'est pourquoi elles sont traitées ici et non dans <c>GitPanelView</c>, exactement comme
        /// <c>pp_width</c>/<c>pp_dock</c> le sont pour la colonne de l'explorateur.
        ///
        /// N'a d'effet que si la fenêtre EXISTE déjà : aucune fenêtre n'est créée ici — c'est
        /// <c>gp_starts_open</c> qui décide de l'ouvrir au démarrage, et un changement de
        /// <c>gp_dock</c> alors qu'elle est ouverte doit la déplacer tout de suite (sinon le
        /// réglage se lirait comme inerte, piège déjà rencontré sur les <c>tb_*</c>).
        /// </summary>
        private void ApplyGitWindowPlacement(SettingsEngine s)
        {
#if WINDOWS
            var gitWindow = _windowManager?.Get(Moto.Editor.Windows.WindowKind.Git);
            var native = gitWindow?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
            if (native is null) return;

            var hwnd = global::WinRT.Interop.WindowNative.GetWindowHandle(native);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
            if (area is null) return;

            var wa = area.WorkArea;
            // Hauteur : celle que la fenêtre a déjà — rien dans la famille gp_* ne gouverne la
            // hauteur, donc on n'en invente pas une (même règle que pour les valeurs affichées).
            var height = Math.Min(Math.Max(480, appWindow.Size.Height), wa.Height);
            var width = Math.Min(GitPanelSettings.Width(s), wa.Width);
            const int margin = 24;

            global::Windows.Graphics.PointInt32 position;
            switch (GitPanelSettings.Dock(s))
            {
                case "Left":
                    position = new global::Windows.Graphics.PointInt32(wa.X + margin, wa.Y + margin);
                    break;
                case "Bottom":
                    // Ancrage en bas, centré horizontalement : la fenêtre garde la largeur
                    // gp_width (le catalogue ne décrit pas une fenêtre pleine largeur).
                    position = new global::Windows.Graphics.PointInt32(
                        wa.X + Math.Max(0, (wa.Width - width) / 2),
                        wa.Y + Math.Max(0, wa.Height - height - margin));
                    break;
                default: // "Right" — défaut DÉCLARÉ au catalogue
                    position = new global::Windows.Graphics.PointInt32(
                        wa.X + Math.Max(0, wa.Width - width - margin),
                        wa.Y + margin);
                    break;
            }

            appWindow.Resize(new global::Windows.Graphics.SizeInt32(width, height));
            appWindow.Move(position);
#endif
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
            ExplorerPanel.SetActiveFile(doc.Path); // ★ (25/09) : ligne du fichier affiché surlignée dans l'explorateur
            // ★ AJOUT (01/10) : alimente la puce « fichier actif » de la barre de statut
            // (réglage sb_active_file). Même point d'appel unique que le fil d'Ariane —
            // pas de 2e mécanisme qui pourrait diverger.
            StatusBar.SetActiveFile(doc.Path);
            // ★ AJOUT (01/10, tranche 2) : puce « fins de ligne » (sb_line_endings).
            // Détection RÉELLE depuis doc.Text (le texte chargé, dont File.ReadAllText
            // préserve les fins de ligne) — jamais de valeur inventée.
            StatusBar.SetLineEndings(DetectLineEndings(doc.Text));
            EditorPane.EditorText = doc.Text;
            _currentPath = doc.Path;
            RefreshAiUndoButton();
            if (_cortex != null && doc.Path != null)
                _cortexPanel.LoadSuggestions(doc.Path, doc.Text);
        }

        /// <summary>
        /// ★ AJOUT (01/10, tranche 2) : détecte les fins de ligne RÉELLES du texte
        /// chargé. « CRLF » si les retours Windows dominent (ou à égalité), « LF » si
        /// les sauts Unix dominent, <c>null</c> si aucun retour à la ligne (fichier
        /// d'une seule ligne : il n'y a pas de « fins de ligne » à afficher — la puce
        /// reste masquée, jamais de valeur inventée).
        /// </summary>
        private static string? DetectLineEndings(string? text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int crlf = 0, lf = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n') { crlf++; i++; }
                }
                else if (text[i] == '\n') { lf++; }
            }
            if (crlf == 0 && lf == 0) return null;
            return crlf >= lf ? "CRLF" : "LF";
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
