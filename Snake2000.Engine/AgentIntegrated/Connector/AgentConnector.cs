using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Snake2000.Engine.AgentIntegrated.Core;

namespace Snake2000.Engine.AgentIntegrated.Connector
{
    /// <summary>
    /// AgentConnector : analyse les fichiers générés et produit un plan d'intégration (IntegrationPlan)
    /// pour les relier au projet existant (DI, Pipeline, Usings) sans modifier directement les fichiers sur le disque.
    /// </summary>
    public class AgentConnector
    {
        // Regex compilées pour extraire les métadonnées des fichiers générés de manière performante
        private static readonly Regex NamespaceRegex = new Regex(@"\bnamespace\s+([\w\.]+)", RegexOptions.Compiled);
        private static readonly Regex ClassImplRegex = new Regex(@"\bclass\s+(\w+)\s*:\s*(\w+)", RegexOptions.Compiled);
        private static readonly Regex SystemRegex = new Regex(@"\bclass\s+(\w+System)", RegexOptions.Compiled);

        public AgentResult Connect(AgentContext context, AgentResult synthResult, AgentResult scanResult = null)
        {
            var result = new AgentResult
            {
                ModuleName = "Connector",
                Status = "success",
                Summary = "Integration plan generated."
            };

            // 1. Extraction sécurisée des payloads précédents
            var generatedFiles = synthResult.Payload["GeneratedFiles"] as List<(string path, string content)> ?? new();
            var allExistingFiles = scanResult?.Payload["Files"] as List<string> ?? new();
            var rootPath = context.RootPath ?? Directory.GetCurrentDirectory();

            var plan = new IntegrationPlan();

            if (!generatedFiles.Any())
            {
                result.Summary = "No generated files to connect.";
                result.Payload["IntegrationPlan"] = plan;
                return result;
            }

            // 2. Analyse des artefacts générés pour extraire les contrats d'intégration
            var generatedNamespaces = new HashSet<string>();
            var generatedInterfaces = new List<(string Interface, string Implementation)>();
            var generatedSystems = new List<string>();

            foreach (var file in generatedFiles)
            {
                var nsMatch = NamespaceRegex.Match(file.content);
                if (nsMatch.Success) generatedNamespaces.Add(nsMatch.Groups[1].Value);

                var implMatch = ClassImplRegex.Match(file.content);
                if (implMatch.Success)
                {
                    generatedInterfaces.Add((implMatch.Groups[2].Value, implMatch.Groups[1].Value));
                }

                var sysMatch = SystemRegex.Match(file.content);
                if (sysMatch.Success)
                {
                    generatedSystems.Add(sysMatch.Groups[1].Value);
                }
            }

            // 3. Identification des points d'ancrage dans l'architecture (Engine/Game/App)
            // Priorité aux fichiers standards de configuration et de boucle de jeu
            var diTarget = FindTargetFile(allExistingFiles, new[] { "Program.cs", "Startup.cs", "ServiceCollectionExtensions.cs", "App.cs" });
            var engineTarget = FindTargetFile(allExistingFiles, new[] { "GameEngine.cs", "Engine.cs", "GameLoop.cs", "SystemManager.cs" });

            // 4. Formulation du plan d'intégration (Instructions de patch)

            // A. Ajout des directives 'using' dans les points d'ancrage
            foreach (var ns in generatedNamespaces)
            {
                if (diTarget != null)
                {
                    plan.Steps.Add(new IntegrationStep
                    {
                        TargetFile = diTarget,
                        ActionType = "AddUsing",
                        CodeSnippet = $"using {ns};",
                        Description = $"Ajout du namespace {ns} pour la configuration DI."
                    });
                }

                if (engineTarget != null)
                {
                    plan.Steps.Add(new IntegrationStep
                    {
                        TargetFile = engineTarget,
                        ActionType = "AddUsing",
                        CodeSnippet = $"using {ns};",
                        Description = $"Ajout du namespace {ns} pour le pipeline d'exécution."
                    });
                }
            }

            // B. Enregistrement dans le conteneur d'Injection de Dépendances (DI)
            if (diTarget != null)
            {
                foreach (var (iface, impl) in generatedInterfaces)
                {
                    plan.Steps.Add(new IntegrationStep
                    {
                        TargetFile = diTarget,
                        ActionType = "RegisterDI",
                        // Format standard pour Microsoft.Extensions.DependencyInjection
                        CodeSnippet = $"services.AddTransient<{iface}, {impl}>();",
                        Description = $"Enregistrement de {impl} comme implémentation de {iface}."
                    });
                }
            }

            // C. Hook des systèmes dans le pipeline du moteur
            if (engineTarget != null)
            {
                foreach (var system in generatedSystems)
                {
                    plan.Steps.Add(new IntegrationStep
                    {
                        TargetFile = engineTarget,
                        ActionType = "HookInitialization",
                        // Convention standard d'ajout à une collection de systèmes dans un moteur ECS/Modulaire
                        CodeSnippet = $"_systems.Add(new {system}()); // Connecté par AgentConnector",
                        Description = $"Ajout de {system} dans la liste des systèmes actifs du moteur."
                    });
                }
            }

            // 5. Finalisation du rapport
            result.Details.Add($"Generated integration plan with {plan.Steps.Count} atomic steps.");
            result.Details.Add($"Target DI file: {diTarget ?? "Not found"}");
            result.Details.Add($"Target Engine file: {engineTarget ?? "Not found"}");

            result.Payload["IntegrationPlan"] = plan;
            return result;
        }

        /// <summary>
        /// Recherche le premier fichier correspondant aux noms cibles dans la liste des fichiers existants.
        /// Utilisé pour localiser les points d'entrée de l'application et du moteur.
        /// </summary>
        private string FindTargetFile(List<string> files, string[] targetNames)
        {
            foreach (var name in targetNames)
            {
                var match = files.FirstOrDefault(f => Path.GetFileName(f).Equals(name, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }
            return null;
        }
    }

    /// <summary>
    /// Représente le plan complet d'intégration des nouveaux artefacts dans le projet.
    /// Ce plan est conçu pour être sérialisé ou consommé par un agent de type "FilePatchAgent".
    /// </summary>
    public class IntegrationPlan
    {
        public List<IntegrationStep> Steps { get; set; } = new();
    }

    /// <summary>
    /// Représente une action atomique à effectuer sur un fichier cible.
    /// </summary>
    public class IntegrationStep
    {
        public string TargetFile { get; set; }
        public string ActionType { get; set; } // AddUsing, RegisterDI, HookInitialization
        public string CodeSnippet { get; set; }
        public string Description { get; set; }
    }
}
