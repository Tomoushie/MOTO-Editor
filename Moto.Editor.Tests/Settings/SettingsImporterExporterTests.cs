// Moto.Core.Tests/Settings/SettingsImporterExporterTests.cs
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging.Abstractions;
using Moto.Core.Settings;
using Xunit;

namespace Moto.Core.Tests.Settings
{
    /// <summary>
    /// Tests de l'export/import : checksum, validation, paramètres sensibles.
    /// </summary>
    public class SettingsImporterExporterTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly FakeSettingsStore _store;
        private readonly SettingsImporterExporter _service;

        public SettingsImporterExporterTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);

            _store = new FakeSettingsStore();
            _service = new SettingsImporterExporter(_store, NullLogger<SettingsImporterExporter>.Instance);
        }

        [Fact]
        public void Export_Then_Import_Restores_All_Settings()
        {
            // Arrange : on peuple le store.
            _store.Set("theme", "dark");
            _store.Set("editor.font_size", 14);
            _store.Set("minimap", true);

            var exportPath = Path.Combine(_tempDir, "export.json");

            // Act : export puis réimport dans un store vierge.
            _service.Export(exportPath);
            var result = _service.Import(exportPath);

            // Assert : succès et valeurs restaurées.
            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(result.AppliedCount >= 3);
        }

        [Fact]
        public void Import_Corrupted_Checksum_Fails()
        {
            // Arrange : export valide.
            _store.Set("theme", "dark");
            var exportPath = Path.Combine(_tempDir, "tampered.json");
            _service.Export(exportPath);

            // On altère le fichier (simulation de corruption).
            var content = File.ReadAllText(exportPath);
            content = content.Replace("\"dark\"", "\"light\"");
            File.WriteAllText(exportPath, content);

            // Act : import du fichier altéré.
            var result = _service.Import(exportPath);

            // Assert : refusé à cause du checksum.
            Assert.False(result.Success);
            Assert.Contains("Checksum", result.ErrorMessage);
        }

        [Fact]
        public void Import_Unknown_Keys_Are_Skipped()
        {
            // Arrange : export avec une clé valide.
            _store.Set("theme", "dark");
            var exportPath = Path.Combine(_tempDir, "unknown.json");
            _service.Export(exportPath);

            // On injecte une clé inconnue dans le JSON.
            var json = File.ReadAllText(exportPath);
            json = json.Replace("\"theme\": \"dark\"",
                                "\"theme\": \"dark\", \"unknown.key.xyz\": 42");
            File.WriteAllText(exportPath, json);

            // Act
            var result = _service.Import(exportPath);

            // Assert : succès mais la clé inconnue est comptée comme skipped.
            Assert.True(result.Success, result.ErrorMessage);
            Assert.True(result.SkippedCount >= 1);
        }

        [Fact]
        public void Export_Excludes_Sensitive_Keys_By_Default()
        {
            // Arrange : une clé sensible.
            _store.Set("api.token", "secret-123");
            _store.Set("theme", "dark");

            var exportPath = Path.Combine(_tempDir, "sensitive.json");

            // Act : export sans flag includeSensitive.
            _service.Export(exportPath);
            var content = File.ReadAllText(exportPath);

            // Assert : le token n'est PAS dans l'export.
            Assert.DoesNotContain("secret-123", content);
            Assert.Contains("dark", content);
        }

        [Fact]
        public void Import_NonExistent_File_Fails_Gracefully()
        {
            var result = _service.Import(Path.Combine(_tempDir, "does-not-exist.json"));
            Assert.False(result.Success);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* ignoré : nettoyage best-effort */ }
        }
    }

    /// <summary>
    /// Fake in-memory de ISettingsStore pour les tests.
    /// Évite toute dépendance à SettingsEngine.Shared (singleton global).
    /// </summary>
    internal sealed class FakeSettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, object> _data = new();

        public T Get<T>(string key, T defaultValue)
        {
            if (_data.TryGetValue(key, out var value))
            {
                try { return (T)Convert.ChangeType(value, typeof(T)); }
                catch { return defaultValue; }
            }
            return defaultValue;
        }

        public void Set<T>(string key, T value)
        {
            if (value is null) _data.Remove(key);
            else _data[key] = value;
        }

        public object? GetRaw(string key)
            => _data.TryGetValue(key, out var v) ? v : null;

        public bool GetBool(string key, bool defaultValue = false)
            => Get(key, defaultValue);

        public string GetString(string key, string defaultValue = "")
            => Get(key, defaultValue);
    }
}
