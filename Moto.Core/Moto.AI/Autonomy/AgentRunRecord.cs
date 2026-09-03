// Moto.Core/AI/Autonomy/AgentRunRecord.cs
// État d'exécution d'un run, exposé en direct (ObservableCollection/
// INotifyPropertyChanged) — même convention que ChatTaskRecord
// (Moto.Editor/Models/ChatTaskRecord.cs) pour qu'un futur panneau "Agents en
// cours" (jalon 3) n'ait aucun nouvel idiome de binding à apprendre.
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Moto.Core.AI.Autonomy
{
    public enum AgentRunStatus
    {
        Running,
        AwaitingConfirmation,
        Completed,
        Failed,
        Cancelled,
        StepLimitReached
    }

    public enum ConfirmationState
    {
        NotRequired,
        Pending,
        Approved,
        Declined
    }

    public sealed class AgentStepRecord : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public int Index { get; init; }
        public AgentActionKind ActionKind { get; init; }
        public string Summary { get; init; } = string.Empty;
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        private ConfirmationState _confirmation = ConfirmationState.NotRequired;
        public ConfirmationState Confirmation
        {
            get => _confirmation;
            set { _confirmation = value; Notify(); }
        }

        private string? _observationSummary;
        public string? ObservationSummary
        {
            get => _observationSummary;
            set { _observationSummary = value; Notify(); }
        }
    }

    public sealed class AgentRunRecord : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public Guid Id { get; } = Guid.NewGuid();
        public string AgentId { get; init; } = "agent-1";
        public string Goal { get; init; } = string.Empty;
        public DateTime StartedUtc { get; } = DateTime.UtcNow;
        public DateTime? EndedUtc { get; private set; }

        public ObservableCollection<AgentStepRecord> Steps { get; } = new();

        /// <summary>Jeton d'annulation du run — possédé par BackgroundAgentService,
        /// jamais créé ni annulé par le run lui-même.</summary>
        internal CancellationTokenSource Cts { get; } = new();

        private AgentRunStatus _status = AgentRunStatus.Running;
        public AgentRunStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                if (value is AgentRunStatus.Completed or AgentRunStatus.Failed
                    or AgentRunStatus.Cancelled or AgentRunStatus.StepLimitReached)
                {
                    EndedUtc = DateTime.UtcNow;
                    Notify(nameof(EndedUtc));
                }
                Notify();
            }
        }
    }
}
