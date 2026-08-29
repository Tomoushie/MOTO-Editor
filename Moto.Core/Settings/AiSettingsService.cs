// Moto.Core/Settings/AiSettingsService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Moto.Core.Settings
{
    public interface ISettingsStore
    {
        object? GetRaw(string key);
        void Set(string key, object value);
    }

    public sealed class AiSettingsResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;

        public static AiSettingsResult Ok(string message)
            => new() { Success = true, Message = message };

        public static AiSettingsResult Fail(string message)
            => new() { Success = false, Message = message };
    }

    public sealed class AiSettingsChangePreview
    {
        public bool IsValid { get; init; }
        public string Key { get; init; } = string.Empty;
        public object? OldValue { get; init; }
        public object? NewValue { get; init; }
        public string? ErrorMessage { get; init; }

        public static AiSettingsChangePreview Valid(string key, object? oldValue, object newValue)
            => new() { IsValid = true, Key = key, OldValue = oldValue, NewValue = newValue };

        public static AiSettingsChangePreview Invalid(string message)
            => new() { IsValid = false, ErrorMessage = message };
    }

    /// <summary>
    /// Permet à MOTO AI de modifier des paramètres avec whitelist, audit et preview.
    /// </summary>
    public sealed class AiSettingsService
    {
        private readonly ISettingsStore _settings;
        private readonly string _auditPath;

        private static readonly HashSet<string> AiModifiableKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "theme",
            "font_size",
            "font_family",
            "minimap",
            "editor.tab_size",
            "editor.word_wrap",
            "editor.line_numbers",
            "editor.auto_save",
            "cortex_mode",
            "ai_autosuggest",
            "ai_confidence_threshold",
            "ai_proactive_actions",
            "performance_mode",
            "ui.show_terminal",
            "ui.show_explorer",
            "ui.show_statusbar"
        };

        public AiSettingsService(SettingsEngine settings, string workspaceRoot)
            : this(new SettingsEngineAdapter(settings), workspaceRoot)
        {
        }

        public AiSettingsService(ISettingsStore settings, string workspaceRoot)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            var motoDir = Path.Combine(workspaceRoot, ".moto");
            Directory.CreateDirectory(motoDir);
            _auditPath = Path.Combine(motoDir, "ai-settings-audit.json");
        }

        public bool IsAiModifiable(string key)
            => !string.IsNullOrWhiteSpace(key) && AiModifiableKeys.Contains(key);

        public IReadOnlyList<string> GetModifiableKeys()
            => AiModifiableKeys.ToList();

        /// <summary>
        /// Applique directement, pour compatibilité avec les anciens appels.
        /// Pour une confirmation UI, utiliser PrepareSetting + ApplySetting.
        /// </summary>
        public AiSettingsResult SetSetting(string key, object value)
        {
            var preview = PrepareSetting(key, value);
            if (!preview.IsValid)
                return AiSettingsResult.Fail(preview.ErrorMessage ?? "Modification refusée.");

            return ApplySetting(preview);
        }

        public AiSettingsChangePreview PrepareSetting(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return AiSettingsChangePreview.Invalid("Clé manquante.");

            if (!IsAiModifiable(key))
                return AiSettingsChangePreview.Invalid($"🔒 La clé '{key}' n'est pas modifiable par l'IA.");

            var oldValue = _settings.GetRaw(key);
            return AiSettingsChangePreview.Valid(key, oldValue, value);
        }

        public AiSettingsResult ApplySetting(AiSettingsChangePreview preview)
        {
            if (preview is null)
                return AiSettingsResult.Fail("Preview invalide.");

            if (!preview.IsValid)
                return AiSettingsResult.Fail(preview.ErrorMessage ?? "Modification refusée.");

            try
            {
                _settings.Set(preview.Key, preview.NewValue!);
                AppendAudit(preview.Key, preview.OldValue, preview.NewValue);

                return AiSettingsResult.Ok(
                    $"✅ '{preview.Key}' modifié : {preview.OldValue?.ToString() ?? "(null)"} → {preview.NewValue?.ToString() ?? "(null)"}");
            }
            catch (Exception ex)
            {
                return AiSettingsResult.Fail($"Erreur : {ex.Message}");
            }
        }

        private void AppendAudit(string key, object? oldValue, object? newValue)
        {
            try
            {
                List<AuditEntry> entries;

                if (File.Exists(_auditPath))
                {
                    var json = File.ReadAllText(_auditPath);
                    entries = JsonSerializer.Deserialize<List<AuditEntry>>(json) ?? new List<AuditEntry>();
                }
                else
                {
                    entries = new List<AuditEntry>();
                }

                entries.Add(new AuditEntry
                {
                    TimestampUtc = DateTime.UtcNow,
                    Key = key,
                    OldValue = oldValue?.ToString() ?? "(null)",
                    NewValue = newValue?.ToString() ?? "(null)",
                    Source = "MOTO AI"
                });

                if (entries.Count > 100)
                    entries = entries.Skip(entries.Count - 100).ToList();

                File.WriteAllText(
                    _auditPath,
                    JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Audit best-effort.
            }
        }

        private sealed class AuditEntry
        {
            public DateTime TimestampUtc { get; init; }
            public string Key { get; init; } = string.Empty;
            public string OldValue { get; init; } = string.Empty;
            public string NewValue { get; init; } = string.Empty;
            public string Source { get; init; } = string.Empty;
        }

        private sealed class SettingsEngineAdapter : ISettingsStore
        {
            private readonly SettingsEngine _engine;

            public SettingsEngineAdapter(SettingsEngine engine)
            {
                _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            }

            public object? GetRaw(string key) => _engine.GetRaw(key);

            public void Set(string key, object value) => _engine.Set(key, value);
        }
    }
}
