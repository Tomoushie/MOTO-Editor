// Moto.Core/Moto.AI/Autonomy/V2/LineDiff.cs
// ★ AJOUT (24/09, agent v2) : différence ligne à ligne (format « unifié », comme git diff).
// Sert à MONTRER à l'humain ce qu'une modification va changer AVANT qu'il l'autorise —
// l'ancienne confirmation affichait le fichier entier proposé, sans dire ce qui change
// (et tronqué à 2000 caractères : impossible de vérifier un fichier plus long).
using System.Text;

namespace Moto.Core.AI.Autonomy.V2;

public sealed record DiffResult(string Unified, int Added, int Removed)
{
    public bool HasChanges => Added > 0 || Removed > 0;

    /// <summary>Compteur court, façon « +34 −2 ».</summary>
    public string Summary => $"+{Added} −{Removed}";
}

public static class LineDiff
{
    // Au-delà, on ne cherche plus la plus longue sous-suite commune (mémoire) : la zone qui diffère
    // est présentée comme entièrement remplacée — toujours exact, seulement moins fin.
    private const int MaxCells = 4_000_000;

    public static string[] SplitLines(string text)
    {
        if (text.Length == 0) return Array.Empty<string>();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        return lines[^1].Length == 0 ? lines[..^1] : lines;
    }

    public static DiffResult Compute(string oldText, string newText, int context = 3)
    {
        var a = SplitLines(oldText);
        var b = SplitLines(newText);

        var start = 0;
        while (start < a.Length && start < b.Length && a[start] == b[start]) start++;
        var endA = a.Length;
        var endB = b.Length;
        while (endA > start && endB > start && a[endA - 1] == b[endB - 1]) { endA--; endB--; }

        var ops = new List<(char Op, string Line)>(a.Length + b.Length);
        for (var i = 0; i < start; i++) ops.Add((' ', a[i]));
        AppendMiddle(ops, a, start, endA, b, start, endB);
        for (var i = endA; i < a.Length; i++) ops.Add((' ', a[i]));

        var added = 0;
        var removed = 0;
        foreach (var (op, _) in ops)
        {
            if (op == '+') added++;
            else if (op == '-') removed++;
        }
        if (added + removed == 0) return new DiffResult(string.Empty, 0, 0);

        // Numéro de ligne (base 1) de chaque opération dans l'ancien et dans le nouveau texte.
        var oldNo = new int[ops.Count];
        var newNo = new int[ops.Count];
        int ol = 1, nl = 1;
        for (var i = 0; i < ops.Count; i++)
        {
            oldNo[i] = ol;
            newNo[i] = nl;
            if (ops[i].Op != '+') ol++;
            if (ops[i].Op != '-') nl++;
        }

        var sb = new StringBuilder();
        var idx = 0;
        while (idx < ops.Count)
        {
            while (idx < ops.Count && ops[idx].Op == ' ') idx++;
            if (idx >= ops.Count) break;

            var hunkStart = Math.Max(0, idx - context);
            var lastChange = idx;
            var j = idx;
            while (j < ops.Count)
            {
                if (ops[j].Op != ' ') { lastChange = j; j++; continue; }
                var k = j;
                while (k < ops.Count && ops[k].Op == ' ') k++;
                // Deux modifications proches partagent le même bloc.
                if (k < ops.Count && k - j <= 2 * context) { j = k; continue; }
                break;
            }
            var hunkEnd = Math.Min(ops.Count - 1, lastChange + context);

            int oldCount = 0, newCount = 0;
            for (var i = hunkStart; i <= hunkEnd; i++)
            {
                if (ops[i].Op != '+') oldCount++;
                if (ops[i].Op != '-') newCount++;
            }
            sb.Append("@@ -").Append(oldNo[hunkStart]).Append(',').Append(oldCount)
              .Append(" +").Append(newNo[hunkStart]).Append(',').Append(newCount).Append(" @@\n");
            for (var i = hunkStart; i <= hunkEnd; i++)
                sb.Append(ops[i].Op).Append(ops[i].Line).Append('\n');

            idx = hunkEnd + 1;
        }

        return new DiffResult(sb.ToString(), added, removed);
    }

    private static void AppendMiddle(List<(char Op, string Line)> ops, string[] a, int a0, int a1, string[] b, int b0, int b1)
    {
        var n = a1 - a0;
        var m = b1 - b0;

        if (n == 0) { for (var i = b0; i < b1; i++) ops.Add(('+', b[i])); return; }
        if (m == 0) { for (var i = a0; i < a1; i++) ops.Add(('-', a[i])); return; }

        if ((long)n * m > MaxCells)
        {
            for (var i = a0; i < a1; i++) ops.Add(('-', a[i]));
            for (var i = b0; i < b1; i++) ops.Add(('+', b[i]));
            return;
        }

        // dp[i,j] = longueur de la plus longue sous-suite commune de a[i..] et b[j..].
        var w = m + 1;
        var dp = new int[(n + 1) * w];
        for (var i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                dp[i * w + j] = a[a0 + i] == b[b0 + j]
                    ? dp[(i + 1) * w + j + 1] + 1
                    : Math.Max(dp[(i + 1) * w + j], dp[i * w + j + 1]);

        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[a0 + x] == b[b0 + y]) { ops.Add((' ', a[a0 + x])); x++; y++; }
            else if (dp[(x + 1) * w + y] >= dp[x * w + y + 1]) { ops.Add(('-', a[a0 + x])); x++; }
            else { ops.Add(('+', b[b0 + y])); y++; }
        }
        while (x < n) { ops.Add(('-', a[a0 + x])); x++; }
        while (y < m) { ops.Add(('+', b[b0 + y])); y++; }
    }
}
