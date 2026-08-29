// Mise à jour de Moto.Core/AI/Embedded/SpeculativeDecoder.cs

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Décodeur spéculatif (Embedded) : vérifie les tokens générés par le modèle draft
/// à l'aide du modèle target, en une seule passe batch.
/// </summary>
public sealed class SpeculativeDecoder
{
    private readonly EmbeddedLlmEngine _targetModel;
    private readonly DecoderVerificationConfig _config;

    public SpeculativeDecoder(EmbeddedLlmEngine targetModel)
    {
        _targetModel = targetModel;
        _config = new DecoderVerificationConfig();
    }

    /// <summary>
    /// Vérifie les tokens draft avec le modèle target en UNE SEULE passe (batch).
    /// </summary>
    private async Task<VerificationResult> VerifyWithTargetAsync(
        string prompt,
        List<string> draftTokens,
        CancellationToken ct)
    {
        var verified = new List<VerifiedToken>();

        // Construction d'un seul prompt contenant le contexte + tous les tokens draft
        // Ex: "prompt... [DRAFT_TOKEN_1] [DRAFT_TOKEN_2] [DRAFT_TOKEN_3]"
        var batchPrompt = prompt + " " + string.Join(" ", draftTokens);

        // ★ Le modèle target génère K+1 tokens en une seule inférence (forward pass unique)
        // Cela permet au GPU de paralléliser le calcul des logits pour toutes les positions.
        var targetResponse = await _targetModel.GenerateAsync(
            batchPrompt,
            maxTokens: draftTokens.Count + 1,
            temperature: _config.TargetTemperature,
            ct: ct);

        var targetTokens = SplitTokens(targetResponse);

        for (int i = 0; i < draftTokens.Count; i++)
        {
            var draftToken = draftTokens[i];
            // Le token target à la position i+1 (car le premier token généré est la suite du prompt)
            var targetToken = i + 1 < targetTokens.Count ? targetTokens[i + 1] : "";

            var isAccepted = string.Equals(draftToken.Trim(), targetToken.Trim(), StringComparison.OrdinalIgnoreCase)
                             || ComputeSimilarity(draftToken, targetToken) > _config.AcceptanceThreshold;

            verified.Add(new VerifiedToken
            {
                DraftToken = draftToken,
                TargetToken = targetToken,
                AlternativeToken = targetToken,
                IsAccepted = isAccepted
            });

            if (!isAccepted) break; // Arrêt au premier rejet (standard speculative decoding)
        }

        return new VerificationResult { VerifiedTokens = verified };
    }

    /// <summary>
    /// Découpe la réponse brute du modèle target en tokens (mots), pour l'aligner
    /// positionnellement avec les tokens draft (cf. string.Join(" ", draftTokens) plus haut).
    /// </summary>
    private static List<string> SplitTokens(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>
    /// Similarité normalisée [0..1] entre deux tokens, basée sur la distance de Levenshtein.
    /// </summary>
    private static double ComputeSimilarity(string a, string b)
    {
        a ??= string.Empty;
        b ??= string.Empty;
        if (a.Length == 0 && b.Length == 0) return 1.0;

        var maxLength = Math.Max(a.Length, b.Length);
        if (maxLength == 0) return 1.0;

        var distance = LevenshteinDistance(a, b);
        return 1.0 - (double)distance / maxLength;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var costs = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) costs[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            costs[0] = i;
            var previousDiagonal = i - 1;
            for (var j = 1; j <= b.Length; j++)
            {
                var previousDiagonalSave = costs[j];
                costs[j] = a[i - 1] == b[j - 1]
                    ? previousDiagonal
                    : 1 + Math.Min(previousDiagonal, Math.Min(costs[j], costs[j - 1]));
                previousDiagonal = previousDiagonalSave;
            }
        }

        return costs[b.Length];
    }

    private sealed class DecoderVerificationConfig
    {
        public double AcceptanceThreshold { get; init; } = 0.8;
        public float TargetTemperature { get; init; } = 0.5f;
    }
}

/// <summary>
/// Résultat de vérification d'un token draft face au modèle target.
/// </summary>
public sealed class VerifiedToken
{
    public string DraftToken { get; set; } = string.Empty;
    public string TargetToken { get; set; } = string.Empty;
    public string AlternativeToken { get; set; } = string.Empty;
    public bool IsAccepted { get; set; }
}

/// <summary>
/// Résultat global de la vérification batch des tokens draft.
/// </summary>
public sealed class VerificationResult
{
    public List<VerifiedToken> VerifiedTokens { get; set; } = new();
}
