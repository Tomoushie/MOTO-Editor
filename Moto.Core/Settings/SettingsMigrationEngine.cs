// Moto.Core/Settings/SettingsMigrationEngine.cs — Remplacer la détection
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Settings
{
    /// <summary>Résultat d'une opération de migration de settings.json.</summary>
    public sealed class MigrationResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public int MigratedKeys { get; init; }
        public string? BackupPath { get; init; }

        public static MigrationResult Ok(int migratedKeys, string? backupPath)
            => new()
            {
                Success = true,
                Message = migratedKeys > 0
                    ? $"Migré {migratedKeys} clé(s)."
                    : "Aucune migration nécessaire.",
                MigratedKeys = migratedKeys,
                BackupPath = backupPath
            };

        public static MigrationResult Fail(string message)
            => new() { Success = false, Message = message };
    }

    /// <summary>Format v1 des settings (scopes Global/Project).</summary>
    public sealed class MigratedSettings
    {
        public int Version { get; init; } = 1;
        public DateTime MigratedUtc { get; init; } = DateTime.UtcNow;
        public Dictionary<string, object> Global { get; init; } = new();
        public Dictionary<string, Dictionary<string, object>> Project { get; init; } = new();
    }

    /// <summary>Moteur de migration de l'ancien format flat vers le format v1 (scopes).</summary>
    public sealed class SettingsMigrationEngine
    {
        private readonly ILogger<SettingsMigrationEngine> _logger;

        public SettingsMigrationEngine(ILogger<SettingsMigrationEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public MigrationResult MigrateIfNeeded(string settingsPath)
        {
            if (!File.Exists(settingsPath))
            {
                _logger.LogInformation("[Migration] Aucun settings.json trouvé, rien à migrer.");
                return MigrationResult.Ok(0, null);
            }

            try
            {
                var json = File.ReadAllText(settingsPath);

                // ── DÉTECTION FINE : Parse le JSON et vérifie Version == 1 ──
                try
                {
                    var parsed = JsonSerializer.Deserialize<JsonElement>(json);

                    if (parsed.ValueKind == JsonValueKind.Object &&
                        parsed.TryGetProperty("Version", out var versionProp) &&
                        versionProp.ValueKind == JsonValueKind.Number &&
                        versionProp.GetInt32() >= 1)
                    {
                        _logger.LogInformation("[Migration] Format déjà migré (Version={Version}), skip.", versionProp.GetInt32());
                        return MigrationResult.Ok(0, null);
                    }

                    // Vérifie aussi en minuscule (camelCase)
                    if (parsed.ValueKind == JsonValueKind.Object &&
                        parsed.TryGetProperty("version", out var versionPropLower) &&
                        versionPropLower.ValueKind == JsonValueKind.Number &&
                        versionPropLower.GetInt32() >= 1)
                    {
                        _logger.LogInformation("[Migration] Format déjà migré (version={Version}), skip.", versionPropLower.GetInt32());
                        return MigrationResult.Ok(0, null);
                    }
                }
                catch (JsonException)
                {
                    // JSON invalide : on continue avec la migration
                    _logger.LogWarning("[Migration] JSON invalide, tentative de migration.");
                }

                // Backup avant toute modification
                var backupPath = CreateBackup(settingsPath);

                // Parse de l'ancien format (flat key-value)
                var oldSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (oldSettings == null || oldSettings.Count == 0)
                {
                    _logger.LogWarning("[Migration] Fichier vide ou invalide.");
                    return MigrationResult.Fail("Fichier source vide ou invalide.");
                }

                // Conversion vers le nouveau format avec scopes
                var migrated = new MigratedSettings
                {
                    Version = 1,
                    MigratedUtc = DateTime.UtcNow,
                    Global = oldSettings,
                    Project = new Dictionary<string, Dictionary<string, object>>()
                };

                // Écriture du nouveau format
                var newJson = JsonSerializer.Serialize(migrated, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(settingsPath, newJson);

                _logger.LogInformation(
                    "[Migration] Migré {Count} clés. Backup : {Backup}",
                    oldSettings.Count, backupPath);

                return MigrationResult.Ok(oldSettings.Count, backupPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Migration] Échec de la migration.");
                return MigrationResult.Fail($"Erreur : {ex.Message}");
            }
        }

        private static string CreateBackup(string settingsPath)
        {
            var backupPath = $"{settingsPath}.{DateTime.UtcNow:yyyyMMdd-HHmmss}.bak";
            File.Copy(settingsPath, backupPath, overwrite: false);
            return backupPath;
        }
    }
}
