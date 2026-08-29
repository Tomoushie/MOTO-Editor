// Moto.Core.Tests/Settings/AiSettingsServiceTests.cs
using System;
using System.Collections.Generic;
using System.IO;
using Moto.Core.Settings;
using Xunit;

namespace Moto.Core.Tests.Settings
{
    public class AiSettingsServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly FakeSettingsStore _store;
        private readonly AiSettingsService _service;

        public AiSettingsServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);

            _store = new FakeSettingsStore();
            _service = new AiSettingsService(_store, _tempDir);
        }

        // ── Tests whitelist ──

        [Fact]
        public void IsAiModifiable_AllowedKey_ReturnsTrue()
        {
            Assert.True(_service.IsAiModifiable("theme"));
            Assert.True(_service.IsAiModifiable("font_size"));
            Assert.True(_service.IsAiModifiable("cortex_mode"));
        }

        [Fact]
        public void IsAiModifiable_ForbiddenKey_ReturnsFalse()
        {
            Assert.False(_service.IsAiModifiable("api.token"));
            Assert.False(_service.IsAiModifiable("license.key"));
            Assert.False(_service.IsAiModifiable("unknown_key"));
        }

        [Fact]
        public void SetSetting_NotInWhitelist_Fails()
        {
            var result = _service.SetSetting("api.token", "secret");

            Assert.False(result.Success);
            Assert.Contains("pas modifiable", result.Message);
        }

        // ── Tests application ──

        [Fact]
        public void SetSetting_AllowedKey_UpdatesStore()
        {
            var result = _service.SetSetting("theme", "dark");

            Assert.True(result.Success);
            Assert.Equal("dark", _store.GetRaw("theme"));
        }

        [Fact]
        public void SetSetting_AllowedKey_CreatesAuditFile()
        {
            _service.SetSetting("minimap", true);

            var auditPath = Path.Combine(_tempDir, ".moto", "ai-settings-audit.json");
            Assert.True(File.Exists(auditPath));
        }

        // ── Tests preview ──

        [Fact]
        public void PrepareSetting_ReturnsOldAndNewValues()
        {
            _store.Set("font_size", 12);

            var preview = _service.PrepareSetting("font_size", 14);

            Assert.True(preview.IsValid);
            Assert.Equal("font_size", preview.Key);
            Assert.Equal(12, preview.OldValue);
            Assert.Equal(14, preview.NewValue);
        }

        [Fact]
        public void PrepareSetting_ForbiddenKey_ReturnsInvalid()
        {
            var preview = _service.PrepareSetting("api.token", "secret");

            Assert.False(preview.IsValid);
            Assert.Contains("pas modifiable", preview.ErrorMessage);
        }

        [Fact]
        public void ApplySetting_AfterPreview_UpdatesStore()
        {
            var preview = _service.PrepareSetting("ai_autosuggest", false);

            var result = _service.ApplySetting(preview);

            Assert.True(result.Success);
            Assert.Equal(false, _store.GetRaw("ai_autosuggest"));
        }

        // ── Tests GetModifiableKeys ──

        [Fact]
        public void GetModifiableKeys_ReturnsNonEmptyList()
        {
            var keys = _service.GetModifiableKeys();

            Assert.NotEmpty(keys);
            Assert.Contains("theme", keys);
            Assert.Contains("font_size", keys);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* Nettoyage best-effort */ }
        }

        private sealed class FakeSettingsStore : AiSettingsService.ISettingsStore
        {
            private readonly Dictionary<string, object> _data = new();

            public object? GetRaw(string key)
                => _data.TryGetValue(key, out var value) ? value : null;

            public void Set(string key, object value)
                => _data[key] = value;
        }
    }
}
