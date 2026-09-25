// Moto.Core/Moto.AI/Generation/InlineEditPrompts.cs
// ★ AJOUT (24/09, "écriture générative fonctionnelle") : les consignes envoyées au modèle pour une édition en ligne, et le calcul de
// la fenêtre de contexte à demander.
// Pourquoi la fenêtre de contexte compte : l'ancien chemin (/api/generate, sans options) laissait Ollama à son défaut (souvent 4096 jetons),
// qui TRONQUE silencieusement le début du prompt — donc la consigne — dès que le fichier est un peu long.
// Pourquoi seulement deux tailles (16384 ou 32768) : quand la taille demandée change d'un appel à l'autre, Ollama recharge le modèle
// (~13 s mesuré pour un 7B). 16384 est aussi le défaut de l'agent : le modèle déjà en mémoire est réutilisé tel quel.
namespace Moto.Core.AI.Generation;

/// <param name="System">Consigne système.</param>
/// <param name="User">Le message : contexte, passage, demande.</param>
/// <param name="MaxOutputTokens">Plafond de jetons à générer (un modèle qui s'emballe s'arrête là, au lieu de tourner des minutes).</param>
/// <param name="NumCtx">Fenêtre de contexte à demander.</param>
public sealed record InlineEditPrompt(string System, string User, int MaxOutputTokens, int NumCtx);

public static class InlineEditPrompts
{
    public const int SmallContext = 16384;
    public const int LargeContext = 32768;

    private const int LinesBefore = 40;
    private const int LinesAfter = 25;
    private const int MaxContextChars = 3000;

    public const string SystemText =
        "Tu es l'assistant de programmation intégré à un éditeur de code. " +
        "Tu modifies UNIQUEMENT ce qu'on te demande, en gardant le style, l'indentation et les noms existants. " +
        "Tu réponds par le code modifié EN ENTIER dans UN SEUL bloc de code ```, sans numéros de ligne, puis rien d'autre : aucune explication. " +
        "N'abrège jamais : pas de « ... », pas de « reste du code inchangé ».";

    /// <summary>Environ 3 caractères par jeton pour du code : volontairement prudent (on préfère surestimer).</summary>
    public static int EstimateTokens(string text) => (text.Length + 2) / 3;

    /// <summary>
    /// Construit les consignes, ou renvoie null avec <paramref name="problem"/> si la demande est trop grosse pour être traitée d'un bloc.
    /// <paramref name="maxContext"/> : la plus grande fenêtre que le modèle accepte.
    /// </summary>
    public static InlineEditPrompt? Build(InlineEditRequest request, int maxContext, out string? problem)
    {
        problem = null;

        var doc = InlineEditPlanner.Normalize(request.DocumentText);
        var name = string.IsNullOrWhiteSpace(request.DisplayPath) ? "sans nom" : Path.GetFileName(request.DisplayPath);
        var header = $"Fichier : {name} ({CodeBlocks.LanguageLabel(request.DisplayPath)})\n\n";
        var instruction = request.Instruction.Trim();

        string target;
        string user;

        if (request.Scope == InlineEditScope.WholeFile)
        {
            target = doc;
            if (target.Length > InlineEditPlanner.MaxTargetChars)
            {
                problem = TooLong(target.Length);
                return null;
            }

            user = header
                + (doc.Length == 0 ? "CONTENU ACTUEL : (fichier vide)\n\n" : $"CONTENU ACTUEL :\n{Fenced(doc)}\n\n")
                + $"DEMANDE : {instruction}\n\n"
                + "Réponds par le fichier COMPLET modifié dans UN SEUL bloc de code, sans numéros de ligne, sans explication. N'abrège rien.";
        }
        else
        {
            target = InlineEditPlanner.Normalize(request.Selection!);
            if (target.Length > InlineEditPlanner.MaxTargetChars)
            {
                problem = TooLong(target.Length);
                return null;
            }

            var at = doc.IndexOf(target, StringComparison.Ordinal);
            var before = at < 0 ? string.Empty : TailLines(doc[..at], LinesBefore);
            var after = at < 0 ? string.Empty : HeadLines(doc[(at + target.Length)..], LinesAfter);

            user = header
                + (before.Length > 0 ? $"Contexte AVANT le passage (à ne pas modifier, à ne pas recopier) :\n{Fenced(before)}\n\n" : string.Empty)
                + $"PASSAGE À MODIFIER :\n{Fenced(target)}\n\n"
                + (after.Length > 0 ? $"Contexte APRÈS le passage (à ne pas modifier, à ne pas recopier) :\n{Fenced(after)}\n\n" : string.Empty)
                + $"DEMANDE : {instruction}\n\n"
                + "Réponds par le passage modifié EN ENTIER dans UN SEUL bloc de code, sans numéros de ligne, sans explication.";
        }

        var promptTokens = EstimateTokens(SystemText) + EstimateTokens(user) + 40; // 40 : balises du gabarit de conversation
        var maxOutput = EstimateTokens(target) * 2 + 400;                          // une réécriture peut grossir, pas au-delà du double
        var needed = promptTokens + maxOutput + 128;

        var small = Math.Min(SmallContext, maxContext);
        var numCtx = needed <= small ? small : maxContext >= LargeContext && needed <= LargeContext ? LargeContext : 0;
        if (numCtx == 0)
        {
            problem = $"Cette demande est trop grosse pour la mémoire de travail du modèle (environ {needed} jetons, il en accepte {maxContext}) : sélectionne un passage plus court.";
            return null;
        }

        return new InlineEditPrompt(SystemText, user, Math.Min(maxOutput, numCtx - promptTokens - 64), numCtx);
    }

    /// <summary>
    /// ★ AJOUT (25/09, confidentialité) : ce que <see cref="Build"/> met dans le message, dit en clair — pour prévenir l'utilisateur AVANT
    /// qu'une demande parte vers un service en ligne. À garder d'accord avec Build (fichier entier, ou passage + lignes voisines).
    /// </summary>
    public static string DescribeSentContent(InlineEditRequest request)
    {
        var name = string.IsNullOrWhiteSpace(request.DisplayPath) ? "sans nom" : Path.GetFileName(request.DisplayPath);
        if (request.Scope == InlineEditScope.WholeFile)
            return $"tout le fichier « {name} » ({CountLines(request.DocumentText)} ligne(s)) et ta demande";

        return $"le passage sélectionné ({CountLines(request.Selection!)} ligne(s)), jusqu'à {LinesBefore} lignes avant lui et {LinesAfter} après, "
             + $"le nom du fichier « {name} » et ta demande";
    }

    // ── Aides ───────────────────────────────────────────────────────────────

    private static int CountLines(string text)
    {
        var normalized = InlineEditPlanner.Normalize(text).TrimEnd('\n');
        return normalized.Length == 0 ? 0 : normalized.Split('\n').Length;
    }

    private static string TooLong(int chars)
        => $"Ce texte est long ({chars} caractères) : un modèle local ne le réécrit pas d'un bloc de façon fiable. "
         + "Sélectionne le passage à modifier, ou confie le travail à l'agent (« /agent … »), qui modifie le fichier par petites touches.";

    /// <summary>Le texte dans un bloc de code — plus long qu'à l'ordinaire si le texte contient lui-même des « ``` » (fichier Markdown).</summary>
    internal static string Fenced(string text)
    {
        var fence = "```";
        while (text.Contains(fence, StringComparison.Ordinal)) fence += "`";
        return $"{fence}\n{text}\n{fence}";
    }

    private static string TailLines(string text, int lines)
    {
        var all = text.TrimEnd('\n', ' ', '\t').Split('\n');
        var slice = string.Join("\n", all.Length > lines ? all[^lines..] : all);
        return (slice.Length > MaxContextChars ? slice[^MaxContextChars..] : slice).Trim('\n');
    }

    private static string HeadLines(string text, int lines)
    {
        var all = text.TrimStart('\n', ' ', '\t').Split('\n');
        var slice = string.Join("\n", all.Length > lines ? all[..lines] : all);
        return (slice.Length > MaxContextChars ? slice[..MaxContextChars] : slice).Trim('\n');
    }
}
