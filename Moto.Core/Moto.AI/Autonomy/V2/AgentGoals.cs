// Moto.Core/Moto.AI/Autonomy/V2/AgentGoals.cs
// ★ AJOUT (24/09, agent v2) : les consignes prêtes à l'emploi de /refactor, /test et /doc. Une par moteur : la v1 écrit un fichier
// entier avec « WriteFile » (à lui donner mot pour mot), la v2 modifie des passages avec ses propres outils (edit_file,
// insert_lines, write_file) — une consigne « écris la version corrigée avec WriteFile sur le même chemin » y pousserait le modèle
// à réécrire tout le fichier, ce que write_file refuse justement de faire sur un fichier existant.
namespace Moto.Core.AI.Autonomy.V2;

public static class AgentGoals
{
    /// <summary>Les préréglages connus, dans l'ordre où ils sont proposés.</summary>
    public static readonly IReadOnlyList<string> Presets = new[] { "refactor", "test", "doc" };

    /// <param name="preset">« refactor », « test » ou « doc ».</param>
    /// <param name="displayPath">Le fichier visé, tel qu'il sera écrit dans la consigne (relatif au projet si possible).</param>
    /// <param name="v2">Moteur v2 (défaut de l'éditeur) ou v1 (secours).</param>
    public static string ForPreset(string preset, string displayPath, bool v2)
        => preset switch
        {
            "refactor" => v2
                ? $"Refactore le fichier \"{displayPath}\" pour améliorer sa lisibilité et sa maintenabilité, SANS changer son comportement " +
                  "(mêmes entrées, mêmes sorties). Lis-le, puis fais des modifications ciblées sur les passages à améliorer : change le " +
                  "minimum de lignes, ne réécris pas le fichier entier. Résume en 2-3 phrases ce que tu as changé et pourquoi."
                : $"Refactore le fichier \"{displayPath}\" pour améliorer sa lisibilité et sa " +
                  "maintenabilité, SANS changer son comportement (mêmes entrées, mêmes sorties). " +
                  "Lis-le d'abord avec ReadFile, puis écris la version corrigée avec WriteFile sur " +
                  "exactement ce même chemin. Résume en 2-3 phrases ce que tu as changé et pourquoi.",

            "test" => v2
                ? $"Écris des tests pour le fichier \"{displayPath}\". Lis-le pour comprendre son comportement, repère le projet de tests " +
                  "existant et ses conventions, puis crée un NOUVEAU fichier de test avec write_file (ne modifie JAMAIS le fichier original). " +
                  "Résume en 2-3 phrases ce que tu as testé."
                : $"Écris des tests pour le fichier \"{displayPath}\". Lis-le d'abord avec ReadFile " +
                  "pour comprendre son comportement, puis crée un NOUVEAU fichier de test à côté " +
                  "(ne modifie JAMAIS le fichier original) avec WriteFile, en suivant les " +
                  "conventions déjà utilisées dans ce projet si tu peux les repérer. Résume en 2-3 " +
                  "phrases ce que tu as testé.",

            "doc" => v2
                ? $"Documente le fichier \"{displayPath}\" : lis-le, puis ajoute des commentaires (XML doc « /// » pour le C#, docstring ou " +
                  "commentaires adaptés sinon) au-dessus de chaque classe et de chaque méthode publique qui n'en a pas déjà. Insère les " +
                  "nouvelles lignes avec insert_lines, SANS modifier les lignes existantes ni le comportement du code. " +
                  "Résume en 2-3 phrases ce que tu as documenté."
                : $"Documente le fichier \"{displayPath}\" : lis-le d'abord avec ReadFile, puis " +
                  "ajoute des commentaires (XML doc pour le C#, docstring/commentaires adaptés " +
                  "sinon) sur les méthodes et classes publiques qui n'en ont pas déjà, SANS changer " +
                  "le comportement du code. Écris la version documentée avec WriteFile sur " +
                  "exactement ce même chemin. Résume en 2-3 phrases ce que tu as documenté.",

            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Préréglage inconnu."),
        };
}
