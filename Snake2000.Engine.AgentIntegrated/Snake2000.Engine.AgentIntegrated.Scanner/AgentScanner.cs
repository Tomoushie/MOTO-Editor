using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Snake2000.Engine.AgentIntegrated.Core;

namespace Snake2000.Engine.AgentIntegrated.Scanner
{
    /// <summary>
    /// AgentScanner : scanne l'arborescence Engine/Game/App et extrait la structure du projet.
    /// Utilise des Regex compilées pour rester léger sans dépendance à Roslyn.
    /// </summary>
    public class AgentScanner
    {
        public AgentResult Scan(AgentContext context)
        {
            var result = new AgentResult
            {
                ModuleName = "Scanner",
                Status = "success",
                Summary = "Scan and parsing completed."
            };

            // 1. Définition des cibles de scan (Architecture Snake2000)
            // Fallback sur le répertoire courant si RootPath n'est pas défini dans le contexte
            var rootPath = context.RootPath ?? Directory.GetCurrentDirectory();
            var basePaths = new[]
            {
                Path.Combine(rootPath, "Engine"),
                Path.Combine(rootPath, "Game"),
                Path.Combine(rootPath, "App")
            };

            var projectMap = new Dictionary<string, object>();
            var allFiles = new List<string>();

            // 2. Regex compilées pour des performances optimales (approche légère)
            var namespaceRegex = new Regex(@"\bnamespace\s+([\w\.]+)", RegexOptions.Compiled);
            var classRegex = new Regex(@"\bclass\s+(\w+)", RegexOptions.Compiled);
            var interfaceRegex = new Regex(@"\binterface\s+(\w+)", RegexOptions.Compiled);

            foreach (var basePath in basePaths)
            {
                if (!Directory.Exists(basePath)) continue;

                var moduleFiles = Directory.GetFiles(basePath, "*.cs", SearchOption.AllDirectories);
                allFiles.AddRange(moduleFiles);

                // Utilisation de HashSet pour éviter les doublons automatiquement
                var moduleData = new Dictionary<string, object>
                {
                    ["Namespaces"] = new HashSet<string>(),
                    ["Classes"] = new HashSet<string>(),
                    ["Interfaces"] = new HashSet<string>(),
                    ["Systems"] = new HashSet<string>()
                };

                foreach (var file in moduleFiles)
                {
                    // Lecture synchrone, suffisante pour un scan de projet standard
                    var content = File.ReadAllText(file);

                    // 3. Extraction des namespaces (gère les block-scoped et file-scoped namespaces C# 10+)
                    foreach (Match match in namespaceRegex.Matches(content))
                        ((HashSet<string>)moduleData["Namespaces"]).Add(match.Groups[1].Value);

                    // 4. Extraction des classes et détection des "Systems"
                    foreach (Match match in classRegex.Matches(content))
                    {
                        var className = match.Groups[1].Value;
                        ((HashSet<string>)moduleData["Classes"]).Add(className);

                        // Détection des systèmes basée sur la convention de nommage
                        if (className.EndsWith("System", StringComparison.OrdinalIgnoreCase))
                            ((HashSet<string>)moduleData["Systems"]).Add(className);
                    }

                    // 5. Extraction des interfaces
                    foreach (Match match in interfaceRegex.Matches(content))
                        ((HashSet<string>)moduleData["Interfaces"]).Add(match.Groups[1].Value);
                }

                // On utilise le nom du dossier comme clé de la ProjectMap
                projectMap[Path.GetFileName(basePath)] = moduleData;
            }

            result.Details.Add($"Scanned {allFiles.Count} files across Engine/Game/App.");

            // Payload structuré pour les agents suivants (ex: AgentAnalyzer)
            result.Payload["Files"] = allFiles;
            result.Payload["ProjectMap"] = projectMap;

            return result;
        }
    }
}
