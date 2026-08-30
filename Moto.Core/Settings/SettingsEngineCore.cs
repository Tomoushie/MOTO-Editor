// Moto.Core/Settings/SettingsEngineCore.cs
// Coeur de SettingsEngine : stockage persistant clé/valeur (%AppData%/MotoEditor/settings.json).
// Les autres fichiers SettingsEngine.*.cs ajoutent des fonctionnalités à cette classe partial.
// Ce fichier n'existait pas (seuls les "ajouts" avaient été écrits) : reconstruit ici.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Moto.Core.Settings
{
    public partial class SettingsEngine
    {
        /// <summary>Instance partagée par toute l'application (paramètres globaux).</summary>
        public static SettingsEngine Shared { get; } = new SettingsEngine();

        /// <summary>Notifié après chaque changement de valeur (id du paramètre, nouvelle valeur).</summary>
        public event Action<string, object?>? SettingChanged;

        private readonly string _settingsPath;
        private Dictionary<string, object?> _values = new();

        public SettingsEngine()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MotoEditor", "settings.json"))
        {
        }

        public SettingsEngine(string settingsPath)
        {
            _settingsPath = settingsPath;
            Load();
        }

        /// <summary>Récupère un paramètre du scope global (voir aussi l'overload avec SettingsScope).</summary>
        public T Get<T>(string key, T defaultValue)
        {
            if (_values.TryGetValue(key, out var raw) && raw != null)
            {
                try
                {
                    if (raw is JsonElement element)
                    {
                        var deserialized = element.Deserialize<T>();
                        return deserialized ?? defaultValue;
                    }
                    if (raw is T typed)
                        return typed;
                    return (T)Convert.ChangeType(raw, typeof(T));
                }
                catch
                {
                    // Valeur stockée incompatible avec T : on retombe sur le défaut.
                }
            }
            return defaultValue;
        }

        /// <summary>Définit un paramètre dans le scope global et notifie les abonnés.</summary>
        public void Set<T>(string key, T value)
        {
            _values[key] = value;
            Save();
            SettingChanged?.Invoke(key, value);
        }

        public object? GetRaw(string key) => _values.TryGetValue(key, out var v) ? v : null;

        public bool GetBool(string key, bool defaultValue = false) => Get(key, defaultValue);

        public int GetInt(string key, int defaultValue = 0) => Get(key, defaultValue);

        public string GetString(string key, string defaultValue = "") => Get(key, defaultValue ?? string.Empty);

        private void Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
                    if (loaded != null) _values = loaded;
                }
            }
            catch
            {
                // Fichier corrompu ou illisible : on repart d'un magasin vide plutôt que de planter.
                _values = new Dictionary<string, object?>();
            }
        }

        private void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_values, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch
            {
                // Sauvegarde impossible (disque plein, permissions...) : ignoré, ne doit jamais
                // empêcher l'application de fonctionner.
            }
        }
    }
}
