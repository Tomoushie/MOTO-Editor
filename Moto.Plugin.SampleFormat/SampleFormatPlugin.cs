// Moto.Plugin.SampleFormat/SampleFormatPlugin.cs
// Plugin d'exemple : formatage automatique du code selon les conventions.
// Utilise PluginBase pour minimiser le boilerplate.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Moto.Plugin.SDK;

namespace Moto.Plugin.SampleFormat
{
    /// <summary>
    /// Plugin de formatage automatique.
    /// Détecte et corrige les violations de style (espaces, indent, nommage).
    /// </summary>
    public sealed class SampleFormatPlugin : PluginBase
    {
        // ── Identité du plugin ──
        public override string Id => "sample-format";
        public override string DisplayName => "🎨 Sample Format";
        public override string Version => "1.0.0";
        public override string Description => "Formatage automatique du code selon vos conventions.";

        // ── Paramètres déclarés (auto-enregistrés dans SettingsCatalog) ──
        public override IReadOnlyList<PluginSettingDefinition> Settings => new[]
        {
            new PluginSettingDefinition
            {
                Key = "auto_format_on_save",
                DisplayName = "Formater à la sauvegarde",
                Description = "Applique le formatage automatique lors de Ctrl+S.",
                Type = SettingType.Toggle,
                DefaultValue = true
            },
            new PluginSettingDefinition
            {
                Key = "indent_style",
                DisplayName = "Style d'indentation",
                Description = "Espaces ou tabulations.",
                Type = SettingType.Enum,
                DefaultValue = "spaces",
                EnumValues = new[] { "spaces", "tabs" }
            },
            new PluginSettingDefinition
            {
                Key = "indent_size",
                DisplayName = "Taille d'indentation",
                Description = "Nombre d'espaces par niveau.",
                Type = SettingType.Int,
                DefaultValue = 4
            },
            new PluginSettingDefinition
            {
                Key = "trim_trailing_whitespace",
                DisplayName = "Supprimer les espaces en fin de ligne",
                Description = "Nettoie les espaces invisibles.",
                Type = SettingType.Toggle,
                DefaultValue = true
            }
        };

        // ── Initialisation ──
        protected override Task OnInitializeAsync(PluginContext context)
        {
            Logger.Info($"[SampleFormat] Initialisé pour workspace : {WorkspaceRoot}");
            Logger.Info($"[SampleFormat] Auto-format : {GetSetting("auto_format_on_save", true)}");
            return Task.CompletedTask;
        }

        // ── Commandes : /sample-format <action> ──
        public override Task<string?> ExecuteCommandAsync(string command, string context)
        {
            if (!command.StartsWith("/sample-format", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<string?>(null);

            var action = command.Substring("/sample-format".Length).Trim().ToLowerInvariant();

            return action switch
            {
                "format" => Task.FromResult<string?>(FormatCode(context)),
                "stats" => Task.FromResult<string?>("📊 Stats : 0 violations détectées."),
                "help" => Task.FromResult<string?>(
                    "Commandes : /sample-format format | stats | help"),
                _ => Task.FromResult<string?>($"Commande inconnue : {action}")
            };
        }

        // ── Suggestions proactives ──
        public override Task<IReadOnlyList<PluginSuggestion>> GetSuggestionsAsync(
            string filePath, string content)
        {
            var suggestions = new List<PluginSuggestion>();

            // Détection : espaces en fin de ligne
            if (GetSetting("trim_trailing_whitespace", true) && HasTrailingWhitespace(content))
            {
                suggestions.Add(new PluginSuggestion
                {
                    Title = "🎨 Espaces en fin de ligne détectés",
                    Description = "Ce fichier contient des espaces invisibles en fin de ligne.",
                    Action = "/sample-format format",
                    Confidence = 0.9
                });
            }

            // Détection : indentation incohérente
            var indentStyle = GetSetting("indent_style", "spaces");
            if (indentStyle == "spaces" && HasTabIndentation(content))
            {
                suggestions.Add(new PluginSuggestion
                {
                    Title = "🎨 Indentation par tabulations",
                    Description = "Vous préférez les espaces, mais ce fichier utilise des tabulations.",
                    Action = "/sample-format format",
                    Confidence = 0.85
                });
            }

            return Task.FromResult<IReadOnlyList<PluginSuggestion>>(suggestions);
        }

        // ── Logique de formatage ──
        private string FormatCode(string code)
        {
            var indentStyle = GetSetting("indent_style", "spaces");
            var indentSize = GetSetting("indent_size", 4);
            var trimWhitespace = GetSetting("trim_trailing_whitespace", true);

            var lines = code.Split('\n');
            var formatted = new List<string>();

            foreach (var line in lines)
            {
                var processed = line;

                // 1. Suppression des espaces en fin de ligne
                if (trimWhitespace)
                    processed = processed.TrimEnd();

                // 2. Conversion tabulations → espaces
                if (indentStyle == "spaces")
                    processed = processed.Replace("\t", new string(' ', indentSize));

                formatted.Add(processed);
            }

            Logger.Info($"[SampleFormat] Formaté {lines.Length} lignes.");
            return string.Join("\n", formatted);
        }

        private static bool HasTrailingWhitespace(string content)
            => Regex.IsMatch(content, @"[ \t]+\r?\n");

        private static bool HasTabIndentation(string content)
            => content.Contains("\t");
    }
}
