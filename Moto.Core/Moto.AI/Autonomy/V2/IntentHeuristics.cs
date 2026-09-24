// Moto.Core/Moto.AI/Autonomy/V2/IntentHeuristics.cs
// ★ AJOUT (24/09, agent v2) : « cette tâche demande-t-elle de MODIFIER des fichiers ? »
// Sert uniquement à relancer un modèle qui se dit « terminé » sans avoir rien modifié alors que la consigne
// demandait de le faire (constat du banc d'essai : un petit modèle recopie le fichier lu ou annonce un plan
// au lieu d'appeler edit_file). En cas de doute la réponse est NON : mieux vaut ne pas relancer
// une simple question que pousser le modèle à modifier ce qu'on lui demandait seulement de lire.
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Moto.Core.AI.Autonomy.V2;

internal static class IntentHeuristics
{
    /// <summary>
    /// Interdictions GLOBALES de modifier (texte normalisé : minuscules, sans accents). Les restrictions ciblées
    /// (« sans changer le comportement », « ne touche pas à B.cs ») n'en font pas partie : la tâche reste une écriture.
    /// </summary>
    private static readonly string[] NoWritePhrases =
    {
        "sans rien modifier", "sans rien changer", "sans rien ecrire", "sans modifier aucun fichier", "sans modifier de fichier",
        "sans modifier les fichiers", "sans ecrire de fichier", "ne modifie rien", "ne change rien", "ne touche a rien", "n'ecris rien",
        "ne fais aucune modification", "lecture seule", "read-only", "readonly", "read only",
        "do not modify anything", "don't modify anything", "do not change anything", "don't change anything",
        "without modifying anything", "without changing anything", "do not edit anything", "don't edit anything",
    };

    /// <summary>« ne change rien d'AUTRE » est une contrainte sur le reste, pas une interdiction : on l'écarte avant de chercher.</summary>
    private static readonly Regex ElseClause = new(
        @"\b(?:rien|aucun\w*|nothing|anything)\s+(?:d'autres?|de\s+plus|else|other)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Verbes d'action (impératif, infinitif, anglais) : mots entiers, accents déjà retirés.</summary>
    private static readonly Regex ActionWords = new(
        @"\b(?:ajout(?:e|er|ez)|rajout(?:e|er|ez)|cree|creer|creez|modifie|modifier|modifiez|renomme|renommer|renommez|" +
        @"corrige|corriger|corrigez|repare|reparer|reparez|supprime|supprimer|supprimez|efface|effacer|extrais|extraire|" +
        @"remplace|remplacer|remplacez|deplace|deplacer|refactorise|refactoriser|implemente|implementer|" +
        @"ecris|ecrire|ecrivez|reecris|reecrire|genere|generer|generez|documente|documenter|commente|commenter|" +
        @"insere|inserer|inserez|transforme|transformer|convertis|convertir|change|changer|changez|retire|retirer|" +
        @"enleve|enlever|integre|integrer|applique|appliquer|mets|mettre|passe(?!\s+en\s+revue)|" +
        @"add|create|rename|remove|delete|replace|write|generate|update|rewrite|insert|fix|make|move|edit|implement|" +
        @"refactor|extract|modify|change)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool ExpectsFileChanges(string? goal)
    {
        if (string.IsNullOrWhiteSpace(goal)) return false;
        var text = ElseClause.Replace(Normalize(goal), string.Empty);

        foreach (var phrase in NoWritePhrases)
            if (text.Contains(phrase, StringComparison.Ordinal)) return false;

        return ActionWords.IsMatch(text);
    }

    /// <summary>Minuscules, sans accents, apostrophes typographiques ramenées à « ' ».</summary>
    internal static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Replace('’', '\'').Replace('‘', '\'').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
