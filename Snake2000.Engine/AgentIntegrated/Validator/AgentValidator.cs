using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Snake2000.Engine.AgentIntegrated.Core;

namespace Snake2000.Engine.AgentIntegrated.Validator
{
    /// <summary>
    /// Validator : audite la cohérence finale du projet après intégration.
    /// Vérifie les namespaces, les contrats d'interfaces, les systèmes et les signatures publiques.
    /// Ne modifie JAMAIS le code source (approche Read-Only).
    /// </summary>
    public class AgentValidator
    {
        // Regex compilées pour l'audit de conformité
        private static readonly Regex NamespaceRegex = new Regex(@"\bnamespace\s+([\w\.]+)", RegexOptions.Compiled);
        private static readonly Regex ClassRegex = new Regex(@"\b(public|internal)\s+class\s+(\w+)", RegexOptions.Compiled);
        private static readonly Regex InterfaceRegex = new Regex(@"\b(public|internal)\s+interface\s+(\w+)", RegexOptions.Compiled);
        private static readonly Regex SystemRegex = new Regex(@"\bclass\s+(\w+System)", RegexOptions.Compiled);

        public AgentResult Validate(AgentContext context, AgentResult connectorResult)
        {
            var result = new AgentResult
            {
                ModuleName = "Validator",
                Status = "success",
                Summary = "Validation completed."
            };

            var report = new ValidationReport();
            var rootPath = context.RootPath ?? Directory.GetCurrentDirectory();

            var basePaths = new[]
            {
                Path.Combine(rootPath, "Engine"),
                Path.Combine(rootPath, "Game"),
                Path.Combine(rootPath, "App")
            };

            var globalClasses = new HashSet<string>();
            var globalInterfaces = new HashSet<string>();

            // 1. Relire le projet et extraire les métadonnées
            foreach (var basePath in basePaths)
            {
                if (!Directory.Exists(basePath)) continue;
                var moduleName = Path.GetFileName(basePath);
                var files = Directory.GetFiles(basePath, "*.cs", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    var content = File.ReadAllText(file);
                    var relativePath = Path.GetRelativePath(rootPath, file);

                    // A. Vérification des Namespaces (Doit préfixer par Snake2000.[Module])
                    var nsMatch = NamespaceRegex.Match(content);
                    if (nsMatch.Success)
                    {
                        var ns = nsMatch.Groups[1].Value;
                        if (!ns.StartsWith($"Snake2000.{moduleName}"))
                        {
                            report.NamespaceViolations.Add($"[{relativePath}] Le namespace '{ns}' n'est pas cohérent avec le module '{moduleName}'.");
                        }
                    }

                    // B. Extraction des Classes et Interfaces pour vérification globale
                    foreach (Match match in ClassRegex.Matches(content))
                    {
                        globalClasses.Add(match.Groups[2].Value);
                    }

                    foreach (Match match in InterfaceRegex.Matches(content))
                    {
                        globalInterfaces.Add(match.Groups[2].Value);
                    }

                    // C. Vérification des Systèmes (Doivent avoir une interface)
                    foreach (Match match in SystemRegex.Matches(content))
                    {
                        var systemName = match.Groups[1].Value;
                        var expectedInterface = "I" + systemName;

                        if (!globalInterfaces.Contains(expectedInterface) && !content.Contains($"interface {expectedInterface}"))
                        {
                            report.OrphanSystems.Add($"[{relativePath}] Le système '{systemName}' n'a pas d'interface '{expectedInterface}' associée.");
                        }
                    }
                }
            }

            // 2. Vérification des dépendances et signatures (Cross-Check)
            // Vérifie que les interfaces attendues par le Connector ont bien été générées
            if (connectorResult?.Payload.ContainsKey("GeneratedFiles") == true)
            {
                var generatedFiles = connectorResult.Payload["GeneratedFiles"] as List<(string path, string content)>;
                if (generatedFiles != null)
                {
                    foreach (var genFile in generatedFiles)
                    {
                        // Vérifie que le fichier généré a bien été écrit sur le disque (ou est présent dans le payload)
                        // Ici on vérifie juste que son contenu déclare bien une classe/interface publique
                        if (!ClassRegex.IsMatch(genFile.content) && !InterfaceRegex.IsMatch(genFile.content))
                        {
                            report.InvalidSignatures.Add($"[{genFile.path}] Le fichier généré ne contient pas de déclaration publique valide.");
                        }
                    }
                }
            }

            // 3. Calcul du score de cohérence globale
            int totalChecks = Math.Max(1, globalClasses.Count + globalInterfaces.Count);
            int violationsCount = report.NamespaceViolations.Count + report.OrphanSystems.Count + report.InvalidSignatures.Count;

            report.GlobalCoherenceScore = Math.Max(0, 100 - (violationsCount * 5)); // -5% par violation

            // 4. Finalisation du rapport
            result.Details.Add($"Audited {globalClasses.Count} classes and {globalInterfaces.Count} interfaces.");
            result.Details.Add($"Global Coherence Score: {report.GlobalCoherenceScore}%");

            if (violationsCount > 0)
            {
                result.Status = "warning";
                result.Summary = $"Validation completed with {violationsCount} architectural violations.";
            }
            else
            {
                result.Summary = "Project is fully coherent and compliant.";
            }

            result.Payload["ValidationReport"] = report;
            return result;
        }
    }

    /// <summary>
    /// Structure de données pour le rapport de validation finale.
    /// Utilisé par l'orchestrateur pour décider de la suite du pipeline (Retry ou Deploy).
    /// </summary>
    public class ValidationReport
    {
        public List<string> NamespaceViolations { get; set; } = new();
        public List<string> OrphanSystems { get; set; } = new();
        public List<string> InvalidSignatures { get; set; } = new();
        public List<string> BrokenDependencies { get; set; } = new();

        /// <summary>
        /// Score de santé du projet (0 à 100).
        /// Un score < 80 devrait déclencher une alerte ou un rollback dans l'orchestrateur.
        /// </summary>
        public int GlobalCoherenceScore { get; set; } = 100;

        public bool IsHealthy() => GlobalCoherenceScore >= 80 && !NamespaceViolations.Any();
    }
}
