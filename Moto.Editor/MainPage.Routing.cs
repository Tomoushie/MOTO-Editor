// Moto.Editor/MainPage.Routing.cs (v29 corrigé — espaces supprimés + /window déplacé)
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Analytics;
using Moto.Core.AI.Builders;
using Moto.Core.AI.Commands;
using Moto.Core.Export;
using Moto.Core.Settings;
using Moto.Editor.Models;
using Moto.Editor.Services;

namespace Moto.Editor
{
    /// <summary>
    /// Partial class : routeurs (menus custom, activity bar, commandes slash).
    /// </summary>
    public partial class MainPage
    {
        // ★ AJOUT (01/09, point 10) : connexion GitHub réelle (device flow OAuth).
        private readonly GitHubAccountService _gitHubAccount = new();
        private CancellationTokenSource? _gitHubConnectCts;

        // ------------------------------------------------------------------
        // Routeur des menus custom
        // ------------------------------------------------------------------
        // ★ AJOUT (02/09, "vrai registre de commandes" — fondation Zed/VS Code,
        // choisi par Tom en retour de la sonde de modularité du même jour). Ce
        // switch(id) — une trentaine de cas codés en dur, chacun appelant
        // directement une méthode privée de MainPage — a été remplacé par
        // CommandRegistry (Moto.Core), une vraie table id → action remplie une
        // seule fois au démarrage (voir RegisterMenuCommands ci-dessous, appelée
        // depuis le constructeur de MainPage). Comportement IDENTIQUE pour Tom :
        // mêmes identifiants, mêmes actions, juste une tuyauterie différente.
        // Ce que ça ouvre pour plus tard (PAS fait ici, hors scope de cette
        // étape) : un futur système de plugins pourrait appeler
        // _commandRegistry.Register(...) pour ajouter ses propres commandes sans
        // jamais modifier ce fichier — voir CLAUDE.md, section "Modularité façon
        // Zed/VS Code", Gap B.
        private readonly CommandRegistry _commandRegistry = new();

        private void OnMenuCommanded(string id) => _commandRegistry.Execute(id);

        /// <summary>Remplit _commandRegistry une seule fois au démarrage — un
        /// enregistrement par identifiant du switch qu'elle remplace, dans le même
        /// ordre, pour qu'un futur diff reste facile à relire.</summary>
        private void RegisterMenuCommands()
        {
            _commandRegistry.Register("file.opendir", () => _viewModel.OpenFileCommand.Execute(null));
            _commandRegistry.Register("file.openfile", () => _viewModel.OpenFileCommand.Execute(null));
            _commandRegistry.Register("file.save", () => _viewModel.SaveCommand.Execute(null));
            _commandRegistry.Register("file.import", () => OnImportClicked(null, null));
            _commandRegistry.Register("file.export", () => ExportMenu.IsVisible = !ExportMenu.IsVisible);
            _commandRegistry.Register("file.lock", () => OnLockClicked(null, null));

            _commandRegistry.Register("edit.search", () => AiBar.Toggle());
            _commandRegistry.Register("edit.commands", () => AiBar.Toggle());

            _commandRegistry.Register("view.explorer", () => ToggleSide(isExplorer: true));
            _commandRegistry.Register("view.sidebar", () => ToggleSide(isExplorer: false));
            _commandRegistry.Register("view.aipanel", () =>
            {
                _aiChatPanel.IsVisible = !_aiChatPanel.IsVisible;
                RefreshAiDockColumnWidth();
            });
            _commandRegistry.Register("view.terminal", () => _viewModel.IsTerminalVisible = !_viewModel.IsTerminalVisible);
            _commandRegistry.Register("view.diagnostics", () => _viewModel.IsDiagnosticsVisible = !_viewModel.IsDiagnosticsVisible);
            _commandRegistry.Register("view.maximize", () => OnMaximizeToggled());
            _commandRegistry.Register("view.theme", () => ThemeService.SetDark());

            // ★ AJOUT (02/09, état des lieux) : "Paramètres" dans la barre de
            // recherche de commandes (Ctrl+Shift+P) envoie "menu:settings", qui
            // tombait sans aucun cas correspondant — cliquer dessus ne faisait
            // rien. Même appel que le menu ⚙ (voir plus bas, OnGearMenuItemSelected).
            _commandRegistry.Register("settings", () => SettingsWindow.Show("Général"));

            _commandRegistry.Register("nav.back", () => OnNavBack());
            _commandRegistry.Register("nav.forward", () => OnNavForward());

            _commandRegistry.Register("run.build", () => OnBuildClicked(null, null));
            _commandRegistry.Register("run.play", () => OnPlayClicked(null, null));
            _commandRegistry.Register("run.stop", () => OnStopClicked(null, null));
            _commandRegistry.Register("run.sandbox", () => OnSandboxClicked(null, null));

            _commandRegistry.Register("ai.cortex", () => OnCortexClicked(null, null));
            _commandRegistry.Register("ai.neural", () => OnNeuralClicked(null, null));
            _commandRegistry.Register("ai.workspace", () => OnWorkspaceClicked(null, null));
            _commandRegistry.Register("ai.autolink", () => AutoLinkPanel.IsVisible = !AutoLinkPanel.IsVisible);
            _commandRegistry.Register("ai.context", () => ContextPanel.IsVisible = !ContextPanel.IsVisible);
            _commandRegistry.Register("ai.evolution", () => StatusBar.SetStatus("🧬 Evolution…"));
            _commandRegistry.Register("ai.story", () => StatusBar.SetStatus("📚 Story Mode…"));
            _commandRegistry.Register("ai.health", () => StatusBar.SetStatus("🩺 Health…"));
            _commandRegistry.Register("ai.timemachine", () => StatusBar.SetStatus("🕘 Time Machine…"));
            _commandRegistry.Register("ai.doc", () => DocPanel.IsVisible = !DocPanel.IsVisible);
            _commandRegistry.Register("ai.platform", () => OnPlatformClicked(null, null));
            _commandRegistry.Register("ai.presentation", () => OnPresentationClicked(null, null));
            _commandRegistry.Register("ai.remote", () => OnRemoteClicked(null, null));
            _commandRegistry.Register("ai.collab", () => OnCollabClicked(null, null));
            _commandRegistry.Register("ai.gallery", () => OnGalleryClicked());
            // ★ AJOUT (02/09, réveil de MotoAiPage) : jamais navigable auparavant
            // (ni DI, ni Navigation.PushAsync nulle part — confirmé par recherche
            // avant ce correctif). Pas de dépendance à résoudre (MotoAiService
            // s'auto-construit) — fire-and-forget, comme les autres actions
            // ci-dessus qui ne bloquent pas sur un résultat.
            _commandRegistry.Register("ai.motopage", () => _ = Navigation.PushAsync(new Pages.MotoAiPage()));
            // ★ AJOUT (03/09, réveil de GlobalDashboardView) : ouvre dans sa propre
            // fenêtre (même mécanisme que le bouton ⧉ des panneaux ancrés) — pas
            // ajouté au dock IA lui-même dans cette passe, pour rester un petit
            // chantier contenu.
            _commandRegistry.Register("ai.globaldashboard", () => OpenSpecializedWindow("globaldashboard"));
            _commandRegistry.Register("ai.threadlist", () => OpenSpecializedWindow("threadlist"));
            _commandRegistry.Register("ai.claudeshell", () => OpenSpecializedWindow("claudeshell"));
            _commandRegistry.Register("ai.backgroundtasks", () => OpenSpecializedWindow("backgroundtasks"));
            _commandRegistry.Register("ai.agentruns", () => OpenSpecializedWindow("agentruns"));
            _commandRegistry.Register("git.panel", () => OpenSpecializedWindow("git"));

            _commandRegistry.Register("term.open", () => _viewModel.IsTerminalVisible = true);
            _commandRegistry.Register("help.doc", () => DocPanel.IsVisible = true);
            _commandRegistry.Register("help.about", () => StatusBar.SetStatus("MOTO Editor v0.5 — AI Workspace"));

            // ★ AJOUT (31/08) : engrenage ⚙ ou avatar "Moi" de la barre de titre
            // (points 1, 2, 11 de Tom) — CustomMenuBarView lève cet id via
            // MenuCommanded (événement déjà existant, jamais utilisé jusqu'ici).
            _commandRegistry.Register("gear.toggle", () => GearMenu.IsVisible = !GearMenu.IsVisible);

            // ★ AJOUT (31/08) : Fichiers/Recherche/IA/Cortex/Collab, rapatriés dans
            // la barre de titre (Tom veut tout sur une seule ligne — voir
            // CustomMenuBarView.xaml, ActivityBarView retirée de MainPage.xaml).
            // Réutilise OnActivitySelected tel quel : même id, même logique
            // d'ouverture/fermeture des panneaux, rien d'autre à changer.
            foreach (var activityId in new[] { "explorer", "search", "ai", "cortex", "collab" })
                _commandRegistry.Register(activityId, () => OnActivitySelected(activityId));
        }

        /// <summary>
        /// ★ AJOUT (31/08) : choix fait dans GearMenu (voir MainPage.xaml.cs pour
        /// l'abonnement GearMenu.ItemSelected). Réglages/Thèmes/Raccourcis ouvrent la
        /// fenêtre de Réglages directement sur la bonne catégorie ; Extensions
        /// réutilise la vraie Galerie de plugins déjà câblée ailleurs. Les autres
        /// (Utilisateur/Organisation/Se déconnecter) n'ont pas de fonctionnalité
        /// réelle derrière — pas de système de compte dans MOTO Editor — message
        /// honnête plutôt que simuler un effet qui n'existe pas.
        /// ★ RETRAIT (01/09) : "Disposition des panneaux" sort de ce lot — voir le
        /// cas "panellayout" ci-dessous, câblé sur ApplySidePanelLayout()
        /// (MainPage.Panels.cs).
        /// </summary>
        // ★ CORRECTION (31/08) : les 5 items sans fonctionnalité réelle passaient par
        // StatusBar.SetStatus — trop discret en bas de fenêtre, Tom avait l'impression
        // que "rien ne s'ouvrait" alors que ça changeait bien le texte (repéré :
        // "Organisation n'ouvre rien" / "tous les autres n'ouvrent rien"). Remplacé
        // par une vraie boîte de dialogue (DisplayAlert), impossible à manquer.
        // "Utilisateur" reformulé selon ce que Tom a précisé vouloir en faire
        // (connexion GitHub pour mise à jour auto + accès dépôt une fois publié) —
        // pas encore construit, mais le message dit maintenant CE VERS QUOI ça va,
        // pas juste "pas disponible".
        private async void OnGearMenuItemSelected(string id)
        {
            GearMenu.IsVisible = false;
            switch (id)
            {
                // ★ CORRECTION (02/09, chantier Réglages 100+) : chaînes anglaises
                // maison ("General"/"Appearance"/"Keymap") remplacées par les vraies
                // catégories françaises du catalogue (SettingsCatalog.All) — voir
                // SettingsWindowView.xaml.cs, réécrit pour lire ce catalogue plutôt
                // qu'une liste maison qui utilisait ces anciennes clés.
                case "settings": SettingsWindow.Show("Général"); break;
                case "theme": SettingsWindow.Show("Apparence"); break;
                case "keymap": SettingsWindow.Show("Raccourcis"); break;
                case "extensions": OnGalleryClicked(); break;
                // ★ AJOUT (01/09, point 10) : connexion GitHub réelle (device flow OAuth,
                // Client ID fourni par Tom — app "MOTO Editor Local"). Honnêteté sur la
                // portée : la CONNEXION est réelle (jeton obtenu, nom d'utilisateur
                // vérifié) ; publier/importer un projet sur GitHub, télécharger les MAJ
                // depuis le dépôt ne sont PAS construits — chantiers séparés, plus gros,
                // qui s'appuieront sur cette connexion une fois qu'elle existe.
                case "user": await OnGitHubUserClickedAsync(); break;
                case "org":
                    await DisplayAlert("Organisation", "Pas encore disponible.", "OK");
                    break;
                // ★ AJOUT (01/09, "changer de côté") : échange le dock IA et
                // l'explorateur/sidebar de côté (gauche↔droite) d'un coup — voir
                // ApplySidePanelLayout (MainPage.Panels.cs) pour le détail (colonnes
                // + poignées de redimensionnement). Remplace l'ancien message
                // "Pas encore disponible.".
                case "panellayout":
                    _panelsSwapped = !_panelsSwapped;
                    ApplySidePanelLayout();
                    StatusBar.SetStatus(_panelsSwapped
                        ? "🔀 Panneaux inversés : IA à droite, explorateur à gauche"
                        : "🔀 Panneaux rétablis : IA à gauche, explorateur à droite");
                    break;
                case "signout": await OnGitHubSignOutAsync(); break;
            }
        }

        /// <summary>
        /// ★ AJOUT (01/09) : "Utilisateur" — si déjà connecté, affiche qui ; sinon
        /// démarre le device flow (code affiché, navigateur ouvert, sondage en tâche
        /// de fond jusqu'à validation par Tom dans son navigateur).
        /// </summary>
        private async Task OnGitHubUserClickedAsync()
        {
            if (_gitHubAccount.IsConnected)
            {
                await DisplayAlert("Utilisateur", $"Connecté en tant que {_gitHubAccount.Username} sur GitHub.\n\nPublier/importer un projet et télécharger les mises à jour depuis le dépôt ne sont pas encore câblés — la connexion elle-même est la première brique.", "OK");
                return;
            }

            try
            {
                var info = await _gitHubAccount.StartDeviceFlowAsync();
                var openBrowser = await DisplayAlert("Connexion GitHub",
                    $"Code à saisir : {info.UserCode}\n\nOuvrir {info.VerificationUri} dans le navigateur pour le valider ?",
                    "Ouvrir le navigateur", "Annuler");
                if (!openBrowser) return;

                await Launcher.OpenAsync(info.VerificationUri);
                StatusBar.SetStatus("GitHub : en attente de validation dans le navigateur…");

                _gitHubConnectCts?.Cancel();
                _gitHubConnectCts = new CancellationTokenSource();
                var token = await _gitHubAccount.PollForTokenAsync(info, _gitHubConnectCts.Token);

                if (token is null)
                {
                    StatusBar.SetStatus("GitHub : connexion annulée ou expirée.");
                    return;
                }

                var username = await _gitHubAccount.FetchUsernameAsync(token);
                _gitHubAccount.SaveConnection(token, username);
                StatusBar.SetStatus($"GitHub : connecté en tant que {username}.");
                await DisplayAlert("Utilisateur", $"Connecté en tant que {username} sur GitHub.", "OK");
            }
            catch (Exception ex)
            {
                App.LogCrash("OnGitHubUserClickedAsync", ex);
                await DisplayAlert("Utilisateur", $"Échec de la connexion GitHub : {ex.Message}", "OK");
            }
        }

        /// <summary>★ AJOUT (01/09) : "Se déconnecter" — efface la connexion GitHub réelle.</summary>
        private async Task OnGitHubSignOutAsync()
        {
            if (!_gitHubAccount.IsConnected)
            {
                await DisplayAlert("Se déconnecter", "Aucun compte GitHub relié pour l'instant.", "OK");
                return;
            }

            _gitHubConnectCts?.Cancel();
            var username = _gitHubAccount.Username;
            _gitHubAccount.Disconnect();
            StatusBar.SetStatus("GitHub : déconnecté.");
            await DisplayAlert("Se déconnecter", $"Compte GitHub ({username}) déconnecté.", "OK");
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
        /// ★ CORRECTION (02/09) : "ai" pilote maintenant _aiChatPanel (vrai panneau
        /// de chat) au lieu du stub AiHost, supprimé.
        /// </summary>
        private void OnActivitySelected(string id)
        {
            if (id == "explorer") { ToggleSide(isExplorer: true); return; }

            bool showAi = id == "ai" && !_aiChatPanel.IsVisible;
            bool showCortex = id == "cortex" && !_cortexPanel.IsVisible;
            bool showCollab = id == "collab" && !CollabPanel.IsVisible;
            // ★ AJOUT (30/08, 2e passe) : "Recherche" ouvrait le bandeau IA sans
            // rapport — ouvre maintenant une vraie recherche de fichiers par nom
            // (voir SearchView.xaml.cs), sur le même patron que les autres panneaux.
            bool showSearch = id == "search" && !_searchPanel.IsVisible;

            _aiChatPanel.IsVisible = false;
            _cortexPanel.IsVisible = false;
            _neuralPanel.IsVisible = false;
            _workspacePanel.IsVisible = false;
            _pluginGallery.IsVisible = false;
            _analyticsDashboard.IsVisible = false;
            _searchPanel.IsVisible = false;
            CollabPanel.IsVisible = false;

            switch (id)
            {
                case "ai": _aiChatPanel.IsVisible = showAi; break;
                case "cortex":
                    _cortexPanel.IsVisible = showCortex;
                    if (showCortex && _viewModel.SelectedDocument != null)
                        _cortexPanel.LoadSuggestions(_viewModel.SelectedDocument.Path, _viewModel.SelectedDocument.Text);
                    break;
                case "collab": CollabPanel.IsVisible = showCollab; break;
                case "search": _searchPanel.IsVisible = showSearch; break;
                // ★ RETRAIT (31/08) : "settings" n'est plus émis par ActivityBarView —
                // l'engrenage a déménagé dans la barre de titre (point 1 de Tom), voir
                // OnMenuCommanded ("gear.toggle") / OnGearMenuItemSelected ("settings").
                case "gallery":
                    _pluginGallery.IsVisible = !_pluginGallery.IsVisible;
                    if (_pluginGallery.IsVisible) _pluginGallery.LoadGallery();
                    break;
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
            RefreshExplorerHandleVisibility();
        }

        // ------------------------------------------------------------------
        // Routeur des commandes IA (slash commands)
        // ------------------------------------------------------------------
        private async void OnAiCommandSubmitted(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // ★ AJOUT (03/09, jalon 1 — "agents autonomes en tâche de fond") :
            // interceptée ICI, EN PREMIER — bug réel trouvé en testant avec Tom :
            // "/agent crée un fichier hello.txt..." contient "crée" ET "projet",
            // les deux mots que AutoProjectBuilder.ShouldHandle (plus bas) utilise
            // pour détecter une demande de génération de projet complet. Sans cette
            // interception AVANT ce test, la commande se faisait entièrement
            // détourner vers AutoProjectBuilder (un vrai "MotoProject" générique
            // était créé sur le disque à la place). Ce chemin (Accueil/bandeau IA,
            // via HomePromptSubmitted/AiBar) est SÉPARÉ de HandlePluginCommandAsync
            // (ChatService.PluginCommandHandler, atteint uniquement depuis
            // SendAsync — donc depuis AiChatView) : les deux routent maintenant
            // vers la même HandleAgentCommand pour qu'/agent marche depuis
            // n'importe quelle surface de saisie.
            if (text.StartsWith("/agent ", StringComparison.OrdinalIgnoreCase))
            {
                var ack = HandleAgentCommand(text["/agent ".Length..].Trim());
                var thread = _chatService.CurrentThread ?? _chatService.CreateThread();
                thread.Messages.Add(new ChatMessage { Role = "ai", Content = ack });
                thread.LastActivityUtc = DateTime.UtcNow;
                // ★ CORRECTIF (04/09, trouvé en testant /diagnose avec Tom, latent
                // ici aussi) : voir ShowAiReplyAsTab plus bas — sans panneau
                // AiChatView ouvert, ce message n'apparaissait NULLE PART depuis ce
                // bandeau flottant. Masqué jusqu'ici pour /agent par la popup de
                // confirmation (retour visible bien réel, juste pour un autre
                // message) ; la narration pas-à-pas qui suit reste, elle, visible
                // seulement dans AiChatView/l'historique — pas corrigé ici (ferait
                // du fichier ouvert un composant "vivant", chantier séparé).
                ShowAiReplyAsTab(ack, "Agent");
                StatusBar.SetStatus("🤖 Agent démarré.");
                RefreshHomeStats();
                return;
            }

            // ★ AJOUT (04/09, agents de diagnostic) : même raison d'être que le
            // bloc /agent juste au-dessus — chemin Accueil/bandeau IA séparé de
            // HandlePluginCommandAsync (atteint uniquement depuis AiChatView).
            if (text.Equals("/diagnose", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("/diagnose ", StringComparison.OrdinalIgnoreCase))
            {
                var arg = text.Length > "/diagnose".Length ? text["/diagnose".Length..].Trim() : string.Empty;
                var report = await HandleDiagnoseCommandAsync(arg);
                var diagThread = _chatService.CurrentThread ?? _chatService.CreateThread();
                diagThread.Messages.Add(new ChatMessage { Role = "ai", Content = report });
                diagThread.LastActivityUtc = DateTime.UtcNow;
                // ★ CORRECTIF (04/09, bug réel trouvé avec Tom : "aucune réponse en
                // vue" bien que le statut affichait "Diagnostic terminé") : le
                // rapport n'était ajouté qu'à thread.Messages, jamais montré nulle
                // part tant qu'AiChatView n'est pas ouvert — /diagnose n'a AUCUNE
                // popup de confirmation pour compenser (rien n'est jamais mutant).
                // ShowAiReplyAsTab réutilise le mécanisme déjà existant et testé
                // pour une réponse IA normale depuis ce même bandeau (voir plus bas).
                ShowAiReplyAsTab(report, "Diagnostic");
                StatusBar.SetStatus("🔍 Diagnostic terminé.");
                RefreshHomeStats();
                return;
            }

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
            // ★ AJOUT (31/08) : AiBar.SetBusy(true) ne montre rien quand la demande
            // vient du prompt de l'Accueil (AiBar, le bandeau flottant, n'est alors pas
            // visible) — l'attente (parfois 1 min+ avec un modèle local) semblait donc
            // "figée"/anormale à Tom. La barre de statut, elle, est TOUJOURS visible.
            StatusBar.SetStatus("🧠 Réflexion de l'IA en cours…");
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
                // Tom ("ne génère rien du tout"). À l'époque, aucune vue de conversation
                // n'existait dans ce dépôt (ChatHost/ThreadHost, stubs jamais câblés,
                // supprimés le 02/09) ; en attendant, on ouvre la réponse comme un onglet
                // via le mécanisme d'ouverture de fichier déjà existant et testé
                // (_viewModel.OpenFilePath), plutôt que de construire un nouveau modèle
                // de "document en mémoire sans fichier" en urgence.
                // ★ NOTE (02/09) : un vrai panneau de chat (AiChatView) existe désormais
                // — ce bandeau IA flottant (OnAiCommandSubmitted) continue cependant
                // d'ouvrir sa réponse en onglet fichier plutôt que dans AiChatView ; les
                // deux surfaces restent indépendantes pour l'instant (pas retouché ici,
                // hors périmètre de ce correctif).
                var reply = _chatService.CurrentThread?.Messages?.LastOrDefault(m => m.Role == "ai")?.Content;
                App.Breadcrumb($"OnAiCommandSubmitted — reply longueur={reply?.Length ?? -1}");
                if (!string.IsNullOrWhiteSpace(reply))
                {
                    // ★ AJOUT (31/08) : si la réponse contient un bloc de code, on ouvre
                    // CE code (avec la bonne extension) plutôt que le message entier —
                    // repéré par Tom : "demander du code ne fonctionne pas, il se
                    // contente d'ouvrir un fichier" (le fichier s'ouvrait bel et bien,
                    // mais avec les explications de l'IA mélangées au code, à l'intérieur
                    // d'un simple .md — peu engageant, lu comme "ça ne marche pas").
                    var extracted = ExtractCodeBlockWithLanguage(reply);
                    var content = extracted?.Code ?? reply;
                    var extension = extracted?.Extension ?? ".md";

                    // ★ REFACTOR (04/09) : ce bloc écrivait/ouvrait/posait le texte en
                    // dur ici — factorisé dans ShowAiReplyAsTab pour que /agent et
                    // /diagnose (ajoutés le même jour) le réutilisent, plutôt que de
                    // dupliquer 4 lignes 3 fois. Voir ShowAiReplyAsTab pour le VRAI
                    // bug corrigé au passage (contrôle éditeur vide malgré un texte
                    // correct en mémoire, trouvé en testant /diagnose avec Tom).
                    ShowAiReplyAsTab(content, "Reponse-IA", extension);
                    StatusBar.SetStatus(extracted != null ? "✔ Code généré." : "✔ Réponse IA générée.");
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

        /// <summary>★ AJOUT (04/09) : extrait de la logique déjà existante plus
        /// haut ("Réponse IA générée" — repérée par Tom le 30/08, "ne génère
        /// rien du tout") pour que /agent et /diagnose puissent la réutiliser
        /// telle quelle, au lieu de se contenter d'ajouter à thread.Messages
        /// (invisible tant qu'AiChatView n'est pas ouvert). Ouvre `content`
        /// comme un onglet fichier temporaire — le même mécanisme déjà
        /// existant et testé pour une réponse IA normale depuis ce bandeau.</summary>
        private void ShowAiReplyAsTab(string content, string filePrefix, string extension = ".md")
        {
            var repliesDir = Path.Combine(Path.GetTempPath(), "MotoEditor-Reponses-IA");
            Directory.CreateDirectory(repliesDir);
            var replyPath = Path.Combine(repliesDir, $"{filePrefix}-{DateTime.Now:yyyyMMdd-HHmmss}{extension}");
            File.WriteAllText(replyPath, content);
            _viewModel.OpenFilePath(replyPath);
            if (_viewModel.SelectedDocument != null)
            {
                _viewModel.SelectedDocument.Text = content;
                // ★ CORRECTIF (04/09, bug réel trouvé en testant /diagnose avec Tom :
                // l'onglet s'ouvrait bien, mais restait VIDE malgré un vrai rapport en
                // mémoire). Cause : OpenFilePath crée le document avec Text="" puis
                // l'assigne à SelectedDocument — cette assignation déclenche
                // SYNCHRONEMENT LoadDocumentIntoEditor (voir WireEditorPane) qui copie
                // ce texte encore vide dans le contrôle éditeur visible. La ligne
                // au-dessus corrige le MODÈLE (EditorDocument.Text) mais rien ne
                // redéclenche l'affichage. Un second appel explicite ici force le
                // contrôle éditeur à se resynchroniser avec le texte désormais correct.
                // Concerne aussi la réponse IA "normale" (plus haut dans ce fichier),
                // qui utilise maintenant ShowAiReplyAsTab et hérite du correctif.
                LoadDocumentIntoEditor(_viewModel.SelectedDocument);
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
