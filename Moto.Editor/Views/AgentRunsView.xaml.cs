// Moto.Editor/Views/AgentRunsView.xaml.cs
// Panneau "Agents en cours" (jalon 3) : première vraie interface pour les
// agents autonomes en tâche de fond — même patron que BackgroundTasksView
// (CollectionView live + minuteur d'affichage), plus un aperçu des messages
// échangés entre agents (AgentMessageBus) et un accès direct au dossier des
// journaux NDJSON.
using System.Linq;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Autonomy;

namespace Moto.Editor.Views
{
    public partial class AgentRunsView : ContentView
    {
        private readonly BackgroundAgentService _agents;
        private readonly System.Collections.ObjectModel.ObservableCollection<MessageRow> _messageRows = new();

        public AgentRunsView(BackgroundAgentService agents)
        {
            InitializeComponent();
            _agents = agents;

            RunList.ItemsSource = _agents.Runs;
            _agents.Runs.CollectionChanged += (_, _) => RefreshCounts();
            RefreshCounts();

            // Historique déjà posté avant l'ouverture du panneau — lu une seule
            // fois ici, à la construction (donc déjà sur le thread UI).
            foreach (var m in _agents.MessageBus.History) _messageRows.Add(ToRow(m));
            MessageList.ItemsSource = _messageRows;
            // AgentMessageBus vit dans Moto.Core (portable, sans dépendance MAUI) —
            // il ne marshale jamais lui-même vers le thread UI. Depuis que
            // BackgroundAgentService.Start n'utilise plus Task.Run, l'agent qui
            // poste tourne déjà sur le thread UI dans le flux normal, mais on
            // marshale quand même explicitement ici : le contrat de l'événement
            // ne garantit rien sur l'appelant, et une seule ligne suffit à
            // rendre ce code correct dans tous les cas.
            _agents.MessageBus.MessagePosted += OnMessagePosted;

            RefreshBudget();

            // Minuteur d'affichage (durée des runs actifs) — même patron que
            // BackgroundTasksView.Tick(). Une nouvelle instance est recréée à
            // chaque ouverture du panneau (même limitation déjà documentée
            // ailleurs pour les fenêtres spécialisées, voir WindowManager.OpenOrFocus).
            Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                foreach (var run in _agents.Runs) run.Tick();
                RefreshCounts();
                RefreshBudget();
                return true;
            });
        }

        private void OnMessagePosted(AgentMessage message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _messageRows.Insert(0, ToRow(message));
                while (_messageRows.Count > 30) _messageRows.RemoveAt(_messageRows.Count - 1);
            });
        }

        private static MessageRow ToRow(AgentMessage m) => new()
        {
            Icon = m.Kind switch
            {
                AgentMessageKind.Note => "💬",
                AgentMessageKind.Proposal => "📝",
                AgentMessageKind.Outcome => "✅",
                _ => "•"
            },
            Label = $"{m.FromAgentId} → {m.ToAgentId ?? "tous"} : {m.Text}",
            TimeLabel = m.SentUtc.ToLocalTime().ToString("HH:mm:ss")
        };

        private void RefreshCounts()
        {
            var active = _agents.Runs.Count(r => r.IsActive);
            ActiveCountLabel.Text = active == 0 ? "0 actif(s)" : $"{active} actif(s)";
        }

        private void RefreshBudget()
        {
            BudgetLabel.Text = $"Budget IA : {_agents.GlobalBudget.Consumed}/{_agents.GlobalBudget.Limit}";
        }

        /// <summary>Arrête un run précis — CommandParameter porte son Guid (voir
        /// AgentRunsView.xaml, binding sur AgentRunRecord.Id).</summary>
        private void OnCancelRunClicked(object? sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is Guid runId)
                _agents.Cancel(runId);
        }

        /// <summary>Ouvre le dossier des journaux NDJSON dans l'explorateur — un
        /// dossier plutôt qu'un fichier précis : plusieurs workspaces peuvent
        /// avoir chacun leur propre fichier (voir AgentAuditLog.BaseFolder).</summary>
        private void OnOpenAuditFolderClicked(object? sender, EventArgs e)
        {
            try
            {
                System.IO.Directory.CreateDirectory(AgentAuditLog.BaseFolder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AgentAuditLog.BaseFolder)
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ouvrir l'explorateur est un confort, pas une fonctionnalité
                // critique — un échec (ex. sandbox) ne doit rien casser d'autre.
            }
        }

        private sealed class MessageRow
        {
            public string Icon { get; init; } = "•";
            public string Label { get; init; } = string.Empty;
            public string TimeLabel { get; init; } = string.Empty;
        }
    }
}
