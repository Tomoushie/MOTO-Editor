// Moto.Editor/Services/ChatService.cs — AJOUT de la méthode AskWithCodeAsync :

/// <summary>
/// Envoie un prompt avec le code courant au modèle choisi,
/// pour modification en direct depuis le bandeau IA.
/// </summary>
public async Task<string> AskWithCodeAsync(string model, string prompt, string code)
{
    var fullPrompt =
        "Tu es MOTO AI, un assistant de développement.\n" +
        $"Demande : {prompt}\n\n" +
        "Code actuel :\n" + code + "\n\n" +
        "Réponds avec le code COMPLET modifié dans un bloc ``` , sans explication.";

    bool internalModel = model.Contains("interne", StringComparison.OrdinalIgnoreCase);

    if (internalModel)
    {
        var resp = _kernel.Execute(new Moto.Core.AI.Internal.Models.AiRequest
        {
            WorkspacePath = WorkspaceRoot,
            UserText = fullPrompt,
            Mode = Mode
        });

        return FormatInternal(resp);
    }

    var ai = await _fallback.GenerateAsync(fullPrompt, WorkspaceRoot);

    return ai.Success ? ai.Content : "Le modèle externe n'a pas répondu. Vérifie tes clés API.";
}
