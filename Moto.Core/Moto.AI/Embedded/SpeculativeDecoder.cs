// Mise à jour de Moto.Core/AI/Embedded/SpeculativeDecoder.cs

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
