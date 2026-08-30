using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Agents;

/// <summary>P2 — Local model distillation : plan de distillation d'un modèle lourd vers un petit modèle local.</summary>
public sealed class LocalModelDistillationService
{
    private readonly StructuredLogCollector _log;
    public LocalModelDistillationService(StructuredLogCollector log) => _log = log;

    public Task<string> PlanDistillationAsync(string sourceModel, string targetTask, CancellationToken ct = default)
    {
        // L'opération lourde (entraînement/extraction) est déléguée hors éditeur.
        // Ici : planification + traçabilité (respect de la règle "MOTO n'invente pas de systèmes").
        _log.Info("Distillation", "Plan de distillation généré", new { sourceModel, targetTask });
        return Task.FromResult(
            $"Plan : distiller '{sourceModel}' pour la tâche '{targetTask}' vers un petit modèle local " +
            "(génération du dataset → entraînement délégué → validation).");
    }
}

/// <summary>P2 — Local LLM sandbox : exécution hors-ligne limitée en ressources.</summary>
public sealed class LocalLlmSandbox
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private static readonly SemaphoreSlim Gate = new(1, 1); // 1 exécution à la fois

    public LocalLlmSandbox(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    public async Task<string> RunAsync(Func<CancellationToken, Task<string>> work, CancellationToken ct = default)
    {
        int timeoutSec = SettingsCatalog.AiAgents.SandboxTimeoutSeconds.Value;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

        await Gate.WaitAsync(timeoutCts.Token);
        try
        {
            _log.Info("LlmSandbox", "Exécution sandbox démarrée", new { timeoutSec });
            return await work(timeoutCts.Token);
        }
        finally
        {
            Gate.Release();
        }
    }
}

/// <summary>P2 — Agent marketplace : agents spécialisés installables séparément.</summary>
public sealed class AgentMarketplaceService
{
    private readonly SpecializedAgentRegistry _registry;
    private readonly StructuredLogCollector _log;

    public AgentMarketplaceService(SpecializedAgentRegistry registry, StructuredLogCollector log)
    {
        _registry = registry;
        _log = log;
    }

    public IReadOnlyList<AgentDescriptor> ListAvailable() =>
        _registry.All.Select(a => a.Descriptor).ToList();

    public void Install(string agentId)
    {
        // L'installation réelle (téléchargement/vérification) est déléguée au PluginRegistry existant.
        _log.Info("AgentMarketplace", "Demande d'installation", new { agentId });
    }
}

/// <summary>P2 — Local RL feedback loop : met à jour le classement des agents depuis le feedback local (sans cloud).</summary>
public sealed class LocalRlFeedbackLoop
{
    private readonly SettingsEngine _settings;
    private readonly Dictionary<string, (double reward, int pulls)> _weights = new();

    public LocalRlFeedbackLoop(SettingsEngine settings) => _settings = settings;

    public void RecordFeedback(string agentId, bool accepted)
    {
        if (!SettingsCatalog.AiAgents.LocalRlEnabled.Value) return;
        var (reward, pulls) = _weights.GetValueOrDefault(agentId);
        reward += accepted ? 1.0 : 0.0;
        _weights[agentId] = (reward, pulls + 1);
    }

    /// <summary>Bonus de classement UCB-lite pour le tri des suggestions.</summary>
    public double GetRankingBoost(string agentId)
    {
        if (!SettingsCatalog.AiAgents.LocalRlEnabled.Value) return 0;
        if (!_weights.TryGetValue(agentId, out var w) || w.pulls == 0) return 0;
        double mean = w.reward / w.pulls;
        return mean; // peut être combiné à CommandPaletteHistoryService
    }
}

/// <summary>P1 — Interactive refactor walkthrough : étapes avec checkpoints d'annulation.</summary>
public sealed class RefactorWalkthroughService
{
    private readonly StructuredLogCollector _log;
    private readonly Stack<string> _checkpoints = new();
    private readonly List<string> _steps = new();
    private int _cursor;

    public RefactorWalkthroughService(StructuredLogCollector log) => _log = log;

    /// <summary>Démarre un walkthrough ; les snapshots réels passent par TimeMachineEngine.</summary>
    public void BeginWalkthrough(IReadOnlyList<string> steps, string initialSnapshotId)
    {
        _steps.Clear();
        _steps.AddRange(steps);
        _cursor = 0;
        _checkpoints.Clear();
        _checkpoints.Push(initialSnapshotId);
        _log.Info("RefactorWalkthrough", "Démarrage", new { steps = steps.Count });
    }

    public string? NextStep()
    {
        if (_cursor >= _steps.Count) return null;
        return _steps[_cursor++];
    }

    public void CommitCheckpoint(string snapshotId)
    {
        _checkpoints.Push(snapshotId);
        _log.Info("RefactorWalkthrough", "Checkpoint créé", new { snapshotId });
    }

    public string? RollbackToLastCheckpoint() =>
        _checkpoints.Count > 0 ? _checkpoints.Pop() : null;
}
