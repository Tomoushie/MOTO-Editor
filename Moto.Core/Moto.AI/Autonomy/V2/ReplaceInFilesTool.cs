// Moto.Core/Moto.AI/Autonomy/V2/ReplaceInFilesTool.cs
// ★ AJOUT (24/09, agent v2) : « remplace ce mot partout » en UNE opération, avec UN diff global et UNE confirmation.
// Constat du banc d'essai : pour renommer une méthode utilisée dans 3 fichiers, un modèle local de 7-8 milliards de
// paramètres enchaîne 4 edit_file, recopie un long passage avec une indentation fausse, se trompe d'un caractère et
// abandonne — 0 réussite sur 4 essais avec 3 modèles. L'outil retire ce travail au modèle : il donne l'ancien et le
// nouveau nom, l'outil trouve toutes les occurrences (mots entiers, casse respectée) et l'utilisateur voit tout.
// Même règle de sécurité que les autres outils : PrepareAsync ne touche à rien, l'écriture n'a lieu qu'après accord.
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Moto.Core.AI.Autonomy.V2;

public sealed class ReplaceInFilesToolV2 : AgentToolV2
{
    public const string ToolName = "replace_in_files";

    private const int MaxFiles = 40;
    private const int MaxOccurrences = 500;
    private const int MaxTextLength = 200;
    private const int MaxScannedFiles = 4000;
    private const long MaxFileBytes = 1_500_000;
    private const int MaxListedInResult = 12;

    public override string Name => ToolName;

    public override string Description =>
        "Remplace un texte par un autre dans PLUSIEURS fichiers en une seule opération : renommer une méthode, une classe, une variable " +
        "ou une constante partout (définition et appels). old_text = le texte exact (un nom, sur une seule ligne) ; new_text = le remplaçant. " +
        "Seuls les MOTS ENTIERS sont remplacés et la casse est respectée. L'utilisateur voit toutes les modifications avant d'accepter. " +
        "Préfère cet outil à plusieurs edit_file pour un renommage.";

    public override JsonObject Parameters => Schema(new JsonObject
    {
        ["old_text"] = Prop("string", "Texte exact à remplacer (sur une seule ligne), par exemple le nom actuel de la méthode."),
        ["new_text"] = Prop("string", "Texte de remplacement, par exemple le nouveau nom."),
        ["path"] = Prop("string", "Dossier OU fichier où remplacer (défaut : tout le projet)."),
        ["file_glob"] = Prop("string", "Filtre sur le nom des fichiers, par exemple *.cs (défaut : tous les fichiers texte)."),
        ["whole_word"] = Prop("boolean", "Vrai (défaut) : mots entiers seulement. Faux : remplace aussi à l'intérieur d'un mot plus long."),
    }, "old_text", "new_text");

    public override bool IsMutating => true;
    public override bool WritesFiles => true;

    private sealed record FilePlan(string Full, string Display, TextFile Before, string NewContent, DiffResult Diff, int Occurrences);

    public override Task<ToolPreparation> PrepareAsync(JsonObject args, AgentToolContext ctx, CancellationToken ct)
    {
        var oldText = ToolArgs.Str(args, "old_text") ?? ToolArgs.Str(args, "old_string");
        var hasNew = args.ContainsKey("new_text") || args.ContainsKey("new_string");
        var newText = (ToolArgs.Str(args, "new_text") ?? ToolArgs.Str(args, "new_string") ?? string.Empty).Replace("\r\n", "\n");

        if (string.IsNullOrEmpty(oldText))
            return Reject("Le paramètre « old_text » est obligatoire (le texte à remplacer).");
        if (!hasNew)
            return Reject("Le paramètre « new_text » est obligatoire (le texte de remplacement).");
        if (oldText == newText)
            return Reject("Aucun changement : new_text est identique à old_text.");
        if (oldText.Contains('\n') || oldText.Contains('\r'))
            return Reject("old_text doit tenir sur UNE ligne (un nom, une expression). Pour remplacer un passage de plusieurs lignes, utilise edit_file.");
        if (oldText.Length > MaxTextLength)
            return Reject($"old_text est trop long ({oldText.Length} caractères, maximum {MaxTextLength}) : donne seulement le nom à remplacer.");

        string dir;
        try { dir = ctx.Resolve(ToolArgs.Str(args, "path"), allowRoot: true); }
        catch (ToolPathException ex) { return Reject(ex.Message); }

        var singleFile = File.Exists(dir);
        if (!singleFile && !Directory.Exists(dir))
            return Reject($"Chemin introuvable : {ToolArgs.Str(args, "path")}. Utilise list_dir pour voir les dossiers et fichiers du projet.");

        var regex = BuildRegex(oldText, ToolArgs.Bool(args, "whole_word", fallback: true));
        var glob = ToolArgs.Str(args, "file_glob");

        var plans = new List<FilePlan>();
        var occurrences = 0;
        var scanned = 0;
        var skipped = 0;
        var truncated = false;

        try
        {
            foreach (var file in singleFile ? new[] { dir } : ToolFs.EnumerateFiles(dir))
            {
                ct.ThrowIfCancellationRequested();
                if (!singleFile && glob is { Length: > 0 }
                    && !System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(glob, Path.GetFileName(file), ignoreCase: true))
                    continue;
                if (++scanned > MaxScannedFiles) { truncated = true; break; }

                if (!TryLoad(file, out var text, out var wasSkipped))
                {
                    if (wasSkipped) skipped++;
                    continue;
                }

                var count = 0;
                var replaced = regex.Replace(text.Text, _ => { count++; return newText; });
                if (count == 0) continue;

                var diff = LineDiff.Compute(text.Text, replaced);
                if (!diff.HasChanges) continue;
                if (FileSanity.Check(ctx.Display(file), text.Text, replaced) is { } broken)
                    return Reject(broken);

                plans.Add(new FilePlan(file, ctx.Display(file), text, replaced, diff, count));
                occurrences += count;
                if (plans.Count > MaxFiles || occurrences > MaxOccurrences)
                    return Reject($"Trop de changements d'un coup (plus de {MaxFiles} fichiers ou {MaxOccurrences} occurrences) : " +
                                  "restreins l'opération avec path ou file_glob, ou vérifie que old_text est bien le nom voulu.");
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return Reject("Recherche trop lente : simplifie old_text.");
        }

        if (plans.Count == 0)
        {
            var hint = ToolArgs.Bool(args, "whole_word", fallback: true)
                ? " (mots entiers, casse respectée). Vérifie l'orthographe exacte avec search_text, ou mets whole_word à faux."
                : " (casse respectée). Vérifie l'orthographe exacte avec search_text.";
            return Reject($"Aucune occurrence de « {oldText} » trouvée{hint}"
                          + (skipped > 0 ? $" {skipped} fichier(s) binaires ou pas en UTF-8 ont été ignorés." : string.Empty));
        }

        var added = plans.Sum(p => p.Diff.Added);
        var removed = plans.Sum(p => p.Diff.Removed);
        var summary = $"remplacer « {ToolFs.Shorten(oldText, 40)} » par « {ToolFs.Shorten(newText, 40)} » : " +
                      $"{occurrences} occurrence(s) dans {plans.Count} fichier(s) (+{added} −{removed})";

        var details = new StringBuilder();
        details.AppendLine($"Remplacer « {oldText} » par « {newText} » — {occurrences} occurrence(s) dans {plans.Count} fichier(s).");
        if (truncated) details.AppendLine($"(recherche arrêtée après {MaxScannedFiles} fichiers)");
        if (skipped > 0) details.AppendLine($"({skipped} fichier(s) binaires ou pas en UTF-8 ignorés)");
        foreach (var p in plans)
        {
            details.AppendLine();
            details.AppendLine($"── {p.Display} ({p.Occurrences} occurrence(s), {p.Diff.Summary})");
            details.Append(p.Diff.Unified);
        }

        var planList = plans;
        return Task.FromResult(ToolPreparation.Propose(new PendingChange
        {
            Title = $"Remplacer « {ToolFs.Shorten(oldText, 30)} » par « {ToolFs.Shorten(newText, 30)} » ({plans.Count} fichier(s))",
            Summary = summary,
            Details = details.ToString().TrimEnd(),
            Kind = ApprovalKind.FileChange,
            Path = plans.Count == 1 ? plans[0].Display : null,
            Diff = new DiffResult(details.ToString(), added, removed),
            ApplyAsync = token => Task.FromResult(Apply(ctx, planList, oldText, newText, regex, token)),
        }));
    }

    private static Task<ToolPreparation> Reject(string message) => Task.FromResult(ToolPreparation.Reject(message));

    /// <summary>Mots entiers (ni lettre, ni chiffre, ni « _ » collé de chaque côté) quand old_text commence et finit par un caractère de mot.</summary>
    internal static Regex BuildRegex(string oldText, bool wholeWord)
    {
        var escaped = Regex.Escape(oldText);
        var canBeWhole = wholeWord && IsWordChar(oldText[0]) && IsWordChar(oldText[^1]);
        var pattern = canBeWhole ? $@"(?<![\p{{L}}\p{{N}}_]){escaped}(?![\p{{L}}\p{{N}}_])" : escaped;
        return new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static bool TryLoad(string file, out TextFile text, out bool skipped)
    {
        text = null!;
        skipped = false;
        try
        {
            var info = new FileInfo(file);
            if (info.Length == 0 || info.Length > MaxFileBytes) return false;
            text = TextFile.Load(file);
            return true;
        }
        catch (InvalidDataException) { skipped = true; return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static ToolResult Apply(AgentToolContext ctx, IReadOnlyList<FilePlan> plans, string oldText, string newText, Regex regex, CancellationToken ct)
    {
        // 1. Aucun fichier ne doit avoir changé pendant l'attente de la confirmation : sinon on n'écrit RIEN.
        var stale = new List<string>();
        foreach (var p in plans)
        {
            try
            {
                if (TextFile.Load(p.Full).Text != p.Before.Text) stale.Add(p.Display);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                stale.Add(p.Display);
            }
        }
        if (stale.Count > 0)
            return ToolResult.Error($"{string.Join(", ", stale)} : le contenu a changé pendant que tu attendais la confirmation. " +
                                    "Rien n'a été modifié. Relance search_text puis refais l'opération.");

        // 2. Écriture ; si un fichier échoue, ceux déjà écrits sont remis dans leur état d'origine (tout ou rien).
        var written = new List<FilePlan>();
        try
        {
            foreach (var p in plans)
            {
                ct.ThrowIfCancellationRequested();
                ctx.Backup.Save(p.Full);
                p.Before.Save(p.NewContent);
                written.Add(p);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            foreach (var w in written)
            {
                try { w.Before.Save(w.Before.Text); }
                catch (Exception) { /* la sauvegarde du run permet de tout restaurer à la main */ }
            }
            return ToolResult.Error($"Écriture impossible : {ex.Message}. Les fichiers déjà écrits ont été remis dans leur état d'origine.");
        }

        var changed = plans.Select(p => new ChangedFile(p.Display, p.Diff.Added, p.Diff.Removed, Created: false)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"Remplacement effectué : « {oldText} » → « {newText} » — {plans.Sum(p => p.Occurrences)} occurrence(s) dans {plans.Count} fichier(s) :");
        foreach (var p in plans.Take(MaxListedInResult)) sb.AppendLine($"  {p.Display} ({p.Occurrences})");
        if (plans.Count > MaxListedInResult) sb.AppendLine($"  … et {plans.Count - MaxListedInResult} autres fichiers.");
        sb.Append(RemainingNote(ctx, regex, oldText, plans));

        return new ToolResult(false, sb.ToString().TrimEnd(), null, null, changed);
    }

    /// <summary>Occurrences qui restent ailleurs dans le projet (hors des fichiers modifiés) : évite un search_text de contrôle.</summary>
    private static string RemainingNote(AgentToolContext ctx, Regex regex, string oldText, IReadOnlyList<FilePlan> done)
    {
        var doneSet = new HashSet<string>(done.Select(p => p.Full), StringComparer.OrdinalIgnoreCase);
        var elsewhere = new List<string>();
        var scanned = 0;
        try
        {
            foreach (var file in ToolFs.EnumerateFiles(ctx.Root))
            {
                if (doneSet.Contains(file)) continue;
                if (++scanned > MaxScannedFiles) break;
                if (!TryLoad(file, out var text, out _)) continue;
                if (regex.IsMatch(text.Text)) elsewhere.Add(ctx.Display(file));
            }
        }
        catch (RegexMatchTimeoutException) { return string.Empty; }

        return elsewhere.Count == 0
            ? $"Plus aucune occurrence de « {oldText} » ailleurs dans le projet."
            : $"⚠ « {oldText} » apparaît encore dans : {string.Join(", ", elsewhere.Take(MaxListedInResult))}" +
              (elsewhere.Count > MaxListedInResult ? $" … (+{elsewhere.Count - MaxListedInResult})" : string.Empty) + ".";
    }
}
