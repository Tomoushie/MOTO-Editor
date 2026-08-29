using System;
using System.IO;

namespace Moto.Shared;

/// <summary>Mise à jour atomique par swap de dossier + rollback automatique.</summary>
public static class AtomicUpdater
{
    public static void Apply(string installDir, string extractedDir)
    {
        string backup = installDir.TrimEnd(Path.DirectorySeparatorChar) + ".backup";

        // Nettoie un backup résiduel
        if (Directory.Exists(backup)) Directory.Delete(backup, true);

        // 1. Backup de la version actuelle
        Directory.Move(installDir, backup);

        try
        {
            // 2. Swap : extrait → install
            Directory.Move(extractedDir, installDir);
        }
        catch
        {
            // 3. Rollback automatique
            if (Directory.Exists(installDir)) Directory.Delete(installDir, true);
            Directory.Move(backup, installDir);
            throw;
        }

        // 4. Succès → supprime le backup
        try { Directory.Delete(backup, true); } catch { /* non bloquant */ }
    }
}
