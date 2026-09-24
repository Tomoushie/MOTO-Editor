// Moto.Core/Moto.AI/Generation/InlineEditService.cs
// ★ AJOUT (24/09, "écriture générative fonctionnelle") : l'appel du modèle pour une édition en ligne.
// Chemin local : Ollama /api/chat en FLUX (le texte arrive au fil de l'eau) avec une fenêtre de contexte explicite — l'ancien chemin
// (/api/generate) n'avait ni l'un ni l'autre. Chemin externe (OpenAI, Anthropic, Mistral… via le FallbackEngine) : la fonction d'envoi est
// fournie par l'appelant, la réponse passe par le MÊME contrôle (InlineEditPlanner).
using System.Diagnostics;
using Moto.Core.AI.Llm;

namespace Moto.Core.AI.Generation;

public sealed class InlineEditService
{
    private readonly Func<string, OllamaChatClient> _clientFactory;
    private readonly Func<GenerationSettings> _settings;

    public InlineEditService(Func<string, OllamaChatClient>? clientFactory = null, Func<GenerationSettings>? settings = null)
    {
        _clientFactory = clientFactory ?? (endpoint => new OllamaChatClient(endpoint));
        _settings = settings ?? (() => GenerationSettings.Load());
    }

    /// <summary>
    /// Demande l'édition à un modèle Ollama local. <paramref name="status"/> reçoit de courts messages de progression (appelé depuis un fil
    /// d'arrière-plan : à l'appelant de repasser sur le fil de l'interface). Ne lève pas pour un problème « normal » (Ollama éteint, modèle
    /// absent, réponse inutilisable) : il est dans <see cref="InlineEditOutcome.Problem"/>. L'annulation, elle, lève OperationCanceledException.
    /// </summary>
    public async Task<InlineEditOutcome> RunAsync(InlineEditRequest request, Action<string>? status = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Instruction))
            return InlineEditOutcome.Failure("Dis ce qu'il faut modifier.");

        var settings = _settings();
        using var client = _clientFactory(settings.Endpoint);

        IReadOnlyList<string> installed;
        try
        {
            installed = await client.ListModelsAsync(ct).ConfigureAwait(false);
        }
        catch (LlmException ex)
        {
            return InlineEditOutcome.Failure($"{ex.Message} Rien n'a été modifié.");
        }

        var (model, note) = GenerationModels.Choose(settings.Model, installed);
        if (model is null)
            return InlineEditOutcome.Failure("Aucun modèle n'est installé dans Ollama. Installe-en un (par exemple « ollama pull qwen2.5-coder:7b »), puis réessaie.");

        var info = await client.ShowAsync(model, ct).ConfigureAwait(false);
        var maxContext = info is { ContextLength: > 0 } ? Math.Min(InlineEditPrompts.LargeContext, info.ContextLength) : InlineEditPrompts.LargeContext;

        var prompt = InlineEditPrompts.Build(request, maxContext, out var problem);
        if (prompt is null)
            return InlineEditOutcome.Failure(problem ?? "Demande trop grosse.", model);

        var options = new LlmOptions { NumCtx = prompt.NumCtx, Temperature = settings.Temperature, NumPredict = prompt.MaxOutputTokens };
        status?.Invoke($"[{model}] Chargement du modèle…");

        var lines = 0;
        var clock = Stopwatch.StartNew();
        long lastReport = 0;
        void OnChunk(string chunk)
        {
            foreach (var c in chunk) if (c == '\n') lines++;
            if (clock.ElapsedMilliseconds - lastReport < 300) return; // le flux peut être rapide : pas plus de trois mises à jour par seconde
            lastReport = clock.ElapsedMilliseconds;
            status?.Invoke($"[{model}] Écriture en cours… {lines} ligne(s)");
        }

        LlmReply reply;
        try
        {
            reply = await client.ChatAsync(model, new[] { LlmMessage.System(prompt.System), LlmMessage.User(prompt.User) },
                tools: null, options, onContent: OnChunk, ct: ct).ConfigureAwait(false);
        }
        catch (LlmException ex)
        {
            return InlineEditOutcome.Failure($"{ex.Message} Rien n'a été modifié.", model);
        }

        return InlineEditPlanner.Plan(request, reply.Content, model) with { Note = note };
    }

    /// <summary>
    /// Même édition, mais la réponse vient d'ailleurs (fournisseur externe) : <paramref name="ask"/> reçoit la consigne complète (système + message)
    /// et renvoie le texte de la réponse. Le contrôle des blocs de code, le diff et les garde-fous sont exactement ceux du chemin local.
    /// </summary>
    public async Task<InlineEditOutcome> RunWithAsync(InlineEditRequest request, string modelLabel,
        Func<string, CancellationToken, Task<string>> ask, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Instruction))
            return InlineEditOutcome.Failure("Dis ce qu'il faut modifier.");

        var prompt = InlineEditPrompts.Build(request, InlineEditPrompts.LargeContext, out var problem);
        if (prompt is null)
            return InlineEditOutcome.Failure(problem ?? "Demande trop grosse.", modelLabel);

        string reply;
        try
        {
            reply = await ask(prompt.System + "\n\n" + prompt.User, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return InlineEditOutcome.Failure($"Le fournisseur n'a pas répondu ({ex.Message}). Rien n'a été modifié.", modelLabel);
        }

        return InlineEditPlanner.Plan(request, reply, modelLabel);
    }
}
