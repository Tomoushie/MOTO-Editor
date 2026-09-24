// Moto.Core/Moto.AI/Generation/ChatStreamService.cs
// ★ AJOUT (24/09, "écriture générative fonctionnelle") : la réponse du chat, écrite EN FLUX par un modèle Ollama local (/api/chat, fenêtre de
// contexte explicite, historique de la conversation, fichier ouvert) — l'ancien chemin (/api/generate) rendait tout d'un bloc après une longue
// attente, sans historique, et tronquait en silence au-delà de ~4 000 jetons.
// Chemin en ligne (OpenAI, Anthropic, Mistral… via le FallbackEngine) : la fonction d'envoi est fournie par l'appelant ; ce qui part est
// réduit à la question, à ses pièces jointes et à l'historique tapé (ChatPrompts.BuildForOnline).
using System.Diagnostics;
using System.Text;
using Moto.Core.AI.Llm;

namespace Moto.Core.AI.Generation;

/// <summary>Résultat d'une réponse de chat. En cas d'échec, <see cref="Problem"/> est une phrase lisible par l'utilisateur.</summary>
public sealed record ChatOutcome
{
    public bool Succeeded { get; init; }

    /// <summary>Ce que le modèle a écrit, tel quel (c'est ce texte qui sera rejoué dans l'historique).</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Modèle réellement utilisé (ou fournisseur en ligne).</summary>
    public string? Model { get; init; }

    public string? Problem { get; init; }

    /// <summary>Le modèle réglé n'était pas installé : lequel l'a remplacé.</summary>
    public string? Note { get; init; }

    /// <summary>Ollama éteint, ou aucun modèle installé : une autre source peut répondre à la place.</summary>
    public bool LocalUnavailable { get; init; }

    /// <summary>Texte déjà écrit quand l'échec est survenu (délai dépassé en pleine réponse…).</summary>
    public string PartialContent { get; init; } = string.Empty;

    /// <summary>Le message envoyé (question + contexte) : à rejouer tel quel dans l'historique.</summary>
    public string SentUserMessage { get; init; } = string.Empty;

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SentContext { get; init; } = Array.Empty<string>();

    /// <summary>La réponse s'est arrêtée sur le plafond de longueur : elle est incomplète.</summary>
    public bool Truncated { get; init; }

    /// <summary>Délai avant le premier mot (mesuré côté client) — ~0,1 s quand Ollama reprend ce qu'il avait déjà lu.</summary>
    public double FirstChunkSeconds { get; init; }

    public double TotalSeconds { get; init; }
    public int PromptTokens { get; init; }

    public static ChatOutcome Failure(string problem, string? model = null) => new() { Problem = problem, Model = model };
}

public sealed class ChatStreamService
{
    private readonly Func<string, OllamaChatClient> _clientFactory;
    private readonly Func<GenerationSettings> _settings;

    public ChatStreamService(Func<string, OllamaChatClient>? clientFactory = null, Func<GenerationSettings>? settings = null)
    {
        _clientFactory = clientFactory ?? (endpoint => new OllamaChatClient(endpoint));
        _settings = settings ?? (() => GenerationSettings.Load());
    }

    /// <summary>
    /// Demande la réponse à un modèle Ollama local. <paramref name="onDelta"/> reçoit chaque morceau de texte dès qu'il arrive, et
    /// <paramref name="status"/> de courts messages de progression — tous deux depuis un fil d'arrière-plan : à l'appelant de repasser sur le fil
    /// de l'interface. Ne lève pas pour un problème « normal » (Ollama éteint, modèle absent, délai dépassé) : il est dans
    /// <see cref="ChatOutcome.Problem"/>. L'annulation, elle, lève OperationCanceledException.
    /// </summary>
    public async Task<ChatOutcome> StreamAsync(ChatRequest request, Action<string>? onDelta = null, Action<string>? status = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return ChatOutcome.Failure("Écris d'abord ta question.");

        var settings = _settings();
        using var client = _clientFactory(settings.Endpoint);

        IReadOnlyList<string> installed;
        try
        {
            installed = await client.ListModelsAsync(ct).ConfigureAwait(false);
        }
        catch (LlmException ex)
        {
            return ChatOutcome.Failure(ex.Message) with { LocalUnavailable = true };
        }

        var (model, note) = GenerationModels.Choose(settings.Model, installed);
        if (model is null)
            return ChatOutcome.Failure("Aucun modèle n'est installé dans Ollama. Installe-en un (par exemple « ollama pull qwen2.5-coder:7b »), puis réessaie.")
                with { LocalUnavailable = true };

        var info = await client.ShowAsync(model, ct).ConfigureAwait(false);
        var maxContext = info is { ContextLength: > 0 } ? Math.Min(ChatPrompts.LargeContext, info.ContextLength) : ChatPrompts.LargeContext;

        var prompt = ChatPrompts.Build(request, maxContext, out var problem);
        if (prompt is null)
            return ChatOutcome.Failure(problem ?? "Message trop gros.", model);

        var options = new LlmOptions { NumCtx = prompt.NumCtx, Temperature = settings.Temperature, NumPredict = prompt.MaxOutputTokens };
        status?.Invoke($"{model} lit la demande…");

        var written = new StringBuilder();
        LlmReply reply;
        try
        {
            reply = await client.ChatAsync(model, prompt.Messages, tools: null, options,
                onContent: chunk => { written.Append(chunk); onDelta?.Invoke(chunk); }, ct: ct).ConfigureAwait(false);
        }
        catch (LlmException ex)
        {
            return ChatOutcome.Failure(ex.Message, model) with
            {
                PartialContent = written.ToString(), SentUserMessage = prompt.SentUserMessage, Notes = prompt.Notes, SentContext = prompt.SentContext,
            };
        }

        return new ChatOutcome
        {
            Succeeded = true,
            Content = reply.Content,
            Model = model,
            Note = note,
            SentUserMessage = prompt.SentUserMessage,
            Notes = prompt.Notes,
            SentContext = prompt.SentContext,
            Truncated = string.Equals(reply.DoneReason, "length", StringComparison.OrdinalIgnoreCase),
            FirstChunkSeconds = reply.FirstChunkSeconds,
            TotalSeconds = reply.TotalSeconds,
            PromptTokens = reply.PromptTokens,
        };
    }

    /// <summary>
    /// La réponse vient d'un fournisseur en ligne : <paramref name="ask"/> reçoit la conversation en un seul texte (sans le fichier ouvert ni la
    /// sélection) et renvoie la réponse avec le nom du fournisseur. Une exception de <paramref name="ask"/> devient un problème lisible.
    /// </summary>
    public async Task<ChatOutcome> RunOnlineAsync(ChatRequest request, Func<string, CancellationToken, Task<(string Text, string Source)>> ask,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return ChatOutcome.Failure("Écris d'abord ta question.");

        var prompt = ChatPrompts.BuildForOnline(request, out var problem);
        if (prompt is null)
            return ChatOutcome.Failure(problem ?? "Message trop gros.");

        var clock = Stopwatch.StartNew();
        (string Text, string Source) answer;
        try
        {
            answer = await ask(ChatPrompts.Flatten(prompt), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ChatOutcome.Failure($"Aucun service en ligne n'a répondu ({ex.Message}).") with { Notes = prompt.Notes };
        }

        if (string.IsNullOrWhiteSpace(answer.Text))
            return ChatOutcome.Failure($"{answer.Source} n'a rien répondu.", answer.Source) with { Notes = prompt.Notes };

        return new ChatOutcome
        {
            Succeeded = true,
            Content = answer.Text,
            Model = answer.Source,
            SentUserMessage = prompt.SentUserMessage,
            Notes = prompt.Notes,
            SentContext = prompt.SentContext,
            TotalSeconds = clock.Elapsed.TotalSeconds,
        };
    }
}
