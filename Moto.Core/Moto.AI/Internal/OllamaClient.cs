// Moto.Core/AI/Internal/OllamaClient.cs
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.Settings;

namespace Moto.Core.AI.Internal;

/// <summary>
/// Client Ollama (existant, préservé).
/// ★ CORRECTION (02/09, "Réglages → IA Locale") : le modèle appelé par
/// GenerateAsync était codé en dur ("qwen2.5-coder:7b") et le délai d'attente
/// fixé à 5 minutes sans aucun moyen de les changer — confirmé : AUCUN
/// réglage utilisateur n'atteignait jamais ce client (MotoAiKernel construit
/// toujours "new OllamaClient()" sans paramètre). Le constructeur sans
/// paramètre lit maintenant SettingsEngine.Shared (mêmes clés que le
/// catalogue "IA Locale", SettingsCatalog.cs) au lieu de valeurs figées —
/// Model/Endpoint/Timeout deviennent de vraies propriétés modifiables.
/// </summary>
public sealed class OllamaClient
{
    private readonly HttpClient _http;

    /// <summary>Adresse du serveur Ollama.</summary>
    public string Endpoint { get; set; }

    /// <summary>Modèle local utilisé pour GenerateAsync.</summary>
    public string Model { get; set; }

    public OllamaClient(string? endpoint = null, string? model = null, int? timeoutSeconds = null)
    {
        var s = SettingsEngine.Shared;
        Endpoint = endpoint ?? s.GetString("ollama_endpoint", "http://localhost:11434");
        Model = model ?? s.GetString("ollama_model", "qwen2.5-coder:7b");
        var timeout = timeoutSeconds ?? s.GetInt("ollama_timeout_seconds", 300);
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(5, timeout)) };
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"{Endpoint}/api/tags", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var payload = new { model = Model, prompt, stream = false };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{Endpoint}/api/generate", content, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);

        // ★ CORRECTION (02/09, réveil du panneau IA) : le commentaire disait
        // "Parser la réponse Ollama" mais ne faisait rien de tel — le JSON brut
        // complet (model/created_at/response/context/...) était renvoyé tel
        // quel. Invisible jusqu'ici (rien n'affichait la réponse à un vrai
        // utilisateur), révélé par le premier vrai test du panneau de chat.
        // Extraction du seul champ "response", même logique que l'autre client
        // Ollama du dépôt (Moto.Editor/AI/OllamaClient.cs), qui le faisait déjà
        // correctement.
        using var doc = JsonDocument.Parse(json);
        // ★ CORRECTION (02/09, revue croisée) : un repli silencieux vers "" en cas de
        // forme de réponse inattendue (proxy non standard, évolution future de
        // l'API...) masquerait le souci sans aucune trace — contrairement à l'ancien
        // comportement (JSON brut affiché tel quel) qui, bien que moche, montrait AU
        // MOINS qu'un problème existait. Repli sur un message diagnostique explicite
        // plutôt que sur l'un ou l'autre extrême.
        return doc.RootElement.TryGetProperty("response", out var value)
            ? value.GetString() ?? string.Empty
            : "[Réponse Ollama inattendue — champ \"response\" absent]";
    }

    public async Task<string> GenerateCodeAsync(string instruction, string? context, CancellationToken ct = default)
    {
        var fullPrompt = context != null
            ? $"Context:\n{context}\n\nInstruction: {instruction}\n\nCode:"
            : $"Instruction: {instruction}\n\nCode:";
        return await GenerateAsync(fullPrompt, ct);
    }

    public async Task<string> CompleteCodeAsync(string prefix, string suffix, CancellationToken ct = default)
    {
        var prompt = $"<pre>{prefix}<suf>{suffix}<mid>";
        return await GenerateAsync(prompt, ct);
    }
}
