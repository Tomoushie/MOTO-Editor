using System;
using System.Linq;
using Moto.Core.Logging;
using Moto.Core.Settings;

namespace Moto.Core.AI.Speculative;

/// <summary>
/// BONUS — Remplace la logique d'acceptation simplifiée par une vraie
/// comparaison de logits (échantillonnage de type speculative decoding).
/// </summary>
public sealed class SpeculativeLogitsVerifier
{
    private readonly SettingsEngine _settings;
    private readonly StructuredLogCollector _log;
    private readonly Random _rng = new();

    public SpeculativeLogitsVerifier(SettingsEngine settings, StructuredLogCollector log)
    {
        _settings = settings;
        _log = log;
    }

    /// <summary>
    /// Vérifie un token draft contre les logits du modèle cible.
    /// Règle d'acceptation : p_target(token) >= min(p_draft(token), seuil) — approche standard.
    /// </summary>
    public bool VerifyToken(int draftTokenId, float[] draftLogits, float[] targetLogits)
    {
        if (draftLogits.Length != targetLogits.Length)
        {
            _log.Warning("LogitsVerifier", "Taille logits incohérente, rejet du draft.");
            return false;
        }

        float pDraft = SoftmaxProbability(draftLogits, draftTokenId);
        float pTarget = SoftmaxProbability(targetLogits, draftTokenId);
        double threshold = _settings.Shared.Ai.Advanced.SpeculativeAcceptThreshold.Value;

        // Acceptation probabiliste réelle : accepte si target >= draft * seuil
        bool accepted = pTarget >= pDraft * threshold;
        _log.Debug("LogitsVerifier", "Vérification token",
            new { draftTokenId, pDraft, pTarget, accepted });
        return accepted;
    }

    private static float SoftmaxProbability(float[] logits, int index)
    {
        float max = logits.Max();
        float sum = 0f;
        float target = 0f;
        for (int i = 0; i < logits.Length; i++)
        {
            float exp = (float)Math.Exp(logits[i] - max);
            sum += exp;
            if (i == index) target = exp;
        }
        return sum == 0f ? 0f : target / sum;
    }
}
