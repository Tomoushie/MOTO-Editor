using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.AI.Cortex;
using Moto.Core.Settings;
using Moto.Editor.Controls;
using Moto.Editor.Services;

namespace Moto.Editor.Views
{
    /// <summary>
    /// Écran d'accueil (mix Claude Code / ChatGPT) :
    /// greeting personnalisé, stats d'usage, input central, gestion du drag & drop des sessions.
    /// </summary>
    public partial class HomeView : ContentView
    {
        private readonly ChatService _chatService;
        private CortexEngine? _cortexEngine;
        private WorkspaceStateService? _workspaceState;
        private CancellationTokenSource _refreshStatsCts;
        private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(150);

        /// <summary>Prompt saisi depuis l'accueil → routé vers le chat IA.</summary>
        public event Action<string>? HomePromptSubmitted;

        /// <summary>Notifie le parent qu'une session a changé de section.</summary>
        public event Action<string, string>? SessionMoved;

        /// <summary>Événement déclenché lorsqu'une chip '💻 Local' est tapée.</summary>
        public event Action? LocalChipTapped;

        /// <summary>
        /// ★ AJOUT (31/08, point 1 de Tom) : le menu "Emplacement d'exécution" a
        /// déménagé ICI (directement au-dessus de la chip "Local", façon Claude
        /// Code) — auparavant ancré en haut à droite de toute la fenêtre par
        /// MainPage. MainPage n'a plus besoin de connaître LocationMenu.IsVisible,
        /// seulement le résultat du choix (relayé par cet événement).
        /// </summary>
        public event Action<string>? LocationSelected;

        /// <summary>
        /// ★ AJOUT (31/08, point 5 de Tom) : "IA"/"Cortex" de la barre du bas
        /// (ComposerBar) — relayé tel quel, MainPage route vers OnActivitySelected
        /// (exactement comme les items équivalents de la barre du haut, retirés de
        /// là pour ne pas être dupliqués).
        /// </summary>
        public event Action<string>? ComposerPanelRequested;

        /// <summary>Événement déclenché lorsqu'une chip '📁 Projet logiciel' est tapée.</summary>
        public event Action? ProjectChipTapped;

        /// <summary>
        /// Le constructeur reçoit les services via Injection de Dépendances (DI).
        /// cortexEngine/workspaceState sont nullables : au premier lancement (aucun
        /// workspace encore ouvert), MainPage construit Home avant que ces moteurs
        /// existent — voir MainPage.SetCoreServices / RebindHome.
        /// </summary>
        public HomeView(ChatService chatService, CortexEngine? cortexEngine = null, WorkspaceStateService? workspaceState = null)
        {
            InitializeComponent();
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _cortexEngine = cortexEngine;
            _workspaceState = workspaceState;
            LocationMenu.LocationSelected += id => LocationSelected?.Invoke(id);
            ComposerBar.PanelRequested += id => ComposerPanelRequested?.Invoke(id);
            // ★ AJOUT (31/08) : "+" de la barre du bas — pas de vrai système de pièces
            // jointes dans MOTO Editor, relayé vers la même action que "Rechercher
            // projet" (sélecteur de dossier) plutôt qu'un bouton qui ne ferait rien.
            ComposerBar.AttachRequested += () => ProjectChipTapped?.Invoke();

            // ★ AJOUT (31/08, point 2) : fond gris arrondi + léger agrandissement au
            // survol sur la flèche d'envoi (elle était un Button nu, sans retour visuel).
            HoverEffects.Attach(SendArrowHost);
            var hoverIn = new PointerGestureRecognizer();
            hoverIn.PointerEntered += (_, _) => SendArrowLabel.ScaleTo(1.15, 120, Easing.CubicOut);
            hoverIn.PointerExited += (_, _) => SendArrowLabel.ScaleTo(1.0, 120, Easing.CubicIn);
            SendArrowHost.GestureRecognizers.Add(hoverIn);
        }

        /// <summary>★ AJOUT (31/08, point 2) : la flèche d'envoi est un Border+Label
        /// (survol/animation), pas un Button — TapGestureRecognizer.Tapped passe un
        /// TappedEventArgs, transmis tel quel à OnPromptSubmitted (qui n'utilise pas e).</summary>
        private void OnSendArrowTapped(object sender, TappedEventArgs e) => OnPromptSubmitted(sender, e);

        /// <summary>Rebranche les moteurs une fois un workspace ouvert (LoadWorkspace).</summary>
        public void SetCoreServices(CortexEngine? cortexEngine, WorkspaceStateService? workspaceState)
        {
            _cortexEngine = cortexEngine;
            _workspaceState = workspaceState;
        }

        /// <summary>Remplit la grille de stats (8 tuiles max).</summary>
        public void SetStats(string[] values, string[] titles)
        {
            if (StatsGrid == null) return;
            StatsGrid.Children.Clear();

            for (int i = 0; i < Math.Min(values.Length, 8); i++)
            {
                var cell = new VerticalStackLayout { Spacing = 2 };
                cell.Children.Add(new Label
                {
                    Text = values[i],
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = (Color)Application.Current.Resources["Txt1"]
                });
                cell.Children.Add(new Label
                {
                    Text = titles[i],
                    FontSize = 10,
                    TextColor = (Color)Application.Current.Resources["Txt2"]
                });

                var col = i % 4;
                var row = i / 4;
                StatsGrid.Children.Add(cell);
                Grid.SetColumn(cell, col);
                Grid.SetRow(cell, row);
            }
        }

        /// <summary>Attache les gestionnaires VSM pour un effet hover fluide sur les Border/Chips.</summary>
        public void AttachChipHover(Border chip)
        {
            if (chip == null) return;
            var pointer = new PointerGestureRecognizer();
            pointer.PointerEntered += (_, _) => VisualStateManager.GoToState(chip, "PointerOver");
            pointer.PointerExited += (_, _) => VisualStateManager.GoToState(chip, "Normal");
            pointer.PointerPressed += (_, _) => VisualStateManager.GoToState(chip, "Pressed");
            pointer.PointerReleased += (_, _) => VisualStateManager.GoToState(chip, "PointerOver");
            chip.GestureRecognizers.Add(pointer);
        }

        private void OnPromptSubmitted(object sender, EventArgs e)
        {
            var text = PromptEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            PromptEntry.Text = string.Empty;
            HomePromptSubmitted?.Invoke(text);
        }

        private void OnAttachClicked(object sender, EventArgs e)
        {
            // Placeholder : joindre un fichier au prompt
        }

        /// <summary>
        /// Gère la persistance et la mise à jour après un drop de session.
        /// Debounce de 100ms pour éviter les écritures en rafale.
        /// </summary>
        private async void OnSessionDropped(string sessionId, string targetSectionKey)
        {
            var section = targetSectionKey switch
            {
                "pinned" => SessionSection.Pinned,
                "projects" => SessionSection.Projects,
                _ => SessionSection.Recent
            };

            // Debounce déjà géré dans WorkspaceStateService.SetSessionSectionAsync
            if (_workspaceState != null)
                await _workspaceState.SetSessionSectionAsync(sessionId, section);

            // Refresh stats avec debounce
            await DebouncedRefreshHomeStatsAsync();

            SessionMoved?.Invoke(sessionId, targetSectionKey);
        }

        /// <summary>
        /// Refresh des stats avec debounce de 150ms pour éviter les appels en rafale.
        /// </summary>
        private async Task DebouncedRefreshHomeStatsAsync()
        {
            _refreshStatsCts?.Cancel();
            _refreshStatsCts = new CancellationTokenSource();
            var localCts = _refreshStatsCts;

            try
            {
                await Task.Delay(_debounceDelay, localCts.Token);
                await RefreshHomeStatsAsync();
            }
            catch (OperationCanceledException)
            {
                // Un nouvel appel a annulé celui-ci
            }
        }

        /// <summary>
        /// Récupère les données des services et met à jour l'UI des stats.
        /// </summary>
        public async Task RefreshHomeStatsAsync()
        {
            try
            {
                var threads = _chatService.Threads;
                var cortex = _cortexEngine?.GetStats();

                var values = new[]
                {
                    threads.Count.ToString(),
                    (cortex?.TotalHabits ?? 0).ToString(),
                    (cortex?.TotalPatterns ?? 0).ToString(),
                    (cortex?.TotalCorrections ?? 0).ToString(),
                    "0", "0", "0", "0" // Padding pour maintenir la grille 4x2
                };
                var titles = new[]
                {
                    "Threads", "Habits", "Patterns", "Corrections",
                    "Réservé", "Réservé", "Réservé", "Réservé"
                };

                SetStats(values, titles);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HomeView] Échec RefreshHomeStats: {ex.Message}");
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Gestionnaire d'événement pour la chip '💻 Local'.
        /// </summary>
        private void OnLocalChipTapped(object sender, EventArgs e)
        {
            // ★ CORRECTION (01/09) : LocationMenu ne bascule plus IsVisible — elle
            // reste "montée"/mesurée en permanence, l'ouverture/fermeture se fait via
            // Opacity + InputTransparent. La VRAIE cause du bug ("Local" n'ouvrait
            // rien) était ailleurs (voir HomeView.xaml : la VerticalStackLayout qui
            // héberge chips/saisie/barre IA rognait tout débordement vers le haut au-
            // delà d'un certain seuil — LocationMenu en est sortie, promue "sœur" au
            // lieu d'être nichée dedans), mais ce réglage Opacity reste une bonne
            // pratique à part entière : plusieurs vraies régressions MAUI documentées
            // (dotnet/maui#9850/#28677/#8185) touchent les contrôles qui démarrent
            // IsVisible=false, alors qu'Opacity n'a pas ce défaut.
            bool willShow = LocationMenu.Opacity == 0;
            LocationMenu.Opacity = willShow ? 1 : 0;
            LocationMenu.InputTransparent = !willShow;
            LocalChipTapped?.Invoke();
        }

        /// <summary>
        /// Gestionnaire d'événement pour la chip '📁 Projet logiciel'.
        /// </summary>
        private void OnProjectChipTapped(object sender, EventArgs e)
        {
            ProjectChipTapped?.Invoke();
        }
    }
}