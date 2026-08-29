// Moto.Core/Settings/SettingsRollbackEngine.cs
using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Settings
{
    public sealed class RollbackResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? RestoredFrom { get; init; }
        public string? PreRollbackBackup { get; init; }

        public static RollbackResult Ok(string restoredFrom, string preBackup)
            => new()
            {
                Success = true,
                Message = $"Restauré depuis {Path.GetFileName(restoredFrom)}",
                RestoredFrom = restoredFrom,
                PreRollbackBackup = preBackup
            };

        public static RollbackResult Fail(string message)
            => new() { Success = false, Message = message };
    }

    public sealed class SettingsRollbackEngine
    {
        private readonly ILogger<SettingsRollbackEngine> _logger;

        public SettingsRollbackEngine(ILogger<SettingsRollbackEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public RollbackResult RollbackToLastBackup(string settingsPath)
        {
            if (!File.Exists(settingsPath))
                return RollbackResult.Fail("settings.json introuvable.");

            try
            {
                var dir = Path.GetDirectoryName(settingsPath);
                if (string.IsNullOrWhiteSpace(dir))
                    return RollbackResult.Fail("Chemin invalide.");

                var fileName = Path.GetFileName(settingsPath);

                var backups = Directory
                    .GetFiles(dir, $"{fileName}.*.bak")
                    .Where(f => !f.EndsWith(".pre-rollback.bak", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .ToArray();

                if (backups.Length == 0)
                {
                    _logger.LogWarning("[Rollback] Aucun backup trouvé.");
                    return RollbackResult.Fail("Aucun backup trouvé.");
                }

                var lastBackup = backups[0];

                var preBackup = $"{settingsPath}.{DateTime.UtcNow:yyyyMMdd-HHmmss}.pre-rollback.bak";
                File.Copy(settingsPath, preBackup, overwrite: true);

                File.Copy(lastBackup, settingsPath, overwrite: true);

                _logger.LogInformation("[Rollback] Restauré depuis {Backup}", lastBackup);
                return RollbackResult.Ok(lastBackup, preBackup);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Rollback] Échec.");
                return RollbackResult.Fail($"Erreur : {ex.Message}");
            }
        }

        public string[] ListBackups(string settingsPath)
        {
            if (!File.Exists(settingsPath))
                return Array.Empty<string>();

            var dir = Path.GetDirectoryName(settingsPath);
            if (string.IsNullOrWhiteSpace(dir))
                return Array.Empty<string>();

            var fileName = Path.GetFileName(settingsPath);

            return Directory
                .GetFiles(dir, $"{fileName}.*.bak")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();
        }
    }
}
