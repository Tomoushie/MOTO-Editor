// Moto.Plugin.SDK/Helpers/PluginBase.cs
// Classe de base optionnelle : réduit le boilerplate des plugins simples.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Moto.Plugin.SDK
{
    /// <summary>
    /// Base classe pour les plugins.
    /// Fournit : accès aux paramètres, logger, cycle de vie par défaut.
    /// Héritez de cette classe au lieu d'implémenter IPlugin directement
    /// pour bénéficier des helpers.
    /// </summary>
    public abstract class PluginBase : IPlugin
    {
        /// <summary>Version du SDK implémentée.</summary>
        public virtual string SdkVersion => "1.0";

        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract string Version { get; }
        public abstract string Description { get; }

        /// <summary>Paramètres par défaut : vide. Surchargez si besoin.</summary>
        public virtual IReadOnlyList<PluginSettingDefinition> Settings
            => Array.Empty<PluginSettingDefinition>();

        /// <summary>Accès aux paramètres après InitializeAsync.</summary>
        protected IPluginSettingsAccessor SettingsAccessor { get; private set; } = null!;

        /// <summary>Logger fourni par l'éditeur.</summary>
        protected IPluginLogger Logger { get; private set; } = null!;

        /// <summary>Racine du workspace courant.</summary>
        protected string WorkspaceRoot { get; private set; } = string.Empty;

        /// <summary>Appelé une fois au chargement. Stocke le contexte puis appelle OnInitializeAsync.</summary>
        public async Task InitializeAsync(PluginContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            SettingsAccessor = context.Settings ?? throw new ArgumentNullException(nameof(context.Settings));
            Logger = context.Logger ?? throw new ArgumentNullException(nameof(context.Logger));
            WorkspaceRoot = context.WorkspaceRoot;

            await OnInitializeAsync(context);
        }

        /// <summary>Surchargez pour votre logique d'initialisation.</summary>
        protected virtual Task OnInitializeAsync(PluginContext context) => Task.CompletedTask;

        /// <summary>Par défaut : aucune commande gérée.</summary>
        public virtual Task<string?> ExecuteCommandAsync(string command, string context)
            => Task.FromResult<string?>(null);

        /// <summary>Par défaut : aucune suggestion.</summary>
        public virtual Task<IReadOnlyList<PluginSuggestion>> GetSuggestionsAsync(string filePath, string content)
            => Task.FromResult<IReadOnlyList<PluginSuggestion>>(Array.Empty<PluginSuggestion>());

        /// <summary>Lecture typée d'un paramètre du plugin.</summary>
        protected T GetSetting<T>(string key, T defaultValue)
            => SettingsAccessor.Get(key, defaultValue);

        /// <summary>Écriture d'un paramètre du plugin.</summary>
        protected void SetSetting<T>(string key, T value)
            => SettingsAccessor.Set(key, value);

        /// <summary>Cleanup par défaut : vide.</summary>
        public virtual void Dispose() { }
    }
}
