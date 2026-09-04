// Moto.Core/AI/Autonomy/AgentAuditLog.cs
// ★ AJOUT (03/09, jalon 2 — "agents autonomes en tâche de fond"). Seul manque
// réel laissé volontairement par le jalon 1 : rien ne survivait à un
// redémarrage (ObservableCollection Steps/Runs en mémoire seulement, pas de
// trace des confirmations une fois le chat fait défiler). Journal NDJSON
// (une ligne JSON par événement), écrit directement par BackgroundAgentLoop —
// pas par un outil, pour qu'un futur outil bogué ne puisse jamais l'omettre.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Moto.Core.AI.Autonomy
{
    public sealed class AgentAuditLog
    {
        private readonly string _filePath;
        private readonly object _gate = new();

        /// <summary>★ AJOUT (jalon 3) : dossier de base, exposé pour que le panneau
        /// "Agents en cours" (Moto.Editor, pas ce projet) puisse l'ouvrir dans
        /// l'explorateur sans dupliquer ce chemin ni deviner le nom de fichier
        /// haché d'un workspace précis.</summary>
        public static string BaseFolder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MotoEditor", "AgentAudit");

        public AgentAuditLog(string workspaceRoot)
        {
            Directory.CreateDirectory(BaseFolder);
            _filePath = Path.Combine(BaseFolder, $"{SanitizeForFileName(workspaceRoot)}.ndjson");
        }

        public string FilePath => _filePath;

        /// <summary>Ajoute une ligne, immédiatement écrite sur le disque (pas de
        /// tampon en mémoire) — une action mutante qui plante juste après doit
        /// quand même laisser une trace de ce qui a été proposé/décidé.</summary>
        public void Append(object entry)
        {
            try
            {
                var json = JsonSerializer.Serialize(entry);
                lock (_gate)
                {
                    File.AppendAllText(_filePath, json + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Le journal ne doit jamais faire planter un agent — une écriture
                // ratée (disque plein, permissions…) reste silencieuse ici.
            }
        }

        private static string SanitizeForFileName(string? workspaceRoot)
        {
            var folderName = string.IsNullOrWhiteSpace(workspaceRoot)
                ? "default"
                : Path.GetFileName(workspaceRoot!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(folderName)) folderName = "default";

            // Un simple nom de dossier ne suffit pas à éviter les collisions
            // (deux projets différents peuvent s'appeler pareil sur deux
            // disques) — complété par un hash court du chemin complet.
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(workspaceRoot ?? string.Empty)))[..8];
            return $"{folderName}-{hash}";
        }
    }
}
