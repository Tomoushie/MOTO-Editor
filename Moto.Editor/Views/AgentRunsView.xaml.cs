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

        /// <summary>
        /// ★ AJOUT (24/09, agent v2) : pour des chemins relatifs au projet, une phrase sur ceux qui sont ouverts dans l'éditeur avec des
        /// modifications non enregistrées (null si aucun) — « Annuler les modifications » les remplacerait. Fourni par la page principale.
        /// </summary>
        public Func<IReadOnlyList<string>, string?>? UnsavedEditsCheck { get; init; }

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

            // Ce plafond ne compte que les appels de l'ancien moteur (v1) : avec la v2, un « 0/200 » affiché en permanence
            // laisserait croire qu'il y a une limite qui protège — la v2 a les siennes (étapes, durée, refus, boucle).
            BudgetLabel.IsVisible = !_agents.UsesV2 || _agents.GlobalBudget.Consumed > 0;
        }

        /// <summary>Arrête un run précis — CommandParameter porte son Guid (voir
        /// AgentRunsView.xaml, binding sur AgentRunRecord.Id).</summary>
        private void OnCancelRunClicked(object? sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is Guid runId)
                _agents.Cancel(runId);
        }

        /// <summary>
        /// ★ AJOUT (24/09, agent v2) : remet les fichiers d'un run dans l'état d'avant lui. Toujours après une confirmation qui liste
        /// les fichiers et prévient si l'utilisateur a travaillé dessus depuis (sur disque, ou dans un onglet non enregistré) —
        /// l'annulation écraserait ce travail.
        /// </summary>
        private async void OnUndoRunClicked(object? sender, EventArgs e)
        {
            if (sender is not Button { CommandParameter: Guid runId }) return;
            var run = _agents.Runs.FirstOrDefault(r => r.Id == runId);
            if (run is null || !run.CanUndo) return;

            try
            {
                // Ce panneau vit dans SA fenêtre : la boîte de confirmation générale de l'éditeur, elle, s'ouvre dans la fenêtre
                // principale (parfois cachée derrière celle-ci) et fait la queue derrière les demandes d'un agent en cours. L'annulation
                // est un geste de l'utilisateur, pas une demande de l'IA : on la confirme donc ici, dans cette fenêtre.
                var page = Window?.Page;
                if (page is null) return;

                var files = run.ChangedFiles.Select(f => f.RelativePath).ToList();
                var confirmed = await page.DisplayAlert(
                    "↩ Annuler les modifications de l'agent",
                    $"Remettre {files.Count} fichier(s) dans l'état d'avant « {run.AgentId} » ?\n\n{DescribeUndo(run, UnsavedEditsCheck?.Invoke(files))}",
                    "Annuler les modifications",
                    "Garder");
                if (!confirmed) return;

                // Le run a pu changer pendant que la boîte était ouverte (déjà annulé ailleurs…) : UndoChanges ne fait alors rien.
                _agents.UndoChanges(runId, out _);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AgentRunsView] Annulation en échec : {ex.Message}");
            }
        }

        private static string DescribeUndo(AgentRunRecord run, string? unsavedInEditor)
        {
            var lines = new List<string> { "Fichiers concernés :" };
            foreach (var f in run.ChangedFiles.Take(12))
                lines.Add(f.Created ? $"  • {f.RelativePath} (créé par l'agent : sera supprimé)" : $"  • {f.RelativePath} (remis comme avant l'agent)");
            if (run.ChangedFiles.Count > 12) lines.Add($"  • … et {run.ChangedFiles.Count - 12} autre(s)");

            var edited = run.FilesEditedSinceRun();
            if (edited.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add($"⚠ Modifiés depuis la fin de l'agent : {string.Join(", ", edited.Take(5))}{(edited.Count > 5 ? "…" : string.Empty)}. "
                          + "Annuler les remettra comme avant l'agent : ton travail sur ces fichiers sera perdu.");
            }
            if (!string.IsNullOrWhiteSpace(unsavedInEditor))
            {
                lines.Add(string.Empty);
                lines.Add(unsavedInEditor.Trim());
            }
            return string.Join("\n", lines);
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
