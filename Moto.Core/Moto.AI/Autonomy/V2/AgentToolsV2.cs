// Moto.Core/Moto.AI/Autonomy/V2/AgentToolsV2.cs
// ★ AJOUT (24/09, agent v2) : les 8 outils. Trois lisent (list_dir, read_file, search_text — jamais de
// confirmation), quatre modifient (edit_file, insert_lines, write_file, run_command — chacun PASSE par la
// confirmation humaine de la boucle, avec un diff pour les fichiers), un termine (finish).
// Les limites de taille ne sont pas cosmétiques : le contexte du modèle est petit (16 k jetons par
// défaut), un fichier lu en entier le sature — read_file lit donc par tranches numérotées.
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Moto.Core.AI.Autonomy.V2;

internal static class ToolFs
{
    public static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", ".idea", "bin", "obj", "node_modules", "packages", "dist", "__pycache__",
    };

    public static string Shorten(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    public static bool LooksBinary(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var buffer = new byte[Math.Min(4096, fs.Length)];
            var read = fs.Read(buffer, 0, buffer.Length);
            return Array.IndexOf(buffer, (byte)0, 0, read) >= 0;
        }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }

    /// <summary>Lignes numérotées « 12 | code » (numéros alignés).</summary>
    public static string Numbered(IReadOnlyList<string> lines, int firstLineNumber)
    {
        var width = (firstLineNumber + lines.Count - 1).ToString().Length;
        var sb = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
            sb.Append((firstLineNumber + i).ToString().PadLeft(width)).Append(" | ").Append(lines[i]).Append('\n');
        return sb.ToString().TrimEnd('\n');
    }
}

// ── Lecture ────────────────────────────────────────────────────────────────

public sealed class ListDirToolV2 : AgentToolV2
{
    private const int MaxEntries = 200;

    public override string Name => "list_dir";
    public override string Description =>
        "Liste le contenu d'un dossier du projet (dossiers d'abord). Sert à explorer avant de lire ou modifier.";
    public override JsonObject Parameters => Schema(new JsonObject
    {
        ["path"] = Prop("string", "Dossier relatif au projet. Vide ou « . » = racine du projet."),
        ["depth"] = Prop("integer", "Profondeur (1 à 3, défaut 1)."),
    });

    public override Task<ToolResult> ExecuteAsync(JsonObject args, AgentToolContext ctx, CancellationToken ct)
    {
        string dir;
        try { dir = ctx.Resolve(ToolArgs.Str(args, "path"), allowRoot: true); }
        catch (ToolPathException ex) { return Task.FromResult(ToolResult.Error(ex.Message)); }

        if (File.Exists(dir))
            return Task.FromResult(ToolResult.Error($"« {ctx.Display(dir)} » est un fichier : utilise read_file."));
        if (!Directory.Exists(dir))
            return Task.FromResult(ToolResult.Error($"Dossier introuvable : {ToolArgs.Str(args, "path")}. Commence par list_dir sans chemin pour voir la racine du projet."));

        var depth = Math.Clamp(ToolArgs.Int(args, "depth") ?? 1, 1, 3);
        var sb = new StringBuilder();
        var count = 0;
        sb.AppendLine($"{(dir == ctx.Root ? "(racine du projet)" : ctx.Display(dir) + "/")}");
        Walk(dir, 1, depth, sb, ref count);
        if (count >= MaxEntries) sb.AppendLine($"… (limité à {MaxEntries} entrées : précise un sous-dossier)");
        return Task.FromResult(ToolResult.Ok(sb.ToString().TrimEnd()));
    }

    private static void Walk(string dir, int level, int maxDepth, StringBuilder sb, ref int count)
    {
        IEnumerable<string> dirs, files;
        try
        {
            dirs = Directory.EnumerateDirectories(dir).Where(d => !ToolFs.IgnoredFolders.Contains(Path.GetFileName(d)))
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToList();
            files = Directory.EnumerateFiles(dir).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (UnauthorizedAccessException) { return; }
        catch (IOException) { return; }

        var indent = new string(' ', level * 2);
        foreach (var d in dirs)
        {
            if (count >= MaxEntries) return;
            sb.AppendLine($"{indent}{Path.GetFileName(d)}/");
            count++;
            if (level < maxDepth) Walk(d, level + 1, maxDepth, sb, ref count);
        }
        foreach (var f in files)
        {
            if (count >= MaxEntries) return;
            long size;
            try { size = new FileInfo(f).Length; } catch (IOException) { size = 0; }
            sb.AppendLine($"{indent}{Path.GetFileName(f)} ({(size < 1024 ? size + " o" : (size / 1024) + " Ko")})");
            count++;
        }
    }
}

public sealed class ReadFileToolV2 : AgentToolV2
{
    public const int MaxLinesPerCall = 300;
    private const int MaxCharsPerCall = 14_000;
    private const long MaxFileBytes = 4_000_000;

    public override string Name => "read_file";
    public override string Description =>
        "Lit un fichier texte par tranche, avec les numéros de ligne. Lis TOUJOURS avant de modifier. " +
        "Pour un long fichier, lis d'abord le début puis demande la tranche suivante avec start_line.";
    public override JsonObject Parameters => Schema(new JsonObject
    {
        ["path"] = Prop("string", "Fichier relatif au projet, par exemple Dossier/Fichier.cs"),
        ["start_line"] = Prop("integer", "Première ligne à lire (défaut 1)."),
        ["end_line"] = Prop("integer", $"Dernière ligne à lire (défaut : {MaxLinesPerCall} lignes après start_line)."),
    }, "path");

    public override Task<ToolResult> ExecuteAsync(JsonObject args, AgentToolContext ctx, CancellationToken ct)
    {
        string full;
        try { full = ctx.Resolve(ToolArgs.Str(args, "path")); }
        catch (ToolPathException ex) { return Task.FromResult(ToolResult.Error(ex.Message)); }

        var display = ctx.Display(full);
        if (Directory.Exists(full))
            return Task.FromResult(ToolResult.Error($"« {display} » est un dossier : utilise list_dir."));
        if (!File.Exists(full))
            return Task.FromResult(ToolResult.Error($"Fichier introuvable : {display}. Utilise list_dir ou search_text pour retrouver son chemin."));
        if (new FileInfo(full).Length > MaxFileBytes)
            return Task.FromResult(ToolResult.Error($"« {display} » est trop gros pour être lu ({new FileInfo(full).Length / 1_000_000} Mo)."));

        TextFile file;
        try { file = TextFile.Load(full); }
        catch (InvalidDataException ex) { return Task.FromResult(ToolResult.Error(ex.Message)); }
        catch (IOException ex) { return Task.FromResult(ToolResult.Error($"Lecture impossible : {ex.Message}")); }

        var lines = LineDiff.SplitLines(file.Text);
        if (lines.Length == 0) return Task.FromResult(ToolResult.Ok($"{display} : fichier vide."));

        var start = Math.Max(1, ToolArgs.Int(args, "start_line") ?? 1);
        if (start > lines.Length)
            return Task.FromResult(ToolResult.Error($"{display} n'a que {lines.Length} lignes (start_line={start})."));

        var end = Math.Min(lines.Length, ToolArgs.Int(args, "end_line") ?? (start + MaxLinesPerCall - 1));
        end = Math.Min(end, start + MaxLinesPerCall - 1);
        if (end < start) end = start;

        // Plafond en caractères : quelques lignes très longues ne doivent pas saturer le contexte.
        var chosen = new List<string>();
        var chars = 0;
        for (var i = start - 1; i < end; i++)
        {
            chars += lines[i].Length + 8;
            if (chars > MaxCharsPerCall && chosen.Count > 0) { end = i; break; }
            chosen.Add(lines[i]);
        }
        end = start + chosen.Count - 1;

        var sb = new StringBuilder();
        sb.AppendLine($"{display} — lignes {start}–{end} sur {lines.Length}");
        sb.AppendLine(ToolFs.Numbered(chosen, start));
        if (end < lines.Length)
            sb.AppendLine($"… il reste {lines.Length - end} lignes : appelle read_file avec start_line={end + 1} pour la suite.");
        return Task.FromResult(ToolResult.Ok(sb.ToString().TrimEnd()));
    }
}

public sealed class SearchTextToolV2 : AgentToolV2
{
    private const int MaxMatches = 60;
    private const int MaxFiles = 4000;
    private const long MaxFileBytes = 1_500_000;

    public override string Name => "search_text";
    public override string Description =>
        "Cherche un texte dans les fichiers du projet (insensible à la casse) : renvoie « fichier:ligne: texte ». " +
        "Pratique pour trouver où une méthode ou une classe est définie ou utilisée.";
    public override JsonObject Parameters => Schema(new JsonObject
    {
        ["query"] = Prop("string", "Texte à chercher."),
        ["path"] = Prop("string", "Dossier OU fichier où chercher (défaut : tout le projet)."),
        ["file_glob"] = Prop("string", "Filtre sur le nom de fichier, par exemple *.cs"),
        ["is_regex"] = Prop("boolean", "Vrai si query est une expression régulière."),
    }, "query");

    public override async Task<ToolResult> ExecuteAsync(JsonObject args, AgentToolContext ctx, CancellationToken ct)
    {
        var query = ToolArgs.Str(args, "query");
        if (string.IsNullOrEmpty(query)) return ToolResult.Error("Le paramètre « query » est obligatoire.");

        string dir;
        try { dir = ctx.Resolve(ToolArgs.Str(args, "path"), allowRoot: true); }
        catch (ToolPathException ex) { return ToolResult.Error(ex.Message); }

        // Un chemin de FICHIER est accepté : la recherche se limite à ce fichier (constat du banc d'essai : les modèles le font).
        var singleFile = File.Exists(dir);
        if (!singleFile && !Directory.Exists(dir))
            return ToolResult.Error($"Chemin introuvable : {ToolArgs.Str(args, "path")}. Utilise list_dir pour voir les dossiers et fichiers du projet.");

        Regex? regex = null;
        if (ToolArgs.Bool(args, "is_regex"))
        {
            try { regex = new Regex(query, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2)); }
            catch (ArgumentException ex) { return ToolResult.Error($"Expression régulière invalide : {ex.Message}"); }
        }

        var glob = ToolArgs.Str(args, "file_glob");
        var matches = new List<string>();
        var filesSeen = 0;
        var truncated = false;

        foreach (var file in singleFile ? new[] { dir } : EnumerateFiles(dir))
        {
            ct.ThrowIfCancellationRequested();
            if (!singleFile && glob is { Length: > 0 } && !System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(glob, Path.GetFileName(file), ignoreCase: true))
                continue;
            if (++filesSeen > MaxFiles) { truncated = true; break; }

            FileInfo info;
            try { info = new FileInfo(file); } catch (IOException) { continue; }
            if (info.Length > MaxFileBytes || info.Length == 0 || ToolFs.LooksBinary(file)) continue;

            string[] lines;
            try { lines = await File.ReadAllLinesAsync(file, ct); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            for (var i = 0; i < lines.Length; i++)
            {
                bool hit;
                try
                {
                    hit = regex is not null
                        ? regex.IsMatch(lines[i])
                        : lines[i].Contains(query, StringComparison.OrdinalIgnoreCase);
                }
                catch (RegexMatchTimeoutException) { return ToolResult.Error("Expression régulière trop lente : simplifie-la."); }

                if (!hit) continue;
                matches.Add($"{ctx.Display(file)}:{i + 1}: {ToolFs.Shorten(lines[i].Trim(), 200)}");
                if (matches.Count >= MaxMatches) { truncated = true; break; }
            }
            if (matches.Count >= MaxMatches) break;
        }

        if (matches.Count == 0) return ToolResult.Ok($"Aucun résultat pour « {query} ».");

        var sb = new StringBuilder();
        foreach (var m in matches) sb.AppendLine(m);
        if (truncated) sb.AppendLine($"… (résultats limités à {MaxMatches} : précise path ou file_glob)");
        return ToolResult.Ok(sb.ToString().TrimEnd());
    }

    private static IEnumerable<string> EnumerateFiles(string dir)
    {
        var pending = new Stack<string>();
        pending.Push(dir);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            string[] files, subdirs;
            try
            {
                files = Directory.GetFiles(current);
                subdirs = Directory.GetDirectories(current);
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (var f in files) yield return f;

            Array.Sort(subdirs, StringComparer.OrdinalIgnoreCase);
            for (var i = subdirs.Length - 1; i >= 0; i--)
                if (!ToolFs.IgnoredFolders.Contains(Path.GetFileName(subdirs[i]))) pending.Push(subdirs[i]);
        }
    }
}

// ── Modification ───────────────────────────────────────────────────────────

public sealed class EditFileToolV2 : AgentToolV2
{
    public override string Name => "edit_file";
    public override string Description =>
        "Modifie un fichier existant en REMPLAÇANT un passage par un autre (l'outil normal pour changer du code) : " +
        "old_text doit être recopié EXACTEMENT depuis read_file (sans les numéros de ligne) et assez long pour être unique ; " +
        "new_text est le texte de remplacement. Change le minimum de lignes. Pour AJOUTER du code sans rien remplacer, utilise insert_lines.";
    public override JsonObject Parameters => Schema(new JsonObject
    {
        ["path"] = Prop("string", "Fichier relatif au projet."),
        ["old_text"] = Prop("string", "Passage exact à remplacer (plusieurs lignes possibles)."),
        ["new_text"] = Prop("string", "Texte de remplacement (chaîne vide pour supprimer le passage)."),
        ["replace_all"] = Prop("boolean", "Vrai pour remplacer toutes les occurrences (défaut faux : old_text doit être unique)."),
    }, "path", "old_text", "new_text");

    public override bool IsMutating => true;
    public override bool WritesFiles => true;

    public override Task<ToolPreparation> PrepareAsync(JsonObject args, AgentToolContext ctx, CancellationToken ct)
    {
        string full;
        try { full = ctx.Resolve(ToolArgs.Str(args, "path")); }
        catch (ToolPathException ex) { return Task.FromResult(ToolPreparation.Reject(ex.Message)); }

        var display = ctx.Display(full);
        var oldText = ToolArgs.Str(args, "old_text") ?? ToolArgs.Str(args, "old_string");
        var hasNew = args.ContainsKey("new_text") || args.ContainsKey("new_string");
        var newText = ToolArgs.Str(args, "new_text") ?? ToolArgs.Str(args, "new_string") ?? string.Empty;

        if (oldText is null)
            return Task.FromResult(ToolPreparation.Reject("Le paramètre « old_text » est obligatoire (le passage exact à remplacer)."));
        if (!hasNew)
            return Task.FromResult(ToolPreparation.Reject("Le paramètre « new_text » est obligatoire (chaîne vide pour supprimer le passage)."));
        if (Directory.Exists(full))
            return Task.FromResult(ToolPreparation.Reject($"« {display} » est un dossier."));
        if (!File.Exists(full))
            return Task.FromResult(ToolPreparation.Reject($"« {display} » n'existe pas : edit_file ne modifie que des fichiers existants. Pour en créer un, utilise write_file."));

        TextFile file;
        try { file = TextFile.Load(full); }
        catch (InvalidDataException ex) { return Task.FromResult(ToolPreparation.Reject(ex.Message)); }
        catch (IOException ex) { return Task.FromResult(ToolPreparation.Reject($"Lecture impossible : {ex.Message}")); }

        var outcome = EditMatcher.Apply(file.Text, oldText, newText, ToolArgs.Bool(args, "replace_all"));
        if (!outcome.Success) return Task.FromResult(ToolPreparation.Reject(outcome.Error!));

        var diff = LineDiff.Compute(file.Text, outcome.NewContent!);
        if (!diff.HasChanges)
            return Task.FromResult(ToolPreparation.Reject("Aucun changement : new_text est identique à old_text."));

        var summary = $"modifier {display} ({diff.Summary})";
        return Task.FromResult(ToolPreparation.Propose(new PendingChange
        {
            Title = $"Modifier {display}",
            Summary = summary,
            Details = diff.Unified,
            Kind = ApprovalKind.FileChange,
            Path = display,
            Diff = diff,
            ApplyAsync = _ => Task.FromResult(Apply(ctx, full, display, file, outcome, diff)),
        }));
    }

    private static ToolResult Apply(AgentToolContext ctx, string full, string display, TextFile before, EditOutcome outcome, DiffResult diff)
    {
        var how = outcome.Strategy == "exact" ? string.Empty : $" (old_text retrouvé {outcome.Strategy})";
        return FileEdits.ApplyToExisting(ctx, full, display, before, outcome.NewContent!, diff, outcome.FirstLine, outcome.InsertedLines, "modifié", how);
    }
}

/// <summary>Application d'une modification à un fichier EXISTANT (edit_file, insert_lines) : même garde-fous pour les deux.</summary>
internal static class FileEdits
{
    public static ToolResult ApplyToExisting(AgentToolContext ctx, string full, string display, TextFile before, string newContent,
        DiffResult diff, int firstLine, int insertedLines, string verb, string how)
    {
        try
        {
            // Le fichier peut avoir changé PENDANT l'attente de la confirmation (un autre agent, l'éditeur…).
            var now = TextFile.Load(full);
            if (now.Text != before.Text)
                return ToolResult.Error($"{display} a changé pendant que tu attendais la confirmation : relis-le avec read_file puis refais ta modification.");

            ctx.Backup.Save(full);
            before.Save(newContent);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return ToolResult.Error($"Écriture impossible pour {display} : {ex.Message}");
        }

        // Le passage modifié, avec ses NOUVEAUX numéros de ligne : le modèle n'a pas à relire le fichier
        // (et ne se base pas sur des numéros périmés pour sa modification suivante).
        var newLines = LineDiff.SplitLines(newContent);
        var from = Math.Max(1, firstLine - 2);
        var to = Math.Min(newLines.Length, firstLine + Math.Max(1, insertedLines) + 1);
        to = Math.Min(to, from + 39);
        var snippet = ToolFs.Numbered(newLines[(from - 1)..to], from);

        return ToolResult.Ok(
            $"Fichier {verb} : {display} ({diff.Summary}){how}. Passage modifié, lignes {from}–{to} :\n{snippet}",
            new ChangedFile(display, diff.Added, diff.Removed, Created: false));
    }
}

public sealed class InsertLinesToolV2 : AgentToolV2
{
    public override string Name => "insert_lines";
    public override string Description =>
        "AJOUTE du code dans un fichier existant sans rien remplacer : « text » est inséré AVANT la ligne numéro « line » " +
        "(numéros affichés par read_file). Pour ajouter une méthode à la fin d'une classe, donne le numéro de la dernière accolade « } » de la classe ; " +
        "pour ajouter à la toute fin du fichier, donne le nombre de lignes + 1. Mets l'indentation voulue dans text.";
    public override JsonObject Parameters => Schema(new JsonObject
    {
        ["path"] = Prop("string", "Fichier relatif au projet."),
        ["line"] = Prop("integer", "Numéro de la ligne AVANT laquelle insérer (1 = tout début du fichier)."),
        ["text"] = Prop("string", "Lignes à insérer (avec leur indentation)."),
    }, "path", "line", "text");

    public override bool IsMutating => true;
    public override bool WritesFiles => true;

    public override Task<ToolPreparation> PrepareAsync(JsonObject args, AgentToolContext ctx, CancellationToken ct)
    {
        string full;
        try { full = ctx.Resolve(ToolArgs.Str(args, "path")); }
        catch (ToolPathException ex) { return Task.FromResult(ToolPreparation.Reject(ex.Message)); }

        var display = ctx.Display(full);
        var text = ToolArgs.Str(args, "text");
        var at = ToolArgs.Int(args, "line");

        if (at is null) return Task.FromResult(ToolPreparation.Reject("Le paramètre « line » est obligatoire (numéro de la ligne AVANT laquelle insérer)."));
        if (string.IsNullOrWhiteSpace(text)) return Task.FromResult(ToolPreparation.Reject("Le paramètre « text » est obligatoire (les lignes à insérer)."));
        if (Directory.Exists(full)) return Task.FromResult(ToolPreparation.Reject($"« {display} » est un dossier."));
        if (!File.Exists(full))
            return Task.FromResult(ToolPreparation.Reject($"« {display} » n'existe pas : insert_lines ne modifie que des fichiers existants. Pour en créer un, utilise write_file."));

        TextFile file;
        try { file = TextFile.Load(full); }
        catch (InvalidDataException ex) { return Task.FromResult(ToolPreparation.Reject(ex.Message)); }
        catch (IOException ex) { return Task.FromResult(ToolPreparation.Reject($"Lecture impossible : {ex.Message}")); }

        var lines = LineDiff.SplitLines(file.Text).ToList();
        if (at < 1 || at > lines.Count + 1)
            return Task.FromResult(ToolPreparation.Reject($"« line » doit être entre 1 et {lines.Count + 1} ({display} a {lines.Count} lignes ; {lines.Count + 1} = ajouter à la fin)."));

        // Garde-fou C#/Java : pas entre une signature et son « { », pas en dehors de la classe, accolades équilibrées.
        if (InsertionGuard.Check(display, lines, at.Value, text) is { } refusal)
            return Task.FromResult(ToolPreparation.Reject(refusal));

        var inserted = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        lines.InsertRange(at.Value - 1, inserted);
        var newContent = string.Join('\n', lines) + (file.Text.EndsWith('\n') || file.Text.Length == 0 ? "\n" : string.Empty);

        var diff = LineDiff.Compute(file.Text, newContent);
        var atLine = at.Value;
        return Task.FromResult(ToolPreparation.Propose(new PendingChange
        {
            Title = $"Ajouter du code dans {display}",
            Summary = $"insérer {inserted.Length} ligne(s) dans {display} avant la ligne {atLine} ({diff.Summary})",
            Details = diff.Unified,
            Kind = ApprovalKind.FileChange,
            Path = display,
            Diff = diff,
            ApplyAsync = _ => Task.FromResult(FileEdits.ApplyToExisting(ctx, full, display, file, newContent, diff, atLine, inserted.Length, "modifié", $" (insertion avant la ligne {atLine})")),
        }));
    }
}

public sealed class WriteFileToolV2 : AgentToolV2
{
    private const int MaxChars = 300_000;

    public override string Name => "write_file";
    public override string Description =>
        "Crée un NOUVEAU fichier avec ce contenu complet (les dossiers manquants sont créés). " +
        "Pour changer un fichier existant, utilise edit_file : write_file remplace tout le contenu et est refusé si le résultat est beaucoup plus court.";
    public override JsonObject Parameters => Schema(new JsonObject
    {
        ["path"] = Prop("string", "Fichier relatif au projet."),
        ["content"] = Prop("string", "Contenu complet du fichier."),
    }, "path", "content");

    public override bool IsMutating => true;
    public override bool WritesFiles => true;

    public override Task<ToolPreparation> PrepareAsync(JsonObject args, AgentToolContext ctx, CancellationToken ct)
    {
        string full;
        try { full = ctx.Resolve(ToolArgs.Str(args, "path")); }
        catch (ToolPathException ex) { return Task.FromResult(ToolPreparation.Reject(ex.Message)); }

        var display = ctx.Display(full);
        if (!args.ContainsKey("content"))
            return Task.FromResult(ToolPreparation.Reject("Le paramètre « content » est obligatoire (le contenu complet du fichier)."));

        var content = (ToolArgs.Str(args, "content") ?? string.Empty).Replace("\r\n", "\n");
        if (content.Length > MaxChars)
            return Task.FromResult(ToolPreparation.Reject($"Contenu trop long ({content.Length} caractères, maximum {MaxChars}) : découpe en plusieurs fichiers."));
        if (Directory.Exists(full))
            return Task.FromResult(ToolPreparation.Reject($"« {display} » est un dossier."));

        TextFile file;
        string oldText;
        var created = !File.Exists(full);
        if (created)
        {
            file = TextFile.CreateNew(full, content, ctx.Root);
            oldText = string.Empty;
        }
        else
        {
            try { file = TextFile.Load(full); }
            catch (InvalidDataException ex) { return Task.FromResult(ToolPreparation.Reject(ex.Message)); }
            catch (IOException ex) { return Task.FromResult(ToolPreparation.Reject($"Lecture impossible : {ex.Message}")); }
            oldText = file.Text;

            var oldCount = LineDiff.SplitLines(oldText).Length;
            var newCount = LineDiff.SplitLines(content).Length;
            if (oldCount >= 30 && newCount < oldCount * 0.5)
                return Task.FromResult(ToolPreparation.Reject(
                    $"Refusé : {display} a {oldCount} lignes et ta version n'en a que {newCount} — ça ressemble à un fichier tronqué. " +
                    "Pour changer une partie, utilise edit_file (old_text = le passage, new_text = son remplacement)."));
        }

        var diff = LineDiff.Compute(oldText, content);
        if (!created && !diff.HasChanges)
            return Task.FromResult(ToolPreparation.Reject("Aucun changement : le fichier a déjà exactement ce contenu."));

        return Task.FromResult(ToolPreparation.Propose(new PendingChange
        {
            Title = created ? $"Créer {display}" : $"Réécrire {display}",
            Summary = created ? $"créer {display} ({diff.Added} lignes)" : $"réécrire {display} ({diff.Summary})",
            Details = diff.Unified,
            Kind = ApprovalKind.FileChange,
            Path = display,
            Diff = diff,
            IsDestructive = !created,
            ApplyAsync = _ => Task.FromResult(Apply(ctx, full, display, file, oldText, content, created, diff)),
        }));
    }

    private static ToolResult Apply(AgentToolContext ctx, string full, string display, TextFile file, string expectedBefore,
        string content, bool created, DiffResult diff)
    {
        try
        {
            if (created && File.Exists(full))
                return ToolResult.Error($"{display} a été créé entre-temps : relis-le avec read_file, puis utilise edit_file.");
            if (!created && TextFile.Load(full).Text != expectedBefore)
                return ToolResult.Error($"{display} a changé pendant que tu attendais la confirmation : relis-le avec read_file.");

            ctx.Backup.Save(full);
            file.Save(content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return ToolResult.Error($"Écriture impossible pour {display} : {ex.Message}");
        }

        return ToolResult.Ok(
            created ? $"Fichier créé : {display} ({diff.Added} lignes)." : $"Fichier réécrit : {display} ({diff.Summary}).",
            new ChangedFile(display, diff.Added, diff.Removed, created));
    }
}

public sealed class RunCommandToolV2 : AgentToolV2
{
    public override string Name => "run_command";
    public override string Description =>
        "Exécute une commande dans le dossier du projet (compilation, tests) et renvoie le résultat résumé : " +
        "les lignes d'erreur d'abord. L'utilisateur doit autoriser chaque commande.";
    public override JsonObject Parameters => Schema(new JsonObject
    {
        ["command"] = Prop("string", "Commande à lancer, sur une seule ligne (sans &&, | ni ;). Utilise celle indiquée dans la demande."),
        ["timeout_seconds"] = Prop("integer", "Délai maximal en secondes (10 à 900, défaut 180)."),
    }, "command");

    public override bool IsMutating => true;

    public override Task<ToolPreparation> PrepareAsync(JsonObject args, AgentToolContext ctx, CancellationToken ct)
    {
        var command = ToolArgs.Str(args, "command")?.Trim();
        if (string.IsNullOrEmpty(command))
            return Task.FromResult(ToolPreparation.Reject("Le paramètre « command » est obligatoire."));
        if (command.Length > 600)
            return Task.FromResult(ToolPreparation.Reject("Commande trop longue (600 caractères maximum)."));

        var timeout = Math.Clamp(ToolArgs.Int(args, "timeout_seconds") ?? 180, 10, 900);
        var hint = BackgroundAgentLoop.DangerousCommandHint(command);
        var details = $"Commande : {command}\nDossier : {ctx.Root}\nDélai maximal : {timeout} s" + (hint is null ? string.Empty : $"\n\n{hint}");

        return Task.FromResult(ToolPreparation.Propose(new PendingChange
        {
            Title = "Exécuter une commande",
            Summary = $"exécuter : {ToolFs.Shorten(command, 80)}",
            Details = details,
            Kind = ApprovalKind.Command,
            IsDestructive = true,
            ApplyAsync = token => RunAsync(ctx, command, timeout, token),
        }));
    }

    private static async Task<ToolResult> RunAsync(AgentToolContext ctx, string command, int timeoutSeconds, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var result = await ctx.RunCommand(command, ctx.Root, cts.Token);
        var digest = DigestOutput(result.Output.Replace(ctx.Root + Path.DirectorySeparatorChar, string.Empty, StringComparison.OrdinalIgnoreCase),
            result.Error.Replace(ctx.Root + Path.DirectorySeparatorChar, string.Empty, StringComparison.OrdinalIgnoreCase));

        // Un code de sortie non nul (build qui échoue…) est une INFORMATION pour le modèle, pas une erreur d'outil.
        return new ToolResult(false, $"Commande : {command}\nCode de sortie : {result.ExitCode}\n{digest}", null, result.ExitCode);
    }

    private static readonly Regex ErrorLine = new(
        @"\berror\s+[A-Za-z]+\d+|: error |^\s*(error|erreur)\b|Traceback|Exception:|\bFAILED\b|Échec|\bfailed\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Résumé lisible : les lignes d'erreur (dédoublonnées) d'abord, sinon la fin de la sortie.</summary>
    internal static string DigestOutput(string stdout, string stderr, int maxChars = 3500)
    {
        var lines = (stdout + "\n" + stderr).Replace("\r\n", "\n").Split('\n')
            .Select(l => l.TrimEnd()).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0) return "(aucune sortie)";

        var errors = lines.Where(l => ErrorLine.IsMatch(l)).Distinct().ToList();
        var sb = new StringBuilder();
        if (errors.Count > 0)
        {
            sb.AppendLine($"{errors.Count} ligne(s) d'erreur :");
            foreach (var e in errors.Take(25)) sb.AppendLine(ToolFs.Shorten(e, 300));
            if (errors.Count > 25) sb.AppendLine($"… et {errors.Count - 25} autres.");
            // La fin de la sortie (résumé « X erreurs, Y avertissements »), sans répéter les erreurs déjà listées.
            var tail = lines.Where(l => !errors.Contains(l)).TakeLast(4).ToList();
            if (tail.Count > 0)
            {
                sb.AppendLine("--- fin de la sortie ---");
                foreach (var l in tail) sb.AppendLine(ToolFs.Shorten(l, 200));
            }
        }
        else
        {
            foreach (var l in lines.TakeLast(30)) sb.AppendLine(ToolFs.Shorten(l, 300));
        }

        var text = sb.ToString().TrimEnd();
        return text.Length > maxChars ? text[..maxChars] + "\n… (tronqué)" : text;
    }
}

public sealed class FinishToolV2 : AgentToolV2
{
    public override string Name => "finish";
    public override string Description =>
        "Termine la tâche. À appeler quand l'objectif est atteint (ou impossible), avec un résumé court en français de ce qui a été fait.";
    public override JsonObject Parameters => Schema(new JsonObject
    {
        ["summary"] = Prop("string", "Ce qui a été fait, en une à trois phrases."),
    }, "summary");

    public override Task<ToolResult> ExecuteAsync(JsonObject args, AgentToolContext ctx, CancellationToken ct)
        => Task.FromResult(ToolResult.Ok(ToolArgs.Str(args, "summary") is { Length: > 0 } s ? s : "Terminé."));
}

public static class AgentToolSetV2
{
    public const string FinishName = "finish";

    /// <summary>Les 7 outils, dans l'ordre présenté au modèle.</summary>
    public static IReadOnlyList<AgentToolV2> Default() => new AgentToolV2[]
    {
        new ListDirToolV2(),
        new ReadFileToolV2(),
        new SearchTextToolV2(),
        new EditFileToolV2(),
        new InsertLinesToolV2(),
        new WriteFileToolV2(),
        new RunCommandToolV2(),
        new FinishToolV2(),
    };
}
