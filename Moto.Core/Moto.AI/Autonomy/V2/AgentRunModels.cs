// Moto.Core/Moto.AI/Autonomy/V2/AgentRunModels.cs
// ★ AJOUT (24/09, agent v2) : ce qui entre dans un run (demande), ce qui en sort (résultat), ce qu'on
// observe pendant (événements) et qui autorise (approbateur). Aucune dépendance MAUI.
using Moto.Core.AI.Llm;
using Moto.Core.Settings;

namespace Moto.Core.AI.Autonomy.V2;

/// <summary>Comment l'agent obtient les appels d'outils du modèle.</summary>
public enum AgentToolMode
{
    /// <summary>Appels d'outils natifs d'Ollama. Un modèle qui « raconte » au lieu d'appeler l'outil échoue.</summary>
    Native,

    /// <summary>Sortie contrainte par un schéma JSON (voir StructuredTools) : le modèle ne peut répondre que par un appel d'outil valide.</summary>
    Structured,

    /// <summary>Natif d'abord ; bascule en structuré si le modèle n'a pas la capacité « tools » ou n'appelle pas les outils après relance.</summary>
    Auto,
}

public sealed class AgentRunRequest
{
    public required string Model { get; init; }
    public required string Goal { get; init; }
    public string WorkspaceRoot { get; init; } = string.Empty;
    public string AgentId { get; init; } = "agent-1";

    public AgentToolMode ToolMode { get; init; } = AgentToolMode.Auto;

    public int MaxSteps { get; init; } = 30;
    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromMinutes(15);
    public LlmOptions Options { get; init; } = new();

    /// <summary>Contexte joint à l'objectif : fichier ouvert dans l'éditeur, sélection…</summary>
    public string? Context { get; init; }

    /// <summary>Commande de vérification suggérée (ex. dotnet build …). Sinon le modèle choisit.</summary>
    public string? VerifyCommand { get; init; }

    /// <summary>Si du code a été modifié sans être compilé, demande UNE fois de le vérifier avant de terminer.</summary>
    public bool RequireVerification { get; init; } = true;

    public bool WriteAuditLog { get; init; } = true;
    public string? RunId { get; init; }

    /// <summary>Dossier des sauvegardes d'originaux (défaut : %LOCALAPPDATA%\MotoEditor\AgentBackups).</summary>
    public string? BackupFolder { get; init; }
}

public enum AgentOutcome
{
    /// <summary>Le modèle a terminé (finish ou réponse finale).</summary>
    Completed,
    /// <summary>Trois refus d'affilée de l'utilisateur.</summary>
    Declined,
    StepLimit,
    TimeLimit,
    Cancelled,
    Failed,
    /// <summary>Le modèle répète le même appel sans progrès.</summary>
    LoopDetected,
}

public sealed class AgentRunResult
{
    public AgentOutcome Outcome { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string? Error { get; init; }

    /// <summary>Renseigné quand le run se dit terminé alors qu'AUCUN fichier n'a changé malgré des tentatives (à montrer à l'utilisateur).</summary>
    public string? Warning { get; init; }

    public string RunId { get; init; } = string.Empty;

    /// <summary>Mode d'appel d'outils à la fin du run : « native », « structured » ou « native→structured » (bascule en cours de route).</summary>
    public string ToolMode { get; init; } = "native";

    public int Steps { get; init; }
    public int ModelCalls { get; init; }
    public int ToolCalls { get; init; }
    public int ToolErrors { get; init; }
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public TimeSpan Elapsed { get; init; }
    public double ModelSeconds { get; init; }

    public IReadOnlyList<ChangedFile> Changes { get; init; } = Array.Empty<ChangedFile>();

    /// <summary>Permet « Annuler les modifications de cette exécution » (Restore()).</summary>
    public RunBackup? Backup { get; init; }

    public IReadOnlyList<LlmMessage> Transcript { get; init; } = Array.Empty<LlmMessage>();

    public bool Succeeded => Outcome == AgentOutcome.Completed;
}

public enum AgentEventKind
{
    Started,
    /// <summary>Morceau de texte du modèle (en direct).</summary>
    Text,
    ToolCalled,
    ToolProposed,
    ToolApproved,
    ToolDeclined,
    ToolResult,
    Nudge,
    Finished,
}

public sealed record AgentEvent(AgentEventKind Kind, int Step, string Text, string? Tool = null, string? Path = null, bool IsError = false);

// ── Confirmation humaine ────────────────────────────────────────────────────

public sealed class ApprovalRequest
{
    public string AgentId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public ApprovalKind Kind { get; init; }
    public bool IsDestructive { get; init; }
    public string? Path { get; init; }
    public DiffResult? Diff { get; init; }
}

public interface IAgentApprover
{
    Task<bool> ApproveAsync(ApprovalRequest request, CancellationToken ct);
}

/// <summary>Passe par la boîte de confirmation existante de l'éditeur (AiConfirmationService).</summary>
public sealed class ConfirmationServiceApprover : IAgentApprover
{
    private const int MaxDetailChars = 9000;
    private readonly AiConfirmationService _confirmation;

    public ConfirmationServiceApprover(AiConfirmationService confirmation)
        => _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));

    public async Task<bool> ApproveAsync(ApprovalRequest request, CancellationToken ct)
    {
        var details = request.Details.Length > MaxDetailChars
            ? request.Details[..MaxDetailChars] + $"\n… (aperçu tronqué : {request.Details.Length - MaxDetailChars} caractères de plus)"
            : request.Details;

        var result = await _confirmation.RequestAsync(new ConfirmationRequest
        {
            Action = request.Kind == ApprovalKind.FileChange ? ConfirmationAction.ModifyCode : ConfirmationAction.ExecuteCommand,
            Title = $"🤖 Agent « {request.AgentId} » — {request.Title}",
            Message = request.Summary,
            Details = details,
            ConfirmText = "Autoriser",
            CancelText = "Refuser",
            IsDestructive = request.IsDestructive,
        });
        return result.Confirmed;
    }
}

/// <summary>
/// Autorise sans demander — RÉSERVÉ AUX BANCS D'ESSAI automatisés (AgentBench, tests). Jamais branché dans l'éditeur.
/// Un filtre optionnel refuse ce qu'il ne reconnaît pas (par défaut : toute commande qui n'est pas un build/test .NET).
/// </summary>
public sealed class AutoApprover : IAgentApprover
{
    private readonly Func<ApprovalRequest, bool> _accept;

    public int Approved { get; private set; }
    public int Refused { get; private set; }

    public AutoApprover(Func<ApprovalRequest, bool>? accept = null)
        => _accept = accept ?? DefaultAccept;

    public Task<bool> ApproveAsync(ApprovalRequest request, CancellationToken ct)
    {
        var ok = _accept(request);
        if (ok) Approved++; else Refused++;
        return Task.FromResult(ok);
    }

    private static bool DefaultAccept(ApprovalRequest r)
    {
        if (r.Kind == ApprovalKind.FileChange) return true;
        var line = r.Details.Split('\n').FirstOrDefault(l => l.StartsWith("Commande : ", StringComparison.Ordinal)) ?? string.Empty;
        var command = line["Commande : ".Length..].Trim();
        return command.StartsWith("dotnet build", StringComparison.OrdinalIgnoreCase)
            && command.IndexOfAny(new[] { '&', '|', ';', '>', '<', '`', '$' }) < 0;
    }
}
