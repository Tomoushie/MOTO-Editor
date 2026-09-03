// Moto.Editor/Models/ChatTaskRecord.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Moto.Editor.Models
{
    /// <summary>
    /// Suivi d'un vrai appel IA en cours ou terminé (ChatService.SendAsync /
    /// AskWithCodeAsync) — pour le panneau "Tâches en arrière-plan" RÉEL, pas
    /// une simulation. Volontairement plus simple que le multi-agent de
    /// Claude Code (phases/agents) : MOTO n'a aujourd'hui aucun sous-agent
    /// réellement invoqué depuis l'UI (AgentOrchestratorV3/
    /// SpecializedAgentRegistry existent mais ne sont appelés par rien —
    /// voir CLAUDE.md, section dédiée) — un "task" ici correspond exactement
    /// à un aller-retour vers Ollama/un provider externe, ni plus ni moins.
    /// </summary>
    public sealed class ChatTaskRecord : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void Notify([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>Court libellé : titre du thread, ou contexte (ex. "Bandeau IA (code)").</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>Modèle/provider effectivement utilisé pour CET appel.</summary>
        public string Model { get; init; } = string.Empty;

        public DateTime StartedUtc { get; init; } = DateTime.UtcNow;

        private DateTime? _endedUtc;
        public DateTime? EndedUtc
        {
            get => _endedUtc;
            set { _endedUtc = value; Notify(); Notify(nameof(IsRunning)); Notify(nameof(DurationLabel)); Notify(nameof(StatusIcon)); }
        }

        public bool IsRunning => _endedUtc is null;

        private bool _failed;
        public bool Failed
        {
            get => _failed;
            set { _failed = value; Notify(); Notify(nameof(StatusIcon)); }
        }

        /// <summary>Pastille d'état simple (pas d'icône vectorielle dédiée pour une 1re passe).</summary>
        public string StatusIcon => IsRunning ? "◔" : (Failed ? "✕" : "✓");

        /// <summary>Recalculée à la demande — appeler Tick() depuis un minuteur UI
        /// pendant que IsRunning est vrai pour un affichage qui avance en direct.</summary>
        public string DurationLabel
        {
            get
            {
                var elapsed = (_endedUtc ?? DateTime.UtcNow) - StartedUtc;
                return elapsed.TotalMinutes >= 1
                    ? $"{(int)elapsed.TotalMinutes}min {elapsed.Seconds:D2}s"
                    : $"{Math.Max(0, elapsed.Seconds)}s";
            }
        }

        /// <summary>Force le recalcul de DurationLabel — appelé par le minuteur du panneau.</summary>
        public void Tick()
        {
            if (IsRunning) Notify(nameof(DurationLabel));
        }
    }
}
