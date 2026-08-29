// Moto.Editor/Views/HomeView.xaml.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.AI.Cortex;
using Moto.Core.Chat;
using Moto.Core.Settings;

namespace Moto.Editor.Views
{
    /// <summary>
    /// Écran d'accueil (mix Claude Code / ChatGPT) :
    /// greeting personnalisé, stats d'usage, input central, gestion du drag & drop des sessions.
    /// </summary>
    public partial class HomeView : ContentView
    {
        private readonly ChatService _chatService;
        private readonly CortexEngine _cortexEngine;
        private readonly WorkspaceStateService _workspaceState;
        private CancellationTokenSource _refreshStatsCts;
        private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(150);

        /// <summary>Prompt saisi depuis l'accueil → routé vers le chat IA.</summary>
        public event Action<string>? HomePromptSubmitted;

        /// <summary>Notifie le parent qu'une session a changé de section.</summary>
        public event Action<string, string>? SessionMoved;

        /// <summary>
        /// Le constructeur reçoit les services via Injection de Dépendances (DI).
        /// </summary>
        public HomeView(ChatService chatService, CortexEngine cortexEngine, WorkspaceStateService workspaceState)
        {
            InitializeComponent();
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _cortexEngine = cortexEngine ?? throw new ArgumentNullException(nameof(cortexEngine));
            _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
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
                var cortex = _cortexEngine.GetStats();

                var values = new[]
                {
                    threads.Count.ToString(),
                    cortex.TotalHabits.ToString(),
                    cortex.TotalPatterns.ToString(),
                    cortex.TotalCorrections.ToString(),
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
    }
}
