// Moto.Core/Moto.AI/Autonomy/V2/ContextTrimmer.cs
// ★ AJOUT (24/09, agent v2) : garde la conversation dans la fenêtre du modèle. Sans ça, Ollama coupe
// SILENCIEUSEMENT le début du dialogue (donc la consigne système) dès que quelques fichiers ont été lus —
// mesuré : c'est la cause n°1 d'un agent qui « oublie » ce qu'il doit faire. On préfère remplacer les
// vieux RÉSULTATS d'outils (relisibles à la demande) par une ligne, et garder consigne + objectif intacts.
using Moto.Core.AI.Llm;

namespace Moto.Core.AI.Autonomy.V2;

internal static class ContextTrimmer
{
    private const double CharsPerToken = 3.0;   // prudent pour du code
    private const int ReplyReserveTokens = 2048;
    private const int KeepRecentToolMessages = 2;

    public static int EstimateTokens(IReadOnlyList<LlmMessage> messages)
    {
        double chars = 0;
        foreach (var m in messages)
        {
            chars += m.Content.Length + 16;
            if (m.ToolCalls is not null)
                foreach (var c in m.ToolCalls) chars += c.Name.Length + c.Arguments.ToJsonString().Length;
        }
        return (int)(chars / CharsPerToken);
    }

    /// <summary>Retourne faux si, malgré tout, la conversation ne tient pas.</summary>
    public static bool Fit(List<LlmMessage> messages, int numCtx, int toolSpecChars)
    {
        var budget = (int)(numCtx * 0.9) - ReplyReserveTokens - (int)(toolSpecChars / CharsPerToken);
        if (budget < 1024) budget = 1024;

        // 1) Remplacer les plus anciens résultats d'outils par une ligne.
        while (EstimateTokens(messages) > budget)
        {
            var toolMessages = messages.Where(m => m.Role == "tool").ToList();
            var oldest = toolMessages
                .Take(Math.Max(0, toolMessages.Count - KeepRecentToolMessages))
                .FirstOrDefault(m => !IsStub(m));
            if (oldest is null) break; // plus aucun ancien résultat à retirer

            oldest.Content = $"[ancien résultat de {oldest.ToolName} retiré pour libérer de la place ({oldest.Content.Length} caractères) — rappelle l'outil si tu en as encore besoin]";
        }

        // 2) Encore trop : raccourcir le plus gros résultat récent.
        var guard = 0;
        while (EstimateTokens(messages) > budget && guard++ < 6)
        {
            var biggest = messages.Where(m => m.Role == "tool" && m.Content.Length > 2500)
                .OrderByDescending(m => m.Content.Length).FirstOrDefault();
            if (biggest is null) break;
            biggest.Content = biggest.Content[..2000] + "\n… (raccourci : contexte du modèle presque plein)";
        }

        return EstimateTokens(messages) <= budget + ReplyReserveTokens / 2;
    }

    private static bool IsStub(LlmMessage m) => m.Content.StartsWith("[ancien résultat", StringComparison.Ordinal);
}
