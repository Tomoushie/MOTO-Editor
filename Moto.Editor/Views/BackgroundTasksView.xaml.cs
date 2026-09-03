// Moto.Editor/Views/BackgroundTasksView.xaml.cs
using System.Linq;
using Microsoft.Maui.Controls;
using Moto.Editor.Models;
using Moto.Editor.Services;

namespace Moto.Editor.Views
{
    /// <summary>
    /// Panneau "Tâches en arrière-plan" réel : liste les appels IA en cours et
    /// récemment terminés (ChatService.Tasks). Distinct de la démo visuelle du
    /// même nom dans ClaudeShellView (données factices, phases/agents qui
    /// n'existent pas réellement dans MOTO — voir CLAUDE.md).
    /// </summary>
    public partial class BackgroundTasksView : ContentView
    {
        private readonly ChatService _chat;

        public BackgroundTasksView(ChatService chat)
        {
            InitializeComponent();
            _chat = chat;
            TaskList.ItemsSource = _chat.Tasks;
            _chat.Tasks.CollectionChanged += (_, _) => RefreshCounts();
            RefreshCounts();

            // Minuteur d'affichage (durée des tâches en cours) — tourne tant que le
            // panneau existe ; une nouvelle instance est recréée à chaque ouverture
            // (même limitation déjà documentée pour les autres fenêtres spécialisées
            // de cette session, voir WindowManager.OpenOrFocus).
            this.Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                foreach (var task in _chat.Tasks) task.Tick();
                // ★ CORRECTIF (03/09, trouvé en testant) : RefreshCounts() n'était
                // appelée que sur CollectionChanged (ajout/retrait), pas quand une
                // tâche EXISTANTE passe de "en cours" à "terminée" (EndedUtc change
                // sur l'objet, pas la collection) — l'en-tête restait bloqué sur
                // "1 en cours" après la fin réelle de l'appel. Rattrapé ici, au même
                // rythme que le tic des durées (décalage max ~1s, acceptable).
                RefreshCounts();
                return true;
            });
        }

        private void RefreshCounts()
        {
            var running = _chat.Tasks.Count(t => t.IsRunning);
            var done = _chat.Tasks.Count - running;
            RunningCountLabel.Text = running == 0 ? "0 en cours" : $"{running} en cours";
            TotalCountLabel.Text = $"{done} terminée(s)";
        }

        /// <summary>Retire les tâches terminées de l'historique (garde celles en cours).</summary>
        private void OnClearClicked(object? sender, EventArgs e)
        {
            for (var i = _chat.Tasks.Count - 1; i >= 0; i--)
            {
                if (!_chat.Tasks[i].IsRunning) _chat.Tasks.RemoveAt(i);
            }
        }
    }
}
