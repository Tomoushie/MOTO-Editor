// Moto.Core/Plugins/PluginRegistry.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moto.Core.Settings;

namespace Moto.Core.Plugins
{
    public sealed class PluginRegistry
    {
        private readonly SettingsEngine _settings;
        private readonly ILogger<PluginRegistry> _logger;
        private readonly List<IPlugin> _plugins = new();

        public PluginRegistry(SettingsEngine settings, ILogger<PluginRegistry> logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IReadOnlyList<IPlugin> GetActivePlugins() => _plugins.AsReadOnly();

        /// <summary>Ancienne API : ajoute juste à la liste, sans initialiser ni
        /// enregistrer ses réglages. Conservée pour compatibilité — le vrai point
        /// d'entrée d'un plugin est désormais <see cref="RegisterAsync"/>.</summary>
        public void Register(IPlugin plugin)
        {
            if (plugin is null)
                return;

            _plugins.Add(plugin);
            _logger.LogInformation("[Plugins] Plugin enregistré : {Id}", plugin.Id);
        }

        /// <summary>
        /// ★ AJOUT (02/09, "vrai système de plugins") : vrai point d'entrée d'un
        /// plugin — jusqu'ici Register() ajoutait juste l'objet à une liste, sans
        /// jamais appeler InitializeAsync ni tenir la promesse documentée depuis
        /// longtemps dans IPlugin.Settings ("automatiquement intégrés dans
        /// SettingsCatalog via le préfixe plugin.{id}."). Ce correctif construit
        /// enfin ce pont. Idempotent : appeler deux fois pour le même plugin.Id ne
        /// duplique ni le plugin dans la Galerie, ni ses réglages dans le
        /// catalogue.
        /// </summary>
        public async Task RegisterAsync(IPlugin plugin, string workspaceRoot, IServiceProvider? services = null)
        {
            if (plugin is null)
                return;

            if (_plugins.Any(p => p.Id == plugin.Id))
            {
                _logger.LogInformation("[Plugins] Plugin déjà enregistré, ignoré : {Id}", plugin.Id);
                return;
            }

            var context = new PluginContext
            {
                WorkspaceRoot = workspaceRoot ?? string.Empty,
                Settings = new PluginSettingsAccessor(_settings, plugin.Id),
                Services = services!
            };

            await plugin.InitializeAsync(context);

            foreach (var s in plugin.Settings)
            {
                var id = $"plugin.{plugin.Id}.{s.Key}";
                if (SettingsCatalog.ById(id) != null)
                    continue; // déjà injecté (ex. RegisterAsync rappelée avec une nouvelle instance)

                var def = new SettingDefinition
                {
                    Id = id,
                    Category = "Plugins",
                    Section = plugin.DisplayName,
                    Title = s.DisplayName,
                    Description = s.Description,
                    Type = s.Type,
                    Default = s.DefaultValue
                };
                if (s.EnumValues != null)
                    def.Options.AddRange(s.EnumValues);

                SettingsCatalog.All.Add(def);
            }

            _plugins.Add(plugin);
            _logger.LogInformation(
                "[Plugins] Plugin enregistré et initialisé : {Id} ({Count} réglage(s) ajoutés)",
                plugin.Id, plugin.Settings.Count);
        }
    }

    /// <summary>
    /// ★ AJOUT (02/09, "vrai système de plugins") : implémentation réelle de
    /// IPluginSettingsAccessor — jusqu'ici seule une FakeSettingsAccessor de test
    /// existait (Moto.Editor.Tests). Préfixe automatiquement chaque clé par
    /// "plugin.{pluginId}." pour isoler les réglages de chaque plugin sans que
    /// celui-ci ait à s'en soucier (il voit "auto_format_on_save", jamais le
    /// préfixe complet) — repose sur SettingsEngine, la même API plate déjà
    /// utilisée avec succès ailleurs dans le projet (persistance de session,
    /// réglages IA Locale).
    /// </summary>
    internal sealed class PluginSettingsAccessor : IPluginSettingsAccessor
    {
        private readonly SettingsEngine _settings;
        private readonly string _prefix;
        private event Action<string, object>? _changed;

        public PluginSettingsAccessor(SettingsEngine settings, string pluginId)
        {
            _settings = settings;
            _prefix = $"plugin.{pluginId}.";
            _settings.SettingChanged += (id, value) =>
            {
                if (value != null && id.StartsWith(_prefix, StringComparison.Ordinal))
                    _changed?.Invoke(id.Substring(_prefix.Length), value);
            };
        }

        public T Get<T>(string key, T defaultValue) => _settings.Get(_prefix + key, defaultValue);
        public void Set<T>(string key, T value) => _settings.Set(_prefix + key, value);

        public event Action<string, object>? Changed
        {
            add => _changed += value;
            remove => _changed -= value;
        }
    }
}
