// Moto.Core/Settings/Profiles/ProfileManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Moto.Core.Settings.Profiles
{
    /// <summary>
    /// Profil de configuration : preset de paramètres pour un niveau d'expertise.
    /// </summary>
    public sealed record ConfigurationProfile(
        string Id,
        string DisplayName,
        string Description,
        Dictionary<string, object> Settings
    );

    /// <summary>
    /// Gère les profils de configuration (débutant, expert, turbo).
    /// </summary>
    public sealed class ProfileManager
    {
        private readonly string _profilesDirectory;
        private readonly ISettingsStore _settings;
        private readonly ILogger<ProfileManager> _logger;

        public ProfileManager(SettingsEngine settings, ILogger<ProfileManager> logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _profilesDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MotoEditor", "profiles");
            Directory.CreateDirectory(_profilesDirectory);
        }

        /// <summary>
        /// Constructeur ajustable (tests) : permet d'injecter un ISettingsStore,
        /// un répertoire de profils et un logger optionnels.
        /// </summary>
        public ProfileManager(ISettingsStore settings, string? profilesDirectory = null, ILogger<ProfileManager>? logger = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? NullLogger<ProfileManager>.Instance;
            _profilesDirectory = profilesDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MotoEditor", "profiles");
            Directory.CreateDirectory(_profilesDirectory);
        }

        /// <summary>
        /// Profils prédéfinis (toujours disponibles).
        /// </summary>
        public static IReadOnlyList<ConfigurationProfile> BuiltInProfiles => new[]
        {
            new ConfigurationProfile(
                Id: "beginner",
                DisplayName: "🌱 Débutant",
                Description: "Interface simplifiée, tutoriels actifs, suggestions douces.",
                Settings: new Dictionary<string, object>
                {
                    ["cortex_mode"] = "Beginner",
                    ["ai_autosuggest"] = true,
                    ["ai_confidence_threshold"] = 0.9,
                    ["ui_minimap"] = false,
                    ["ui_terminal"] = false,
                    ["ui_explorer"] = true,
                    ["performance_mode"] = "Balanced",
                    ["beginner_tutorials"] = true,
                    ["beginner_explain_everything"] = true
                }
            ),
            new ConfigurationProfile(
                Id: "expert",
                DisplayName: "⚡ Expert",
                Description: "Tous les panneaux actifs, suggestions agressives, performance max.",
                Settings: new Dictionary<string, object>
                {
                    ["cortex_mode"] = "Expert",
                    ["ai_autosuggest"] = true,
                    ["ai_confidence_threshold"] = 0.5,
                    ["ui_minimap"] = true,
                    ["ui_terminal"] = true,
                    ["ui_explorer"] = true,
                    ["performance_mode"] = "Turbo",
                    ["beginner_tutorials"] = false,
                    ["beginner_explain_everything"] = false
                }
            ),
            new ConfigurationProfile(
                Id: "turbo",
                DisplayName: "🚀 Turbo",
                Description: "Performance maximale, UI minimaliste, IA en arrière-plan.",
                Settings: new Dictionary<string, object>
                {
                    ["cortex_mode"] = "Turbo",
                    ["ai_autosuggest"] = false,
                    ["ai_confidence_threshold"] = 0.7,
                    ["ui_minimap"] = false,
                    ["ui_terminal"] = false,
                    ["ui_explorer"] = false,
                    ["performance_mode"] = "Ultra",
                    ["beginner_tutorials"] = false,
                    ["beginner_explain_everything"] = false
                }
            )
        };

        /// <summary>
        /// Applique un profil (écrase les paramètres actuels).
        /// </summary>
        public void ApplyProfile(ConfigurationProfile profile)
        {
            _logger.LogInformation("[Profiles] Application du profil : {Name}", profile.DisplayName);

            foreach (var (key, value) in profile.Settings)
            {
                _settings.Set(key, value);
            }

            _settings.Set("active_profile", profile.Id);
        }

        /// <summary>
        /// Sauvegarde la configuration actuelle comme profil custom.
        /// </summary>
        public void SaveCurrentAsProfile(string profileName)
        {
            var allSettings = SettingsCatalog.GetAll();
            var currentSettings = new Dictionary<string, object>();

            foreach (var def in allSettings)
            {
                var value = _settings.GetRaw(def.Id);
                if (value != null)
                    currentSettings[def.Id] = value;
            }

            var profile = new ConfigurationProfile(
                Id: profileName.ToLowerInvariant().Replace(" ", "-"),
                DisplayName: profileName,
                Description: "Profil personnalisé",
                Settings: currentSettings
            );

            var path = Path.Combine(_profilesDirectory, $"{profile.Id}.json");
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);

            _logger.LogInformation("[Profiles] Sauvegardé : {Name} → {Path}", profileName, path);
        }

        /// <summary>
        /// Charge les profils custom depuis le disque.
        /// </summary>
        public IReadOnlyList<ConfigurationProfile> LoadCustomProfiles()
        {
            var profiles = new List<ConfigurationProfile>(BuiltInProfiles);

            foreach (var file in Directory.GetFiles(_profilesDirectory, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var profile = JsonSerializer.Deserialize<ConfigurationProfile>(json);
                    if (profile != null)
                        profiles.Add(profile);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Profiles] Erreur chargement {File}", file);
                }
            }

            return profiles;
        }

        /// <summary>
        /// Retourne le profil actif actuel.
        /// </summary>
        public string? GetActiveProfileId()
            => _settings.GetString("active_profile", defaultValue: null);
    }
}
