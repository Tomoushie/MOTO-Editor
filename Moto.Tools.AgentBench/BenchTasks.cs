// Moto.Tools.AgentBench/BenchTasks.cs
// Les tâches du banc d'essai et leurs contrôles OBJECTIFS. Aucun contrôle ne dépend de l'avis d'un modèle :
// compilation, contenu du fichier, taille du diff, sortie du programme identique à l'original.
using System.Text.RegularExpressions;
using Moto.Core.AI.Autonomy.V2;

namespace Moto.Tools.AgentBench;

internal sealed record CheckResult(bool Passed, string Detail);

internal sealed record BenchTask(
    string Id,
    string Title,
    string Goal,
    Action<Workspace>? Setup,
    Func<Workspace, string, CheckResult> Check);

internal sealed class Workspace
{
    private readonly IReadOnlyDictionary<string, string> _original;

    public string Root { get; }

    /// <summary>Sortie de référence du programme sur le projet d'origine.</summary>
    public string BaselineOutput { get; }

    public Workspace(string root, IReadOnlyDictionary<string, string> original, string baselineOutput)
    {
        Root = root;
        _original = original;
        BaselineOutput = baselineOutput;
    }

    public string Path(string rel) => System.IO.Path.Combine(Root, rel);
    public bool Exists(string rel) => File.Exists(Path(rel));
    public string Read(string rel) => File.ReadAllText(Path(rel)).Replace("\r\n", "\n");
    public string Original(string rel) => _original[rel].Replace("\r\n", "\n");

    public IEnumerable<string> SourceFiles() => Directory
        .EnumerateFiles(Root, "*.cs", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}")
                 && !f.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}"));

    /// <summary>Diff cumulé de tous les fichiers .cs par rapport à l'original (les fichiers neufs comptent comme ajoutés).</summary>
    public (int Added, int Removed, int Files) DiffFromOriginal()
    {
        int added = 0, removed = 0, files = 0;
        foreach (var f in SourceFiles())
        {
            var rel = System.IO.Path.GetRelativePath(Root, f).Replace('\\', '/');
            var now = File.ReadAllText(f).Replace("\r\n", "\n");
            var before = _original.TryGetValue(rel, out var o) ? o.Replace("\r\n", "\n") : string.Empty;
            var d = LineDiff.Compute(before, now);
            if (!d.HasChanges) continue;
            added += d.Added; removed += d.Removed; files++;
        }
        return (added, removed, files);
    }

    /// <summary>Signature du contenu de tous les .cs : sert à prouver qu'une tâche « lecture seule » n'a rien touché.</summary>
    public string Fingerprint() => string.Join("|", SourceFiles().OrderBy(f => f, StringComparer.Ordinal)
        .Select(f => f + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f)))));

    public (bool Ok, string Output) Build()
    {
        var (exit, output) = Dotnet.Run("build --nologo -v q", Root, TimeSpan.FromMinutes(3));
        return (exit == 0, output);
    }

    /// <summary>Compile puis lance le programme : sa sortie doit rester celle d'origine pour les refactorisations.</summary>
    public (bool Ok, string Detail) BuildAndCompareOutput()
    {
        var (ok, buildOutput) = Build();
        if (!ok) return (false, "ne compile pas : " + FirstError(buildOutput));

        var (exit, output) = Dotnet.Run("run --no-build", Root, TimeSpan.FromMinutes(1));
        if (exit != 0) return (false, "le programme plante : " + output.Trim());
        return output.Replace("\r\n", "\n").Trim() == BaselineOutput.Replace("\r\n", "\n").Trim()
            ? (true, "compile, sortie identique")
            : (false, "la sortie du programme a changé");
    }

    public static string FirstError(string buildOutput)
        => buildOutput.Replace("\r\n", "\n").Split('\n').FirstOrDefault(l => l.Contains("error", StringComparison.OrdinalIgnoreCase))?.Trim() ?? "erreur inconnue";
}

internal static class BenchTasks
{
    public static IReadOnlyList<BenchTask> All { get; } = new BenchTask[]
    {
        new("add-method", "Ajouter une méthode",
            "Dans Services/InventoryService.cs, ajoute une méthode publique `int CountInStock()` qui retourne le nombre de produits dont le stock est supérieur à 0. Ne change rien d'autre.",
            null,
            (ws, _) =>
            {
                var text = ws.Read("Services/InventoryService.cs");
                if (!Regex.IsMatch(text, @"public\s+int\s+CountInStock\s*\(\s*\)")) return new(false, "méthode CountInStock absente");
                foreach (var name in new[] { "AddProduct", "FindByName", "GetById", "Restock", "Consume", "Remove", "TotalValue", "LowStock", "All" })
                    if (!Regex.IsMatch(text, $@"public\s+[\w<>?]+\s+{name}\s*\(")) return new(false, $"méthode existante perdue : {name}");

                var diff = ws.DiffFromOriginal();
                if (diff.Removed > 1) return new(false, $"trop de lignes supprimées ({diff.Removed})");
                var build = ws.BuildAndCompareOutput();
                return new(build.Ok, build.Detail + $", +{diff.Added} −{diff.Removed}");
            }),

        new("rename-across-files", "Renommer dans 3 fichiers",
            "Renomme la méthode `ComputeTax` en `CalculateTax` dans tout le projet : la définition et tous les appels.",
            null,
            (ws, _) =>
            {
                var remaining = ws.SourceFiles().Count(f => File.ReadAllText(f).Contains("ComputeTax", StringComparison.Ordinal));
                if (remaining > 0) return new(false, $"« ComputeTax » reste dans {remaining} fichier(s)");
                var count = ws.SourceFiles().Sum(f => Regex.Matches(File.ReadAllText(f), @"\bCalculateTax\b").Count);
                if (count != 4) return new(false, $"« CalculateTax » apparaît {count} fois (attendu 4)");
                var build = ws.BuildAndCompareOutput();
                return new(build.Ok, build.Detail);
            }),

        new("fix-compile-error", "Corriger une erreur de compilation",
            "Le projet ne compile plus. Compile-le, repère l'erreur dans le code et corrige-la (une seule ligne est à changer). Ne fais rien d'autre.",
            ws =>
            {
                var path = ws.Path("Services/InventoryService.cs");
                var text = File.ReadAllText(path);
                var broken = text.Replace("p.Price * p.Stock", "p.Price * p.Stok");
                if (broken == text) throw new InvalidOperationException("injection de l'erreur impossible");
                File.WriteAllText(path, broken, new System.Text.UTF8Encoding(false));
            },
            (ws, _) =>
            {
                var diff = ws.DiffFromOriginal();
                if (diff.Added > 1 || diff.Removed > 1) return new(false, $"trop de changements par rapport à l'original (+{diff.Added} −{diff.Removed})");
                var build = ws.BuildAndCompareOutput();
                return new(build.Ok, build.Detail + $", +{diff.Added} −{diff.Removed}");
            }),

        new("small-change-big-file", "Changer une constante dans un fichier de 430 lignes",
            "Dans Services/Big/LegacyCatalog.cs, la constante MaxItems vaut 100 : passe-la à 250. Ne modifie rien d'autre dans ce fichier.",
            null,
            (ws, _) =>
            {
                var text = ws.Read("Services/Big/LegacyCatalog.cs");
                if (!Regex.IsMatch(text, @"const\s+int\s+MaxItems\s*=\s*250\s*;")) return new(false, "MaxItems n'est pas à 250");
                var diff = ws.DiffFromOriginal();
                if (diff.Added != 1 || diff.Removed != 1) return new(false, $"le diff devrait être +1 −1, il est +{diff.Added} −{diff.Removed}");
                var build = ws.BuildAndCompareOutput();
                return new(build.Ok, build.Detail + ", +1 −1");
            }),

        new("new-file-interface", "Créer une classe qui implémente une interface",
            "Crée le fichier Services/ConsoleReportWriter.cs avec une classe publique ConsoleReportWriter qui implémente l'interface IReportWriter (lis l'interface dans Services/IReportWriter.cs) et écrit dans la console.",
            null,
            (ws, _) =>
            {
                if (!ws.Exists("Services/ConsoleReportWriter.cs")) return new(false, "fichier absent");
                var text = ws.Read("Services/ConsoleReportWriter.cs");
                if (!Regex.IsMatch(text, @"class\s+ConsoleReportWriter\s*:\s*IReportWriter")) return new(false, "la classe n'implémente pas IReportWriter");
                var build = ws.BuildAndCompareOutput();
                return new(build.Ok, build.Detail);
            }),

        new("add-xml-docs", "Documenter les méthodes publiques",
            "Ajoute un commentaire de documentation XML (/// avec <summary>) au-dessus de chaque méthode publique de Services/PricingService.cs. Ne change pas le code lui-même.",
            null,
            (ws, _) =>
            {
                var lines = ws.Read("Services/PricingService.cs").Split('\n');
                var methods = 0;
                for (var i = 0; i < lines.Length; i++)
                {
                    if (!Regex.IsMatch(lines[i], @"^\s*public\s+decimal\s+\w+\s*\(")) continue;
                    methods++;
                    var j = i - 1;
                    while (j >= 0 && lines[j].Trim().Length == 0) j--;
                    if (j < 0 || !lines[j].TrimStart().StartsWith("///", StringComparison.Ordinal))
                        return new(false, $"pas de commentaire /// au-dessus de la ligne {i + 1}");
                }
                if (methods != 4) return new(false, $"{methods} méthodes publiques trouvées (attendu 4)");
                if (!ws.Read("Services/PricingService.cs").Contains("<summary>")) return new(false, "aucun <summary>");

                var diff = ws.DiffFromOriginal();
                if (diff.Removed > 0) return new(false, $"du code a été supprimé ou modifié ({diff.Removed} lignes)");
                var build = ws.BuildAndCompareOutput();
                return new(build.Ok, build.Detail + $", +{diff.Added} −{diff.Removed}");
            }),

        new("extract-method", "Extraire une méthode (refactorisation)",
            "Dans Services/InventoryService.cs, les méthodes Restock et Consume répètent la même validation de la quantité. Extrais-la dans une méthode privée `ValidateQuantity(int quantity)` et appelle-la dans les deux méthodes. Le comportement ne doit pas changer.",
            null,
            (ws, _) =>
            {
                var text = ws.Read("Services/InventoryService.cs");
                if (Regex.Matches(text, @"private\s+(static\s+)?void\s+ValidateQuantity\s*\(\s*int\s+quantity\s*\)").Count != 1) return new(false, "ValidateQuantity absente ou en double");
                if (Regex.Matches(text, @"ValidateQuantity\s*\(").Count < 3) return new(false, "ValidateQuantity n'est pas appelée dans les deux méthodes");
                if (Regex.Matches(text, "La quantité est trop grande").Count != 1) return new(false, "la validation est encore dupliquée");
                var build = ws.BuildAndCompareOutput();
                return new(build.Ok, build.Detail);
            }),

        new("answer-question", "Répondre à une question sans rien modifier",
            "Sans rien modifier, dis-moi dans quel fichier est définie la méthode `Consume` et à quelle ligne. Donne le chemin du fichier et le numéro de ligne dans ton résumé final.",
            null,
            (ws, summary) =>
            {
                var diff = ws.DiffFromOriginal();
                if (diff.Files > 0) return new(false, "des fichiers ont été modifiés");
                var expected = Array.FindIndex(ws.Read("Services/InventoryService.cs").Split('\n'), l => l.Contains("public void Consume(", StringComparison.Ordinal)) + 1;
                if (!summary.Contains("InventoryService", StringComparison.OrdinalIgnoreCase))
                    return new(false, $"le fichier n'est pas cité : « {Truncate(summary, 100)} »");
                var numbers = Regex.Matches(summary, @"\b\d+\b").Select(m => int.Parse(m.Value)).ToList();
                return numbers.Contains(expected)
                    ? new(true, $"InventoryService.cs, ligne {expected}")
                    : new(false, $"ligne attendue {expected}, reçu « {Truncate(summary, 100)} »");
            }),
    };

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
