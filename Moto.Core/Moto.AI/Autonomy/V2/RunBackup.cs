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

    /// <summary>Remet chaque fichier dans son état d'avant le run. Retourne le nombre de fichiers restaurés ou supprimés.</summary>
    public int Restore()
    {
        var count = 0;
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
            }
            catch (IOException) { /* fichier verrouillé : on continue avec les suivants */ }
            catch (UnauthorizedAccessException) { }
        }
        return count;
    }
}
