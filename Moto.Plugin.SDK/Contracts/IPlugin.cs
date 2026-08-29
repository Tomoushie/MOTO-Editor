// Moto.Plugin.SDK/Contracts/IPlugin.cs
// Contrat stable pour tout plugin MOTO.
// Versionné via SdkVersion pour permettre l'évolution sans casser les plugins existants.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Moto.Plugin.SDK
{
    /// <summary>
    /// Contrat d'un plugin MOTO.
    /// Implémentez cette interface dans votre assembly ; MOTO Editor le chargera
    /// depuis le dossier plugins/ via PluginRegistry.
    /// </summary>
    public interface IPlugin : IDisposable
    {
        /// <summary>Version du SDK utilisée par ce plugin (ex: "1.0").</summary>
        string SdkVersion { get; }

        /// <summary>Identifiant unique du plugin (ex: "python-assistant").</summary>
        string Id { get; }

        /// <summary>Nom affiché dans l'UI.</summary>
        string DisplayName { get; }

        /// <summary>Version semver du plugin.</summary>
        string Version { get; }

        /// <summary>Description courte.</summary>
        string Description { get; }

        /// <summary>
        /// Paramètres déclarés par le plugin.
        /// Ils sont automatiquement injectés dans SettingsCatalog
        /// sous la clé "plugin.{Id}.{Key}".
        /// </summary>
        IReadOnlyList<PluginSettingDefinition> Settings { get; }

        /// <summary>Initialisation unique au chargement.</summary>
        Task InitializeAsync(PluginContext context);

        /// <summary>Exécute une commande ("/monplugin action"). Retourne null si non géré.</summary>
        Task<string?> ExecuteCommandAsync(string command, string context);

        /// <summary>Suggestions proactives pour le fichier courant.</summary>
        Task<IReadOnlyList<PluginSuggestion>> GetSuggestionsAsync(string filePath, string content);
    }
}
