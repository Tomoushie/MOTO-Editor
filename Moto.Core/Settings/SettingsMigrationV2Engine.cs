// Moto.Core/Settings/SettingsMigrationV2Engine.cs
// Migration v2 : workspace overrides, presets dynamiques, paramètres par langage.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Settings
{
    /// <summary>Format v2 avec scopes avancés.</summary>
    public sealed class SettingsV2Format
    {
        public int Version { get; init; } = 2;
        public DateTime MigratedUtc { get; init; } = DateTime.UtcNow;

        /// <summary>Paramètres globaux (défaut).</summary>
        public Dictionary<string, object> Global { get; init; } = new();

        /// <summary>Overrides par workspace (chemin → paramètres).</summary>
        public Dictionary<string, Dictionary<string, object>> WorkspaceOverrides { get; init; } = new();

        /// <summary>Presets dynamiques (nom → paramètres).</summary>
        public Dictionary<string, Dictionary<string, object>> Presets { get; init; } = new();

        /// <summary>Paramètres par langage (extension → paramètres).</summary>
        public Dictionary<string, Dictionary<string, object>> LanguageOverrides { get; init; } = new();

        /// <summary>Profil actif.</summary>
        public string ActiveProfile { get; set; } = "default";
    }

    /// <summary>
    /// Moteur de migration v1 → v2.
    /// Ajoute : workspace overrides, presets dynamiques, paramètres par langage.
    /// </summary>
    public sealed class SettingsMigrationV2Engine
    {
        private readonly ILogger<SettingsMigrationV2Engine> _logger;

        public SettingsMigrationV2Engine(ILogger<SettingsMigrationV2Engine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public MigrationResult MigrateV1ToV2(string settingsPath)
        {
            if (!File.Exists(settingsPath))
                return MigrationResult.Ok(0, null);

            try
            {
                var json = File.ReadAllText(settingsPath);

                // Détection : déjà en v2 ?
                if (json.Contains("\"Version\": 2") || json.Contains("\"version\": 2"))
                {
                    _logger.LogInformation("[Migration v2] Déjà en v2, skip.");
                    return MigrationResult.Ok(0, null);
                }

                // Backup
                var backupPath = $"{settingsPath}.{DateTime.UtcNow:yyyyMMdd-HHmmss}.v2.bak";
                File.Copy(settingsPath, backupPath, overwrite: false);

                // Parser le format v1
                var v1 = JsonSerializer.Deserialize<MigratedSettings>(json);
                if (v1 == null)
                    return MigrationResult.Fail("Format v1 invalide.");

                // Convertir en v2
                var v2 = new SettingsV2Format
                {
                    Global = v1.Global,
                    WorkspaceOverrides = new Dictionary<string, Dictionary<string, object>>(),
                    Presets = BuildDefaultPresets(),
                    LanguageOverrides = BuildDefaultLanguageOverrides()
                };

                // Écrire le format v2
                var newJson = JsonSerializer.Serialize(v2, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, newJson);

                _logger.LogInformation("[Migration v2] Migré {Count} clés vers v2.", v1.Global.Count);
                return MigrationResult.Ok(v1.Global.Count, backupPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Migration v2] Échec.");
                return MigrationResult.Fail($"Erreur : {ex.Message}");
            }
        }

        private static Dictionary<string, Dictionary<string, object>> BuildDefaultPresets()
        {
            return new Dictionary<string, Dictionary<string, object>>
            {
                ["beginner"] = new()
                {
                    ["cortex_mode"] = "Beginner",
                    ["ai_autosuggest"] = true,
                    ["ai_confidence_threshold"] = 0.9
                },
                ["expert"] = new()
                {
                    ["cortex_mode"] = "Expert",
                    ["ai_autosuggest"] = true,
                    ["ai_confidence_threshold"] = 0.5
                },
                ["turbo"] = new()
                {
                    ["cortex_mode"] = "Turbo",
                    ["performance_mode"] = "Ultra"
                }
            };
        }

        private static Dictionary<string, Dictionary<string, object>> BuildDefaultLanguageOverrides()
        {
            return new Dictionary<string, Dictionary<string, object>>
            {
                [".cs"] = new() { ["editor.tab_size"] = 4, ["editor.word_wrap"] = false },
                [".py"] = new() { ["editor.tab_size"] = 4, ["editor.word_wrap"] = true },
                [".js"] = new() { ["editor.tab_size"] = 2, ["editor.word_wrap"] = false },
                [".md"] = new() { ["editor.word_wrap"] = true }
            };
        }
    }
}
