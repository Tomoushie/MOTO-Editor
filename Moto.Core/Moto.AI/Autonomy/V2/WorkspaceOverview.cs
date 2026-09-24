// Moto.Core/Moto.AI/Autonomy/V2/WorkspaceOverview.cs
// ★ AJOUT (24/09, agent v2) : l'aperçu du projet donné à l'agent dans son PREMIER message.
// Mesuré au banc d'essai : un petit modèle qui doit d'abord deviner l'arborescence et la commande de
// compilation perd ses pas (et recopie parfois un exemple de la description d'un outil). Lui donner
// l'arbre (2 niveaux), la liste des projets .NET et LA commande de vérification évite ces détours.
using System.Text;

namespace Moto.Core.AI.Autonomy.V2;

internal sealed record WorkspaceInfo(string Tree, IReadOnlyList<string> Projects)
{
    /// <summary>Commande de compilation quand elle ne fait aucun doute (un seul projet .csproj).</summary>
    public string? SuggestedBuild
    {
        get
        {
            var csprojs = Projects.Where(p => p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).ToList();
            return csprojs.Count == 1 ? WorkspaceOverview.BuildCommand(csprojs[0]) : null;
        }
    }
}

internal static class WorkspaceOverview
{
    private static readonly HashSet<string> SkippedDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", "node_modules", ".vs", ".idea", ".vscode", "packages", "TestResults",
        ".venv", "venv", "__pycache__", ".gradle", "dist",
    };

    private static readonly string[] ProjectPatterns = { "*.csproj", "*.sln", "*.slnx" };

    private const int MaxProjectDepth = 3;
    private const int MaxProjects = 30;

    public static WorkspaceInfo Scan(string root, int maxLines = 60, int maxPerDir = 10)
    {
        try
        {
            if (!Directory.Exists(root)) return new WorkspaceInfo(string.Empty, Array.Empty<string>());
            return new WorkspaceInfo(BuildTree(root, maxLines, maxPerDir), FindProjects(root));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new WorkspaceInfo(string.Empty, Array.Empty<string>());
        }
    }

    /// <summary>« dotnet build chemin.csproj » (le chemin est mis entre guillemets s'il contient des espaces).</summary>
    public static string BuildCommand(string relativeProject)
        => "dotnet build " + (relativeProject.Contains(' ') ? $"\"{relativeProject}\"" : relativeProject);

    /// <summary>Le projet .csproj le plus proche (en remontant les dossiers) d'un fichier du projet, ou null.</summary>
    public static string? NearestProject(string root, string relativeFile)
    {
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(root, relativeFile)));
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            while (!string.IsNullOrEmpty(dir))
            {
                var found = Directory.EnumerateFiles(dir, "*.csproj").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                if (found is not null) return Path.GetRelativePath(root, found).Replace('\\', '/');

                if (string.Equals(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), rootFull, StringComparison.OrdinalIgnoreCase))
                    break;
                dir = Path.GetDirectoryName(dir);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // pas de projet trouvé : l'appelant retombe sur une commande générique
        }
        return null;
    }

    // ── Arbre ───────────────────────────────────────────────────────────────

    private sealed record Entry(string Name, string FullPath, bool IsDir);

    private static List<Entry> Children(string dir)
    {
        var list = new List<Entry>();
        try
        {
            foreach (var d in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(d);
                if (!SkippedDirs.Contains(name)) list.Add(new Entry(name, d, true));
            }
            foreach (var f in Directory.EnumerateFiles(dir))
                list.Add(new Entry(Path.GetFileName(f), f, false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // dossier illisible : listé vide
        }
        return list
            .OrderBy(e => e.IsDir ? 0 : 1)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildTree(string root, int maxLines, int maxPerDir)
    {
        var top = Children(root);
        var maxRoot = Math.Max(10, maxLines * 2 / 3);
        var shownTop = top.Take(maxRoot).ToList();

        // Les dossiers de premier niveau sont dépliés (un niveau) tant qu'il reste de la place.
        var budget = maxLines - shownTop.Count - (top.Count > maxRoot ? 1 : 0);
        var sb = new StringBuilder();
        foreach (var entry in shownTop)
        {
            if (!entry.IsDir) { sb.AppendLine(entry.Name); continue; }

            var kids = Children(entry.FullPath);
            if (kids.Count == 0) { sb.AppendLine(entry.Name + "/ (vide)"); continue; }

            var take = Math.Min(Math.Min(kids.Count, maxPerDir), Math.Max(0, budget));
            if (take == 0) { sb.AppendLine($"{entry.Name}/ ({kids.Count} éléments)"); continue; }

            sb.AppendLine(entry.Name + "/");
            foreach (var kid in kids.Take(take))
                sb.AppendLine("  " + kid.Name + (kid.IsDir ? "/" : string.Empty));
            budget -= take;
            if (kids.Count > take)
            {
                sb.AppendLine($"  … et {kids.Count - take} autres");
                budget--;
            }
        }
        if (top.Count > maxRoot) sb.AppendLine($"… et {top.Count - maxRoot} autres à la racine");
        return sb.ToString().TrimEnd();
    }

    // ── Projets .NET ────────────────────────────────────────────────────────

    private static List<string> FindProjects(string root)
    {
        var found = new List<string>();
        Collect(root, 0);
        return found
            .OrderBy(p => p.Count(c => c == '/'))
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Take(MaxProjects)
            .ToList();

        void Collect(string dir, int depth)
        {
            if (found.Count >= MaxProjects) return;
            try
            {
                foreach (var pattern in ProjectPatterns)
                    foreach (var f in Directory.EnumerateFiles(dir, pattern))
                        found.Add(Path.GetRelativePath(root, f).Replace('\\', '/'));

                if (depth >= MaxProjectDepth) return;
                foreach (var d in Directory.EnumerateDirectories(dir))
                    if (!SkippedDirs.Contains(Path.GetFileName(d))) Collect(d, depth + 1);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // dossier illisible : ignoré
            }
        }
    }
}
