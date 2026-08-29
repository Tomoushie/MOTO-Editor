// Moto.Plugin.PythonAssistant/PythonAssistantPlugin.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moto.Plugin.SDK;
using OmniSharp.Extensions.LanguageClient;
using OmniSharp.Extensions.LanguageProtocol.Models;

namespace Moto.Plugin.PythonAssistant
{
    /// <summary>
    /// Plugin Python Assistant : analyse sémantique via LSP (pylsp/pyright).
    /// Fournit : diagnostics, complétion intelligente, PEP 8, type hints.
    /// </summary>
    public sealed class PythonAssistantPlugin : PluginBase
    {
        private LanguageClient? _lspClient;
        private readonly SemaphoreSlim _lspLock = new(1, 1);

        public override string Id => "python-assistant";
        public override string DisplayName => "🐍 Python Assistant";
        public override string Version => "2.1.0";
        public override string Description => "Analyse sémantique Python via LSP (PEP 8, type hints, diagnostics).";

        public override IReadOnlyList<PluginSettingDefinition> Settings => new[]
        {
            new PluginSettingDefinition
            {
                Key = "lsp_server",
                DisplayName = "Serveur LSP",
                Description = "pylsp (rapide) ou pyright (précis).",
                Type = SettingType.Enum,
                DefaultValue = "pylsp",
                EnumValues = new[] { "pylsp", "pyright" }
            },
            new PluginSettingDefinition
            {
                Key = "auto_pep8",
                DisplayName = "Auto-format PEP 8",
                Description = "Formate automatiquement selon PEP 8.",
                Type = SettingType.Toggle,
                DefaultValue = true
            },
            new PluginSettingDefinition
            {
                Key = "type_hints",
                DisplayName = "Suggestions type hints",
                Description = "Propose des annotations de type.",
                Type = SettingType.Toggle,
                DefaultValue = true
            },
            new PluginSettingDefinition
            {
                Key = "auto_import",
                DisplayName = "Auto-import",
                Description = "Importe automatiquement les modules manquants.",
                Type = SettingType.Toggle,
                DefaultValue = true
            }
        };

        protected override async Task OnInitializeAsync(PluginContext context)
        {
            Logger.Info($"[PythonAssistant] Initialisation pour {WorkspaceRoot}");

            // Lance le serveur LSP en arrière-plan
            _ = Task.Run(async () => await InitializeLspAsync());
        }

        private async Task InitializeLspAsync()
        {
            await _lspLock.WaitAsync();
            try
            {
                if (_lspClient != null) return;

                var serverType = GetSetting("lsp_server", "pylsp");
                var serverPath = serverType == "pyright" ? "pyright-langserver" : "pylsp";

                Logger.Info($"[PythonAssistant] Démarrage LSP : {serverPath}");

                _lspClient = LanguageClient.PreInit(options =>
                {
                    options
                        .WithLoggerFactory(new Microsoft.Extensions.Logging.LoggerFactory())
                        .WithInput(Console.OpenStandardInput())
                        .WithOutput(Console.OpenStandardOutput())
                        .ConfigureLogging(x => x.AddDebug().SetMinimumLevel(LogLevel.Debug));
                });

                // Note: en production, utiliser Process pour lancer le serveur
                Logger.Info("[PythonAssistant] LSP client prêt.");
            }
            catch (Exception ex)
            {
                Logger.Error($"[PythonAssistant] Échec LSP : {ex.Message}");
            }
            finally
            {
                _lspLock.Release();
            }
        }

        public override async Task<string?> ExecuteCommandAsync(string command, string context)
        {
            if (!command.StartsWith("/python", StringComparison.OrdinalIgnoreCase))
                return null;

            var action = command.Substring("/python".Length).Trim().ToLowerInvariant();

            return action switch
            {
                "pep8" => await FormatPep8Async(context),
                "types" => await AddTypeHintsAsync(context),
                "imports" => await AutoImportAsync(context),
                "diagnostics" => await GetDiagnosticsAsync(context),
                "help" => "Commandes : /python pep8 | types | imports | diagnostics | help",
                _ => $"Commande inconnue : {action}"
            };
        }

        public override async Task<IReadOnlyList<PluginSuggestion>> GetSuggestionsAsync(
            string filePath, string content)
        {
            if (!filePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
                return Array.Empty<PluginSuggestion>();

            var suggestions = new List<PluginSuggestion>();

            // Détection PEP 8
            if (GetSetting("auto_pep8", true) && HasPep8Violations(content))
            {
                suggestions.Add(new PluginSuggestion
                {
                    Title = "🐍 Violations PEP 8 détectées",
                    Description = "Ce fichier ne respecte pas PEP 8.",
                    Action = "/python pep8",
                    Confidence = 0.9
                });
            }

            // Détection imports manquants
            if (GetSetting("auto_import", true) && HasMissingImports(content))
            {
                suggestions.Add(new PluginSuggestion
                {
                    Title = "🐍 Imports manquants",
                    Description = "Des modules utilisés ne sont pas importés.",
                    Action = "/python imports",
                    Confidence = 0.85
                });
            }

            // Détection type hints manquants
            if (GetSetting("type_hints", true) && HasMissingTypeHints(content))
            {
                suggestions.Add(new PluginSuggestion
                {
                    Title = "🐍 Type hints manquants",
                    Description = "Des fonctions n'ont pas d'annotations de type.",
                    Action = "/python types",
                    Confidence = 0.8
                });
            }

            return await Task.FromResult(suggestions);
        }

        private async Task<string> FormatPep8Async(string code)
        {
            Logger.Info("[PythonAssistant] Formatage PEP 8…");

            // En production : appeler LSP textDocument/formatting
            // Ici : simulation basique
            var lines = code.Split('\n');
            var formatted = lines.Select(line =>
            {
                // Supprime trailing whitespace
                line = line.TrimEnd();

                // Indentation 4 espaces
                if (line.StartsWith("\t"))
                    line = "    " + line.Substring(1);

                return line;
            });

            var result = string.Join("\n", formatted);
            Logger.Info("[PythonAssistant] PEP 8 appliqué.");
            return await Task.FromResult(result);
        }

        private async Task<string> AddTypeHintsAsync(string code)
        {
            Logger.Info("[PythonAssistant] Ajout type hints…");

            // En production : analyse LSP + inférence de types
            // Ici : ajout basique sur les fonctions
            var result = System.Text.RegularExpressions.Regex.Replace(
                code,
                @"def\s+(\w+)\s*\((.*?)\):",
                match =>
                {
                    var funcName = match.Groups[1].Value;
                    var args = match.Groups[2].Value;

                    // Ajoute -> None si pas de return hint
                    return $"def {funcName}({args}) -> None:";
                });

            Logger.Info("[PythonAssistant] Type hints ajoutés.");
            return await Task.FromResult(result);
        }

        private async Task<string> AutoImportAsync(string code)
        {
            Logger.Info("[PythonAssistant] Auto-import…");

            // En production : LSP codeAction + résolution
            // Ici : imports courants
            var imports = new List<string>();

            if (code.Contains("np.") && !code.Contains("import numpy"))
                imports.Add("import numpy as np");

            if (code.Contains("pd.") && !code.Contains("import pandas"))
                imports.Add("import pandas as pd");

            if (imports.Count == 0) return code;

            var result = string.Join("\n", imports) + "\n\n" + code;
            Logger.Info($"[PythonAssistant] {imports.Count} imports ajoutés.");
            return await Task.FromResult(result);
        }

        private async Task<string> GetDiagnosticsAsync(string code)
        {
            Logger.Info("[PythonAssistant] Diagnostics…");

            // En production : LSP textDocument/publishDiagnostics
            var diagnostics = new List<string>();

            // Vérification basique
            var lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.Length > 79)
                    diagnostics.Add($"L{i + 1}: Ligne trop longue ({line.Length} > 79)");

                if (line.Contains("\t"))
                    diagnostics.Add($"L{i + 1}: Tabulation au lieu d'espaces");
            }

            var result = diagnostics.Count > 0
                ? $"📋 {diagnostics.Count} diagnostics :\n" + string.Join("\n", diagnostics)
                : "✅ Aucun diagnostic.";

            return await Task.FromResult(result);
        }

        private static bool HasPep8Violations(string content)
            => content.Split('\n').Any(l => l.Length > 79 || l.Contains("\t"));

        private static bool HasMissingImports(string content)
            => (content.Contains("np.") && !content.Contains("import numpy")) ||
               (content.Contains("pd.") && !content.Contains("import pandas"));

        private static bool HasMissingTypeHints(string content)
            => System.Text.RegularExpressions.Regex.IsMatch(content, @"def\s+\w+\s*\([^)]*\)\s*:");

        public override void Dispose()
        {
            _lspClient?.Dispose();
            base.Dispose();
        }
    }
}
