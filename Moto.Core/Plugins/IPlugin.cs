// Moto.Core/Plugins/IPlugin.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Moto.Core.Plugins
{
    /// <summary>
    /// Contrat pour un plugin IA MOTO.
    /// Chaque plugin peut :
    /// - Enregistrer ses propres paramètres dans SettingsCatalog
    /// - Fournir des suggestions/commands via IA
    /// - S'intégrer dans l'UI (panneau dédié ou menu)
    /// </summary>
    public interface IPlugin : IDisposable
    {
        /// <summary>Identifiant unique du plugin (ex: "python-assistant").</summary>
        string Id { get; }

        /// <summary>Nom affiché dans l'UI.</summary>
        string DisplayName { get; }

        /// <summary>Version du plugin (semver).</summary>
        string Version { get; }

        /// <summary>Description courte pour le catalogue.</summary>
        string Description { get; }

        /// <summary>
        /// Paramètres propres au plugin.
        /// Ces paramètres sont automatiquement intégrés dans SettingsCatalog
        /// via le préfixe "plugin.{id}." (ex: "plugin.python-assistant.auto_import").
        /// </summary>
        IReadOnlyList<PluginSettingDefinition> Settings { get; }

        /// <summary>
        /// Initialise le plugin avec son scope de paramètres.
        /// Appelé une seule fois au chargement.
        /// </summary>
        Task InitializeAsync(PluginContext context);

        /// <summary>
        /// Exécute une commande plugin (ex: "/python format").
        /// Retourne le résultat ou null si non géré.
        /// </summary>
        Task<string?> ExecuteCommandAsync(string command, string context);

        /// <summary>
        /// Génère des suggestions proactives pour le fichier courant.
        /// </summary>
        Task<IReadOnlyList<PluginSuggestion>> GetSuggestionsAsync(string filePath, string content);
    }

    /// <summary>
    /// Contexte fourni au plugin lors de l'initialisation.
    /// </summary>
    public sealed class PluginContext
    {
        public string WorkspaceRoot { get; init; } = string.Empty;
        public IPluginSettingsAccessor Settings { get; init; } = null!;
        public IServiceProvider Services { get; init; } = null!;
    }

    /// <summary>
    /// Accès typé aux paramètres du plugin.
    /// </summary>
    public interface IPluginSettingsAccessor
    {
        T Get<T>(string key, T defaultValue);
        void Set<T>(string key, T value);
        event Action<string, object>? Changed;
    }

    /// <summary>
    /// Définition d'un paramètre plugin (auto-enregistré dans SettingsCatalog).
    /// </summary>
    public sealed record PluginSettingDefinition(
        string Key,           // Clé sans préfixe (ex: "auto_import")
        string DisplayName,
        string Description,
        SettingType Type,     // Toggle/Int/Enum/String
        object DefaultValue,
        string[]? EnumValues = null
    );

    /// <summary>
    /// Suggestion générée par un plugin.
    /// </summary>
    public sealed record PluginSuggestion(
        string Title,
        string Description,
        string Action,        // Commande à exécuter
        double Confidence
    );
}
