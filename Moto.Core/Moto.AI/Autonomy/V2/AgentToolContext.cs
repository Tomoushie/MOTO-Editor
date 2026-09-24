// Moto.Core/Moto.AI/Autonomy/V2/AgentToolContext.cs
// ★ AJOUT (24/09, agent v2) : ce que les outils partagent pendant UN run — la racine du projet
// (confinement des chemins), la sauvegarde des originaux, l'exécution de commandes.
using Moto.Editor.Services; // TerminalService (namespace historique)

namespace Moto.Core.AI.Autonomy.V2;

public sealed class ToolPathException : Exception
{
    public ToolPathException(string message) : base(message) { }
}

public sealed class AgentToolContext
{
    private static readonly string[] ProtectedFolders = { ".git" };

    /// <summary>Exécute une commande dans un dossier (injecté pour les tests ; par défaut TerminalService).</summary>
    public Func<string, string, CancellationToken, Task<TerminalCommandResult>> RunCommand { get; }

    public string Root { get; }
    public string AgentId { get; }
    public RunBackup Backup { get; }

    public AgentToolContext(
        string workspaceRoot,
        string agentId,
        RunBackup backup,
        Func<string, string, CancellationToken, Task<TerminalCommandResult>>? runCommand = null)
    {
        Root = Path.GetFullPath(AgentPathResolver.EffectiveRoot(workspaceRoot));
        AgentId = agentId;
        Backup = backup;
        RunCommand = runCommand ?? ((command, dir, ct) => new TerminalService().ExecuteAsync(command, dir, ct));
    }

    /// <summary>Chemin relatif au projet pour l'affichage (séparateur « / »).</summary>
    public string Display(string fullPath) => Path.GetRelativePath(Root, fullPath).Replace('\\', '/');

    /// <summary>
    /// Résout un chemin donné par le modèle et REFUSE tout ce qui sort du dossier du projet
    /// (ou touche à .git). Tolère les habitudes des petits modèles : « / » initial, « ./ », « \ ».
    /// </summary>
    public string Resolve(string? path, bool allowRoot = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (allowRoot) return Root;
            throw new ToolPathException("Le paramètre « path » est obligatoire.");
        }

        path = path.Trim().Trim('"', '\'', '`');

        string candidate;
        if (StartsWithSlashOnly(path))
            // « /Dossier/Fichier.cs » : le modèle veut dire « à partir du projet », pas la racine du disque.
            candidate = Path.GetFullPath(Path.Combine(Root, path.TrimStart('/', '\\')));
        else if (Path.IsPathRooted(path))
            candidate = Path.GetFullPath(path);
        else
            candidate = Path.GetFullPath(Path.Combine(Root, path));

        if (!AgentPathResolver.IsWithinRoot(Root, candidate))
            throw new ToolPathException($"« {path} » sort du dossier du projet : donne un chemin relatif au projet (par exemple Dossier/Fichier.cs).");

        var relative = Path.GetRelativePath(Root, candidate);
        foreach (var part in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            if (Array.Exists(ProtectedFolders, f => string.Equals(f, part, StringComparison.OrdinalIgnoreCase)))
                throw new ToolPathException($"Le dossier « {part} » est protégé : l'agent n'y touche pas.");

        return candidate;
    }

    private static bool StartsWithSlashOnly(string path)
        => (path.StartsWith('/') || path.StartsWith('\\')) && !path.StartsWith("//") && !path.StartsWith("\\\\");
}
