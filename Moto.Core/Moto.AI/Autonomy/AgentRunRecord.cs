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
using Moto.Core.AI.Autonomy.V2;

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
        // ★ MODIFIÉ (24/09, agent v2) : « init » → « set » notifié. Un pas d'écriture v2 est créé au moment de l'appel d'outil
        // (« edit_file Foo.cs »), puis précisé quand la modification est proposée (« modifier Foo.cs (+3 −1) »).
        private string _summary = string.Empty;
        public string Summary
        {
            get => _summary;
            set { _summary = value; Notify(); }
        }

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
                Notify(nameof(StatusIcon));
                Notify(nameof(StatusLabel));
                Notify(nameof(IsActive));
                Notify(nameof(Activity));
                Notify(nameof(HasActivity));
                NotifyResult();
            }
        }

        // ── AJOUT (jalon 3, panneau "Agents en cours") : mêmes idiomes que
        // ChatTaskRecord (Moto.Editor/Models/ChatTaskRecord.cs) — StatusIcon/
        // DurationLabel/Tick() — pour qu'AgentRunsView n'ait aucun nouveau
        // patron de binding à apprendre. ──

        public string StatusIcon => Status switch
        {
            AgentRunStatus.Running => "◔",
            AgentRunStatus.AwaitingConfirmation => "⏸",
            AgentRunStatus.Completed => "✓",
            AgentRunStatus.Failed => "✕",
            AgentRunStatus.Cancelled => "⏹",
            AgentRunStatus.StepLimitReached => "⏱",
            _ => "?"
        };

        /// <summary>★ CORRECTIF (jalon 3, trouvé en testant avec Tom) : AgentRunsView
        /// affichait Status.ToString() brut ("Running"/"Completed"...) — seul
        /// endroit anglais au milieu d'une interface entièrement française.</summary>
        public string StatusLabel => Status switch
        {
            AgentRunStatus.Running => "En cours",
            AgentRunStatus.AwaitingConfirmation => "En attente de confirmation",
            AgentRunStatus.Completed => "Terminé",
            AgentRunStatus.Failed => "Échoué",
            AgentRunStatus.Cancelled => "Annulé",
            AgentRunStatus.StepLimitReached => "Limite atteinte",
            _ => "?"
        };

        /// <summary>Vrai tant que le run peut encore faire quelque chose — pilote
        /// la visibilité du bouton "Arrêter" dans AgentRunsView.</summary>
        public bool IsActive => Status is AgentRunStatus.Running or AgentRunStatus.AwaitingConfirmation;

        // ── AJOUT (24/09, agent v2) : ce que le run a produit, pour le panneau « Agents en cours ».
        // Toutes ces propriétés sont écrites sur le thread UI (voir AgentV2Runner), comme Status/Steps. ──

        /// <summary>« v1 » (ancienne boucle texte) ou « v2 » (appels d'outils, diff, annulation).</summary>
        public string Engine { get; internal set; } = "v1";

        /// <summary>Le modèle qui exécute (ou a exécuté) le run — connu dès que le run a choisi le sien.</summary>
        public string Model { get; private set; } = string.Empty;

        internal void SetModel(string model)
        {
            Model = model;
            Notify(nameof(Model));
        }

        /// <summary>Résumé du modèle à la fin du run (le texte de « finish »), ou la raison de l'arrêt.</summary>
        public string Summary { get; private set; } = string.Empty;

        /// <summary>Avertissement à montrer (ex. « le run se dit terminé mais rien n'a changé », ou une annulation incomplète).</summary>
        public string? Warning { get; private set; }

        /// <summary>Fichiers réellement modifiés par ce run (chemins relatifs au projet).</summary>
        public IReadOnlyList<ChangedFile> ChangedFiles { get; private set; } = Array.Empty<ChangedFile>();

        /// <summary>Copies des originaux avant modification : permet « Annuler les modifications de cette exécution ».</summary>
        internal RunBackup? Backup { get; private set; }

        public bool IsUndone { get; private set; }

        public bool CanUndo => Backup is not null && ChangedFiles.Count > 0 && !IsUndone && !IsActive;

        /// <summary>Une ligne pour le panneau : combien de fichiers ont changé, avec quel modèle.</summary>
        public string ResultLine
        {
            get
            {
                if (Engine != "v2" || IsActive) return string.Empty;

                var parts = new List<string>();
                if (IsUndone)
                {
                    parts.Add("modifications annulées");
                }
                else if (ChangedFiles.Count > 0)
                {
                    var added = 0;
                    var removed = 0;
                    foreach (var f in ChangedFiles) { added += f.Added; removed += f.Removed; }
                    parts.Add($"{ChangedFiles.Count} fichier(s) modifié(s) (+{added} −{removed})");
                }
                else
                {
                    parts.Add("aucun fichier modifié");
                }
                if (Model.Length > 0) parts.Add(Model);
                return string.Join(" · ", parts);
            }
        }

        // Les « Has… » servent de visibilité dans le panneau (MAUI n'a pas de convertisseur texte → booléen d'origine).
        public bool HasResultLine => ResultLine.Length > 0;

        /// <summary>Le résumé n'est montré qu'une fois le run fini : pendant, il serait vide ou périmé.</summary>
        public bool HasSummary => !IsActive && !string.IsNullOrWhiteSpace(Summary);

        public bool HasWarning => !IsActive && !IsUndone && !string.IsNullOrWhiteSpace(Warning);

        private string _activity = string.Empty;

        /// <summary>Ce que l'agent fait en ce moment (« Étape 3 · lit Foo.cs ») ; vide dès que le run est fini.</summary>
        public string Activity => IsActive ? _activity : string.Empty;

        public bool HasActivity => Activity.Length > 0;

        internal void SetActivity(string text)
        {
            _activity = text;
            Notify(nameof(Activity));
            Notify(nameof(HasActivity));
        }

        /// <summary>
        /// Fichiers de ce run (chemins relatifs au projet) modifiés — ou réenregistrés — depuis la fin du run : « Annuler » les
        /// remettrait dans l'état d'avant le run et perdrait ce travail. Vide si rien n'a bougé.
        /// </summary>
        public IReadOnlyList<string> FilesEditedSinceRun()
        {
            if (Backup is null || IsUndone) return Array.Empty<string>();
            return Backup.ChangedSinceSeal().Select(Backup.RelativePath).ToList();
        }

        /// <summary>Renseigne le résultat d'un run v2 (thread UI). À appeler AVANT de fixer le statut final.</summary>
        internal void SetResult(AgentRunResult result, string model)
        {
            Model = model;
            Summary = result.Summary;
            Warning = result.Warning;
            ChangedFiles = result.Changes;
            Backup = result.Backup;
            NotifyResult();
        }

        internal void SetFailure(string message, string model)
        {
            Model = model;
            Summary = message;
            NotifyResult();
        }

        /// <summary>
        /// Remet chaque fichier touché dans son état d'avant le run. Retourne les chemins complets concernés
        /// (pour que l'éditeur recharge ses onglets) ; <paramref name="restored"/> = nombre de fichiers restaurés ou supprimés.
        /// </summary>
        internal IReadOnlyList<string> UndoChanges(out int restored)
        {
            restored = 0;
            if (!CanUndo || Backup is null) return Array.Empty<string>();

            var touched = Backup.TouchedFiles.ToList();
            restored = Backup.Restore(out var failed);

            if (failed.Count == 0)
            {
                IsUndone = true;
                Warning = null; // ni l'avertissement du run ni un échec d'annulation précédent ne concernent plus rien
            }
            else
            {
                // Un fichier verrouillé ou dont la copie d'origine a disparu : le run n'est PAS déclaré annulé, le bouton reste (réessayer
                // est sans risque) et le panneau dit lesquels.
                var names = string.Join(", ", failed.Take(5).Select(Backup.RelativePath)) + (failed.Count > 5 ? "…" : string.Empty);
                Warning = $"Annulation incomplète : {failed.Count} fichier(s) n'ont pas pu être remis en place ({names}). "
                          + "Ferme les programmes qui les utilisent, puis réessaie.";
            }

            NotifyResult();
            return touched;
        }

        private void NotifyResult()
        {
            Notify(nameof(Model));
            Notify(nameof(Summary));
            Notify(nameof(Warning));
            Notify(nameof(ChangedFiles));
            Notify(nameof(IsUndone));
            Notify(nameof(CanUndo));
            Notify(nameof(ResultLine));
            Notify(nameof(HasResultLine));
            Notify(nameof(HasSummary));
            Notify(nameof(HasWarning));
        }

        /// <summary>Recalculée à la demande — appeler Tick() depuis un minuteur UI
        /// pendant que IsActive est vrai pour un affichage qui avance en direct.</summary>
        public string DurationLabel
        {
            get
            {
                var elapsed = (EndedUtc ?? DateTime.UtcNow) - StartedUtc;
                return elapsed.TotalMinutes >= 1
                    ? $"{(int)elapsed.TotalMinutes}min {elapsed.Seconds:D2}s"
                    : $"{Math.Max(0, elapsed.Seconds)}s";
            }
        }

        /// <summary>Force le recalcul de DurationLabel — appelé par le minuteur du panneau.</summary>
        public void Tick()
        {
            if (IsActive) Notify(nameof(DurationLabel));
        }
    }
}
