// Moto.Core/Moto.AI/Autonomy/V2/RunBackup.cs
// ★ AJOUT (24/09, agent v2) : sauvegarde de l'ORIGINAL de chaque fichier avant que l'agent v2 y touche,
// pour pouvoir tout défaire d'un coup (« Annuler les modifications de cette exécution ») même hors Git.
// La v1 écrasait le fichier sans rien garder : une écriture acceptée par erreur était perdue.
namespace Moto.Core.AI.Autonomy.V2;

public sealed class RunBackup
{
    private readonly string _root;
    private readonly Dictionary<string, string?> _saved = new(StringComparer.OrdinalIgnoreCase);

    public static string BaseFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MotoEditor", "AgentBackups");

    public string Folder { get; }

    public RunBackup(string workspaceRoot, string runId, string? baseFolder = null)
    {
        _root = Path.GetFullPath(workspaceRoot);
        Folder = Path.Combine(baseFolder ?? BaseFolder, runId);
    }

    /// <summary>
    /// Supprime les sauvegardes des runs plus vieux que <paramref name="maxAge"/> (14 jours par défaut) : sans ça, le dossier
    /// grossirait à chaque exécution de l'agent. Ne lève jamais d'exception. Retourne le nombre de dossiers supprimés.
    /// </summary>
    public static int Prune(string? baseFolder = null, TimeSpan? maxAge = null)
    {
        var limit = DateTime.UtcNow - (maxAge ?? TimeSpan.FromDays(14));
        var removed = 0;
        try
        {
            var folder = baseFolder ?? BaseFolder;
            if (!Directory.Exists(folder)) return 0;

            foreach (var dir in Directory.EnumerateDirectories(folder))
            {
                try
                {
                    if (Directory.GetLastWriteTimeUtc(dir) >= limit) continue;
                    Directory.Delete(dir, recursive: true);
                    removed++;
                }
                catch (IOException) { /* dossier ouvert ailleurs : sera repris au prochain passage */ }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return removed;
    }

    /// <summary>Fichiers touchés pendant le run (chemins complets).</summary>
    public IReadOnlyCollection<string> TouchedFiles => _saved.Keys;

    /// <summary>À appeler AVANT chaque écriture. Idempotent : seul l'état d'avant le PREMIER changement est gardé.</summary>
    public void Save(string fullPath)
    {
        fullPath = Path.GetFullPath(fullPath);
        if (_saved.ContainsKey(fullPath)) return;

        if (!File.Exists(fullPath))
        {
            _saved[fullPath] = null; // n'existait pas : l'annulation le supprimera
            return;
        }

        var relative = Path.GetRelativePath(_root, fullPath);
        var target = Path.Combine(Folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(fullPath, target, overwrite: true);
        _saved[fullPath] = target;
    }

    // ── État de fin de run : pour ne jamais annuler AU-DESSUS du travail que l'utilisateur a fait depuis ──

    private const string Unreadable = "?";
    private readonly Dictionary<string, string?> _sealed = new(StringComparer.OrdinalIgnoreCase);
    private bool _isSealed;

    /// <summary>
    /// À appeler UNE fois à la fin du run : mémorise l'état de chaque fichier touché (empreinte SHA-256, ou « absent »).
    /// Permet à <see cref="ChangedSinceSeal"/> de dire quels fichiers l'utilisateur a modifiés depuis — l'annulation les écraserait.
    /// </summary>
    public void Seal()
    {
        _sealed.Clear();
        foreach (var path in _saved.Keys) _sealed[path] = StateOf(path);
        _isSealed = true;
    }

    /// <summary>Fichiers touchés dont l'état a changé depuis <see cref="Seal"/> (modifiés, supprimés ou recréés à la main). Chemins complets.</summary>
    public IReadOnlyList<string> ChangedSinceSeal()
    {
        if (!_isSealed) return Array.Empty<string>();
        return _sealed
            .Where(kv =>
            {
                var now = StateOf(kv.Key);
                return now != Unreadable && kv.Value != Unreadable && !string.Equals(now, kv.Value, StringComparison.Ordinal);
            })
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>Chemin relatif au projet (séparateur « / »), pour l'affichage.</summary>
    public string RelativePath(string fullPath) => Path.GetRelativePath(_root, fullPath).Replace('\\', '/');

    // Ne lève jamais : Seal() est appelé en fin de run, y compris depuis un « catch » — une exception ici ferait perdre le résultat du run.
    // Un fichier illisible (verrouillé…) reçoit l'état « ? » : il est traité comme inchangé plutôt que d'alarmer à tort.
    private static string? StateOf(string path)
    {
        try { return File.Exists(path) ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))) : null; }
        catch (Exception) { return Unreadable; }
    }

    /// <summary>Remet chaque fichier dans son état d'avant le run. Retourne le nombre de fichiers restaurés ou supprimés.</summary>
    public int Restore() => Restore(out _);

    /// <summary>
    /// Comme <see cref="Restore()"/>, mais dit aussi quels fichiers n'ont pas pu être remis en place (verrouillés par un autre
    /// programme…) — chemins complets. Refaire l'appel est sans risque : restaurer deux fois donne le même résultat.
    /// </summary>
    public int Restore(out IReadOnlyList<string> failed)
    {
        var count = 0;
        var notRestored = new List<string>();
        foreach (var (path, backup) in _saved)
        {
            try
            {
                if (backup is null)
                {
                    if (File.Exists(path)) { File.Delete(path); count++; }
                }
                else if (File.Exists(backup))
                {
                    File.Copy(backup, path, overwrite: true);
                    count++;
                }
                else
                {
                    notRestored.Add(path); // la copie d'origine a disparu (dossier purgé…) : on ne peut rien remettre
                }
            }
            catch (IOException) { notRestored.Add(path); }   // fichier verrouillé : on continue avec les suivants
            catch (UnauthorizedAccessException) { notRestored.Add(path); }
        }
        failed = notRestored;
        return count;
    }
}
