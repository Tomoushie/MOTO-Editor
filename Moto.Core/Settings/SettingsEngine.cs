// Moto.Core/Settings/SettingsEngine.cs (ajouts à la classe existante)
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Moto.Core.Settings
{
    public enum SettingsScope
    {
        Global,   // %AppData%/MotoEditor/settings.json
        Project   // .moto/settings.json
    }

    public partial class SettingsEngine
    {
        private string? _projectSettingsPath;
        private Dictionary<string, object>? _projectSettings;

        /// <summary>
        /// Définit le workspace courant pour charger les paramètres projet.
        /// </summary>
        public void SetWorkspace(string workspaceRoot)
        {
            _projectSettingsPath = Path.Combine(workspaceRoot, ".moto", "settings.json");
            LoadProjectSettings();
        }

        /// <summary>
        /// Récupère un paramètre avec résolution de scope (projet > global).
        /// </summary>
        public T Get<T>(string key, T defaultValue, SettingsScope scope = SettingsScope.Global)
        {
            if (scope == SettingsScope.Project && _projectSettings != null)
            {
                if (_projectSettings.TryGetValue(key, out var projectValue))
                {
                    try
                    {
                        return (T)Convert.ChangeType(projectValue, typeof(T));
                    }
                    catch
                    {
                        // Fallback sur global si conversion échoue
                    }
                }
            }

            // Fallback sur global
            return Get<T>(key, defaultValue);
        }

        /// <summary>
        /// Définit un paramètre dans le scope spécifié.
        /// </summary>
        public void Set(string key, object value, SettingsScope scope = SettingsScope.Global)
        {
            if (scope == SettingsScope.Project)
            {
                SetProjectSetting(key, value);
            }
            else
            {
                Set(key, value); // méthode existante (global)
            }
        }

        private void LoadProjectSettings()
        {
            _projectSettings = new Dictionary<string, object>();

            if (_projectSettingsPath == null || !File.Exists(_projectSettingsPath))
                return;

            try
            {
                var json = File.ReadAllText(_projectSettingsPath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (loaded != null)
                    _projectSettings = loaded;
            }
            catch
            {
                // Fichier corrompu : on repart vide
            }
        }

        private void SetProjectSetting(string key, object value)
        {
            _projectSettings ??= new Dictionary<string, object>();
            _projectSettings[key] = value;
            SaveProjectSettings();
        }

        private void SaveProjectSettings()
        {
            if (_projectSettingsPath == null || _projectSettings == null)
                return;

            try
            {
                var dir = Path.GetDirectoryName(_projectSettingsPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_projectSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_projectSettingsPath, json);
            }
            catch
            {
                // Sauvegarde impossible : ignoré
            }
        }

        /// <summary>
        /// Retourne le scope d'un paramètre (projet s'il existe, sinon global).
        /// </summary>
        public SettingsScope GetScope(string key)
        {
            if (_projectSettings != null && _projectSettings.ContainsKey(key))
                return SettingsScope.Project;
            return SettingsScope.Global;
        }
    }
}
