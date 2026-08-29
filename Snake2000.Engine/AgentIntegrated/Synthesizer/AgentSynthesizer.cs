using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Snake2000.Engine.AgentIntegrated.Analyzer;
using Snake2000.Engine.AgentIntegrated.Core;

namespace Snake2000.Engine.AgentIntegrated.Synthesizer
{
    /// <summary>
    /// Synthesizer : génère des stubs de code C# pour corriger les incohérences
    /// détectées par l'Analyzer (interfaces manquantes, systèmes orphelins).
    /// Prépare le terrain pour une génération sémantique via LLM local (Qwen/DeepSeek).
    /// </summary>
    public class AgentSynthesizer
    {
        // Templates de génération pour respecter les conventions C# et l'architecture Snake2000
        private const string InterfaceTemplate = @"using System;

namespace {Namespace}
{{
    /// <summary>
    /// Interface générée automatiquement pour supporter le système {SystemName}.
    /// </summary>
    public interface {InterfaceName}
    {{
        // TODO: Définir le contrat pour {SystemName}
        void Initialize();
        void Update(float deltaTime);
    }}
}}";

        private const string ClassTemplate = @"using System;

namespace {Namespace}
{{
    /// <summary>
    /// Implémentation générée automatiquement pour l'interface {InterfaceName}.
    /// </summary>
    public class {ClassName} : {InterfaceName}
    {{
        /// <inheritdoc />
        public void Initialize()
        {{
            // TODO: Implémenter la logique d'initialisation
            throw new NotImplementedException();
        }}

        /// <inheritdoc />
        public void Update(float deltaTime)
        {{
            // TODO: Implémenter la logique de mise à jour
            throw new NotImplementedException();
        }}
    }}
}}";

        public AgentResult Synthesize(AgentContext context, AgentResult analysisResult)
        {
            var result = new AgentResult
            {
                ModuleName = "Synthesizer",
                Status = "success",
                Summary = "Synthesis completed."
            };

            var generatedFiles = new List<(string path, string content)>();

            // 1. Extraction sécurisée du rapport d'analyse
            var report = analysisResult.Payload["Report"] as AnalysisReport;
            if (report == null)
            {
                result.Status = "warning";
                result.Summary = "No AnalysisReport found. Skipping synthesis.";
                result.Payload["GeneratedFiles"] = generatedFiles;
                return result;
            }

            var rootPath = context.RootPath ?? Directory.GetCurrentDirectory();

            // 2. Génération des interfaces manquantes (pour les Systèmes Orphelins)
            // Format attendu dans le rapport : "[Module] Système 'Name' sans abstraction d'interface 'IName'."
            foreach (var orphanSystem in report.OrphanSystems)
            {
                var (moduleName, systemName, interfaceName) = ParseSystemIssue(orphanSystem);
                if (string.IsNullOrEmpty(moduleName)) continue;

                var ns = $"Snake2000.{moduleName}.Systems";
                var content = InterfaceTemplate
                    .Replace("{Namespace}", ns)
                    .Replace("{SystemName}", systemName)
                    .Replace("{InterfaceName}", interfaceName);

                var filePath = Path.Combine(rootPath, moduleName, "Systems", $"{interfaceName}.cs");
                generatedFiles.Add((filePath, content));
            }

            // 3. Génération des implémentations manquantes (pour les Interfaces Orphelines)
            // Format attendu : "[Module] Interface 'IName' sans implémentation 'Name' détectée."
            foreach (var unimplemented in report.UnimplementedInterfaces)
            {
                var (moduleName, interfaceName, className) = ParseInterfaceIssue(unimplemented);
                if (string.IsNullOrEmpty(moduleName)) continue;

                var ns = $"Snake2000.{moduleName}.Components"; // Convention par défaut pour les implémentations
                var content = ClassTemplate
                    .Replace("{Namespace}", ns)
                    .Replace("{InterfaceName}", interfaceName)
                    .Replace("{ClassName}", className);

                var filePath = Path.Combine(rootPath, moduleName, "Components", $"{className}.cs");
                generatedFiles.Add((filePath, content));
            }

            // 4. Finalisation
            result.Details.Add($"Generated {generatedFiles.Count} stub files to resolve architectural issues.");

            // Payload prêt pour l'agent suivant (ex: FileWriteAgent ou LLM Agent pour remplir le code)
            result.Payload["GeneratedFiles"] = generatedFiles;

            if (generatedFiles.Count > 0)
            {
                result.Summary = $"Synthesis completed: {generatedFiles.Count} files generated.";
            }

            return result;
        }

        /// <summary>
        /// Parse une erreur de type OrphanSystem pour extraire Module, SystemName, InterfaceName.
        /// Exemple d'entrée : "[Engine] Système 'PhysicsSystem' sans abstraction d'interface 'IPhysicsSystem'."
        /// </summary>
        private (string module, string system, string iface) ParseSystemIssue(string issue)
        {
            try
            {
                var parts = issue.Split(']');
                var module = parts[0].Trim('[', ' ');

                var systemStart = issue.IndexOf("'") + 1;
                var systemEnd = issue.IndexOf("'", systemStart);
                var system = issue.Substring(systemStart, systemEnd - systemStart);

                var ifaceStart = issue.LastIndexOf("'") + 1;
                var ifaceEnd = issue.LastIndexOf("'", ifaceStart);
                // Si LastIndexOf retourne la même position, on prend le dernier guillemet avant le point
                if (ifaceEnd <= ifaceStart)
                {
                    ifaceEnd = issue.LastIndexOf("'");
                    ifaceStart = issue.Substring(0, ifaceEnd).LastIndexOf("'") + 1;
                }
                var iface = issue.Substring(ifaceStart, ifaceEnd - ifaceStart);

                return (module, system, iface);
            }
            catch
            {
                return (string.Empty, string.Empty, string.Empty);
            }
        }

        /// <summary>
        /// Parse une erreur de type UnimplementedInterface pour extraire Module, InterfaceName, ClassName.
        /// Exemple d'entrée : "[Game] Interface 'IRenderer' sans implémentation 'Renderer' détectée."
        /// </summary>
        private (string module, string iface, string className) ParseInterfaceIssue(string issue)
        {
            try
            {
                var parts = issue.Split(']');
                var module = parts[0].Trim('[', ' ');

                var ifaceStart = issue.IndexOf("'") + 1;
                var ifaceEnd = issue.IndexOf("'", ifaceStart);
                var iface = issue.Substring(ifaceStart, ifaceEnd - ifaceStart);

                var classStart = issue.LastIndexOf("'") + 1;
                var classEnd = issue.LastIndexOf("'", classStart);
                if (classEnd <= classStart)
                {
                    classEnd = issue.LastIndexOf("'");
                    classStart = issue.Substring(0, classEnd).LastIndexOf("'") + 1;
                }
                var className = issue.Substring(classStart, classEnd - classStart);

                return (module, iface, className);
            }
            catch
            {
                return (string.Empty, string.Empty, string.Empty);
            }
        }
    }
}
