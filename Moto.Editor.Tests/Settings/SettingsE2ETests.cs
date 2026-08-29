// Moto.Core.Tests/Settings/SettingsE2ETests.cs
// Scénarios E2E : migration → rollback → validation.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moto.Core.Settings;
using Xunit;

namespace Moto.Core.Tests.Settings
{
    /// <summary>
    /// Tests de bout en bout : cycle complet migration → validation → rollback → validation.
    /// Chaque test utilise un dossier temporaire isolé.
    /// </summary>
    public class SettingsE2ETests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _settingsPath;
        private readonly SettingsMigrationEngine _migration;
        private readonly SettingsRollbackEngine _rollback;

        public SettingsE2ETests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _settingsPath = Path.Combine(_tempDir, "settings.json");

            _migration = new SettingsMigrationEngine(NullLogger<SettingsMigrationEngine>.Instance);
            _rollback = new SettingsRollbackEngine(NullLogger<SettingsRollbackEngine>.Instance);
        }

        // ── Scénario 1 : cycle complet migration → rollback ──
        [Fact]
        public void E2E_Migrate_Then_Rollback_Restores_Original()
        {
            // GIVEN : un ancien settings.json flat
            var originalContent = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["theme"] = "dark",
                ["editor.font_size"] = 14,
                ["minimap"] = true
            });
            File.WriteAllText(_settingsPath, originalContent);

            // WHEN : migration
            var migrateResult = _migration.MigrateIfNeeded(_settingsPath);

            // THEN : migration réussie
            Assert.True(migrateResult.Success, migrateResult.Message);
            Assert.Equal(3, migrateResult.MigratedKeys);
            Assert.NotNull(migrateResult.BackupPath);
            Assert.True(File.Exists(migrateResult.BackupPath));

            // AND : le fichier est au nouveau format
            var migratedJson = File.ReadAllText(_settingsPath);
            Assert.Contains("\"Version\"", migratedJson);
            Assert.Contains("\"Global\"", migratedJson);

            // WHEN : rollback
            var rollbackResult = _rollback.RollbackToLastBackup(_settingsPath);

            // THEN : rollback réussi
            Assert.True(rollbackResult.Success, rollbackResult.Message);
            Assert.NotNull(rollbackResult.PreRollbackBackup);

            // AND : le contenu est restauré à l'original
            var restoredContent = File.ReadAllText(_settingsPath);
            Assert.Equal(originalContent, restoredContent);
        }

        // ── Scénario 2 : idempotence de la migration ──
        [Fact]
        public void E2E_Migrate_Twice_Is_Idempotent()
        {
            // GIVEN : un ancien settings.json
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(
                new Dictionary<string, object> { ["theme"] = "dark" }));

            // WHEN : première migration
            var first = _migration.MigrateIfNeeded(_settingsPath);
            Assert.True(first.Success);
            Assert.Equal(1, first.MigratedKeys);

            // WHEN : deuxième migration
            var second = _migration.MigrateIfNeeded(_settingsPath);

            // THEN : skip (idempotent)
            Assert.True(second.Success);
            Assert.Equal(0, second.MigratedKeys);
        }

        // ── Scénario 3 : validation du contenu migré ──
        [Fact]
        public void E2E_Migration_Preserves_All_Values()
        {
            // GIVEN : paramètres variés
            var original = new Dictionary<string, object>
            {
                ["theme"] = "dark",
                ["font_size"] = 14,
                ["enable_ai"] = true,
                ["api.endpoint"] = "https://example.com"
            };
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(original));

            // WHEN : migration
            var result = _migration.MigrateIfNeeded(_settingsPath);
            Assert.True(result.Success);

            // THEN : toutes les valeurs sont dans Global
            var migrated = JsonSerializer.Deserialize<MigratedSettings>(File.ReadAllText(_settingsPath));
            Assert.NotNull(migrated);
            Assert.Equal(1, migrated.Version);
            Assert.Equal(original.Count, migrated.Global.Count);

            foreach (var kv in original)
            {
                Assert.True(migrated.Global.ContainsKey(kv.Key), $"Clé manquante : {kv.Key}");
            }
        }

        // ── Scénario 4 : rollback sans backup échoue proprement ──
        [Fact]
        public void E2E_Rollback_Without_Backup_Fails_Gracefully()
        {
            // GIVEN : settings.json sans aucun backup
            File.WriteAllText(_settingsPath, "{}");

            // WHEN : rollback
            var result = _rollback.RollbackToLastBackup(_settingsPath);

            // THEN : échec propre, pas d'exception
            Assert.False(result.Success);
            Assert.Contains("Aucun backup", result.Message);
        }

        // ── Scénario 5 : migration d'un fichier vide ──
        [Fact]
        public void E2E_Migrate_Empty_File_Fails_Gracefully()
        {
            File.WriteAllText(_settingsPath, "{}");
            var result = _migration.MigrateIfNeeded(_settingsPath);
            Assert.False(result.Success);
        }

        // ── Scénario 6 : backup de sécurité avant rollback ──
        [Fact]
        public void E2E_Rollback_Creates_PreRollback_Backup()
        {
            // GIVEN : fichier migré avec backup
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(
                new Dictionary<string, object> { ["a"] = 1 }));
            _migration.MigrateIfNeeded(_settingsPath);

            // WHEN : rollback
            var result = _rollback.RollbackToLastBackup(_settingsPath);

            // THEN : un backup pre-rollback existe
            Assert.True(result.Success);
            Assert.True(File.Exists(result.PreRollbackBackup));
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* nettoyage best-effort */ }
        }
    }
}
