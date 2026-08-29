// Moto.Core/AI/Internal/ContextEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// Moteur de compréhension contextuelle profonde.
    /// MOTO AI lit le fichier ouvert, l'arborescence, et l'historique utilisateur
    /// pour construire une conscience globale du projet.
    /// </summary>
    public class ContextEngine
    {
        private readonly ProjectUnderstandingEngine _understanding = new ProjectUnderstandingEngine();

        /// <summary>
        /// Contexte global construit par MOTO AI.
        /// </summary>
        public class GlobalContext
        {
            public ProjectMap ProjectMap { get; set; }
            public string ActiveFile { get; set; } = string.Empty;
            public string ActiveFileContent { get; set; } = string.Empty;
            public List<string> ActiveFileSymbols { get; } = new List<string>();
            public List<string> RecentFiles { get; } = new List<string>();
            public List<string> RecentCommands { get; } = new List<string>();
            public List<string> RecentErrors { get; } = new List<string>();
            public List<string> CreatedModules { get; } = new List<string>();
        }

        /// <summary>
        /// Construit le contexte global complet.
        /// C'est la "conscience" de MOTO AI.
        /// </summary>
        public GlobalContext BuildContext(string workspacePath, string activeFile, UserHistoryEngine history)
        {
            var context = new GlobalContext
            {
                ActiveFile = activeFile ?? string.Empty
            };

            // 1. Lire l'arborescence complète
            context.ProjectMap = _understanding.BuildMap(workspacePath);

            // 2. Lire le fichier actif
            if (!string.IsNullOrWhiteSpace(activeFile) && File.Exists(activeFile))
            {
                try
                {
                    context.ActiveFileContent = File.ReadAllText(activeFile);
                    context.ActiveFileSymbols = ExtractSymbols(context.ActiveFileContent);
                }
                catch
                {
                    // Fichier illisible.
                }
            }

            // 3. Lire l'historique utilisateur
            if (history != null)
            {
                context.RecentFiles.AddRange(history.GetRecentFiles(20));
                context.RecentCommands.AddRange(history.GetRecentCommands(20));
                context.RecentErrors.AddRange(history.GetRecentErrors(10));
                context.CreatedModules.AddRange(history.GetCreatedModules());
            }

            return context;
        }

        /// <summary>
        /// Extrait les symboles d'un fichier actif.
        /// </summary>
        private List<string> ExtractSymbols(string content)
        {
            var symbols = new List<string>();

            var classMatches = System.Text.RegularExpressions.Regex.Matches(content, @"\bclass\s+(\w+)");
            foreach (System.Text.RegularExpressions.Match m in classMatches)
                symbols.Add($"class {m.Groups[1].Value}");

            var interfaceMatches = System.Text.RegularExpressions.Regex.Matches(content, @"\binterface\s+(\w+)");
            foreach (System.Text.RegularExpressions.Match m in interfaceMatches)
                symbols.Add($"interface {m.Groups[1].Value}");

            var methodMatches = System.Text.RegularExpressions.Regex.Matches(content, @"\b(?:public|private|protected|internal)\s+\w+\s+(\w+)\s*\(");
            foreach (System.Text.RegularExpressions.Match m in methodMatches)
                symbols.Add($"method {m.Groups[1].Value}");

            return symbols;
        }
    }
}
