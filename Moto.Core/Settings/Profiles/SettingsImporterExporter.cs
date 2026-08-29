// Moto.Core/Settings/SettingsImporterExporter.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Settings
{
    /// <summary>
    /// DTO pour l'export/import de paramètres.
    /// Inclut version + checksum pour validation.
    /// </summary>
    public sealed class SettingsExport
    {
        public int Version { get; init; } = 1;
        public DateTime ExportedUtc { get; init; } = DateTime.UtcNow;
        public string ApplicationVersion { get; init; } = "0.4.0";
        public Dictionary<string, object> Settings { get; init; } = new();
        public string Checksum { get; init; } = string.Empty;
    }

    /// <summary>
    /// Service d'export/import des paramètres.
    /// </summary>
    public sealed class SettingsImporterExporter
    {
        private readonly SettingsEngine _settings;
        private readonly ILogger<SettingsImporterExporter> _logger;

        public SettingsImporterExporter(SettingsEngine settings, ILogger<SettingsImporterExporter> logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Exporte tous les paramètres vers un fichier JSON.
        /// </summary>
        public void Export(string filePath, bool includeSensitive = false)
        {
            var allSettings = SettingsCatalog.GetAll();
            var export = new SettingsExport
            {
                Settings = new Dictionary<string, object>()
            };

            foreach (var def in allSettings)
            {
                // Skip les paramètres sensibles (tokens, passwords) sauf si explicitement demandé
                if (!includeSensitive && IsSensitiveKey(def.Key))
                    continue;

                var value = _settings.GetRaw(def.Key);
                if (value != null)
                    export.Settings[def.Key] = value;
            }

            // Calcule le checksum pour validation à l'import
            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
            export = export with { Checksum = ComputeChecksum(json) };

            var finalJson = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, finalJson);

            _logger.LogInformation("[Settings] Exporté : {Path} ({Count} paramètres)", filePath, export.Settings.Count);
        }

        /// <summary>
        /// Importe les paramètres depuis un fichier JSON.
        /// Valide le checksum et le schéma avant application.
        /// </summary>
        public ImportResult Import(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var export = JsonSerializer.Deserialize<SettingsExport>(json);

                if (export == null)
                    return ImportResult.Failure("Fichier invalide : structure incorrecte.");

                // Validation du checksum
                var jsonForChecksum = JsonSerializer.Serialize(
                    export with { Checksum = string.Empty },
                    new JsonSerializerOptions { WriteIndented = true });
                var expectedChecksum = ComputeChecksum(jsonForChecksum);

                if (export.Checksum != expectedChecksum)
                {
                    _logger.LogWarning("[Settings] Checksum invalide lors de l'import.");
                    return ImportResult.Failure("Checksum invalide : le fichier a été modifié ou corrompu.");
                }

                // Validation du schéma (clés connues)
                var knownKeys = new HashSet<string>();
                foreach (var def in SettingsCatalog.GetAll())
                    knownKeys.Add(def.Key);

                var unknownKeys = new List<string>();
                foreach (var key in export.Settings.Keys)
                {
                    if (!knownKeys.Contains(key))
                        unknownKeys.Add(key);
                }

                if (unknownKeys.Count > 0)
                {
                    _logger.LogWarning("[Settings] Clés inconnues ignorées : {Keys}", string.Join(", ", unknownKeys));
                }

                // Application des paramètres
                int applied = 0;
                foreach (var (key, value) in export.Settings)
                {
                    if (knownKeys.Contains(key))
                    {
                        _settings.Set(key, value);
                        applied++;
                    }
                }

                _logger.LogInformation("[Settings] Importé : {Path} ({Applied} paramètres)", filePath, applied);
                return ImportResult.Success(applied, unknownKeys.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Settings] Erreur import : {Path}", filePath);
                return ImportResult.Failure($"Erreur : {ex.Message}");
            }
        }

        private static bool IsSensitiveKey(string key)
            => key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("secret", StringComparison.OrdinalIgnoreCase);

        private static string ComputeChecksum(string json)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
            return Convert.ToBase64String(bytes);
        }
    }

    public sealed record ImportResult(bool Success, int AppliedCount, int SkippedCount, string? ErrorMessage)
    {
        public static ImportResult Success(int applied, int skipped)
            => new(true, applied, skipped, null);

        public static ImportResult Failure(string error)
            => new(false, 0, 0, error);
    }
}
