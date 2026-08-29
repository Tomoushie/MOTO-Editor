// Moto.Plugin.SDK/Contracts/Models.cs
// Modèles immuables partagés entre l'éditeur et les plugins.
using System;

namespace Moto.Plugin.SDK
{
    /// <summary>Type d'un paramètre plugin.</summary>
    public enum SettingType
    {
        Toggle,
        Int,
        Enum,
        String
    }

    /// <summary>Définition d'un paramètre plugin (auto-enregistré dans SettingsCatalog).</summary>
    public sealed class PluginSettingDefinition
    {
        /// <summary>Clé sans préfixe (ex: "auto_import").</summary>
        public string Key { get; init; } = string.Empty;

        /// <summary>Nom affiché dans les paramètres.</summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>Description / tooltip.</summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>Type du paramètre.</summary>
        public SettingType Type { get; init; } = SettingType.Toggle;

        /// <summary>Valeur par défaut.</summary>
        public object? DefaultValue { get; init; }

        /// <summary>Valeurs possibles si Type == Enum.</summary>
        public string[]? EnumValues { get; init; }
    }

    /// <summary>Suggestion générée par un plugin.</summary>
    public sealed class PluginSuggestion
    {
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        /// <summary>Commande à exécuter si l'utilisateur valide.</summary>
        public string Action { get; init; } = string.Empty;
        /// <summary>Confiance [0..1] ; en dessous du seuil, la suggestion est filtrée.</summary>
        public double Confidence { get; init; }
    }

    /// <summary>
    /// Contexte fourni au plugin lors de InitializeAsync.
    /// Donne accès aux paramètres scopés et au workspace courant.
    /// </summary>
    public sealed class PluginContext
    {
        /// <summary>Racine du projet ouvert (peut être vide si aucun projet).</summary>
        public string WorkspaceRoot { get; init; } = string.Empty;

        /// <summary>Accès typé aux paramètres du plugin.</summary>
        public IPluginSettingsAccessor Settings { get; init; } = null!;

        /// <summary>Logger fourni par l'éditeur.</summary>
        public IPluginLogger Logger { get; init; } = null!;
    }
}
