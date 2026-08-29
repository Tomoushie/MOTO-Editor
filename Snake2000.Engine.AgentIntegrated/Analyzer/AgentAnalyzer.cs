using System.Collections.Generic;
using System.Linq;
using Snake2000.Engine.AgentIntegrated.Core;

namespace Snake2000.Engine.AgentIntegrated.Analyzer
{
    /// <summary>
    /// Analyzer : analyse la ProjectMap, détecte les incohérences architecturales
    /// et les dépendances cassées entre les modules Engine, Game et App.
    /// </summary>
    public class AgentAnalyzer
    {
        public AgentResult Analyze(AgentContext context, AgentResult scanResult)
        {
            var result = new AgentResult
            {
                ModuleName = "Analyzer",
                Status = "success",
                Summary = "Analysis completed."
            };

            var report = new AnalysisReport();

            // Extraction sécurisée de la ProjectMap depuis le payload du Scanner
            var projectMap = scanResult.Payload["ProjectMap"] as Dictionary<string, object>;

            if (projectMap == null || projectMap.Count == 0)
            {
                result.Status = "warning";
                result.Summary = "ProjectMap is empty. Scanner might have failed or found no files.";
                result.Payload["Report"] = report;
                return result;
            }

            // Cache pour les analyses croisées entre modules
            var moduleDataCache = new Dictionary<string, (HashSet<string> Namespaces, HashSet<string> Classes, HashSet<string> Interfaces, HashSet<string> Systems)>();
            var allClasses = new HashSet<string>();

            // 1. Analyse intra-module
            foreach (var module in projectMap)
            {
                var moduleName = module.Key; // "Engine", "Game", "App"
                var data = module.Value as Dictionary<string, object>;
                if (data == null) continue;

                var namespaces = data["Namespaces"] as HashSet<string> ?? new HashSet<string>();
                var classes = data["Classes"] as HashSet<string> ?? new HashSet<string>();
                var interfaces = data["Interfaces"] as HashSet<string> ?? new HashSet<string>();
                var systems = data["Systems"] as HashSet<string> ?? new HashSet<string>();

                moduleDataCache[moduleName] = (namespaces, classes, interfaces, systems);
                allClasses.UnionWith(classes);

                // A. Cohérence des Namespaces (Règle : doit contenir le nom du module)
                foreach (var ns in namespaces)
                {
                    if (!ns.Contains(moduleName) && !ns.StartsWith($"Snake2000.{moduleName}"))
                    {
                        report.InconsistentNamespaces.Add($"[{moduleName}] Le namespace '{ns}' viole la convention architecturale.");
                    }
                }

                // B. Interfaces non implémentées (Heuristique C# standard : IName -> Name)
                foreach (var iface in interfaces)
                {
                    if (iface.StartsWith("I") && iface.Length > 1 && char.IsUpper(iface[1]))
                    {
                        var expectedImpl = iface.Substring(1);
                        // Vérifie dans le module courant et globalement
                        if (!classes.Contains(expectedImpl) && !allClasses.Contains(expectedImpl))
                        {
                            report.UnimplementedInterfaces.Add($"[{moduleName}] Interface '{iface}' sans implémentation '{expectedImpl}' détectée.");
                        }
                    }
                }

                // C. Systèmes non connectés (Un System doit avoir une interface pour l'Injection de Dépendances)
                foreach (var sys in systems)
                {
                    var expectedInterface = "I" + sys;
                    if (!interfaces.Contains(expectedInterface))
                    {
                        report.OrphanSystems.Add($"[{moduleName}] Système '{sys}' sans abstraction d'interface '{expectedInterface}'.");
                    }
                }
            }

            // 2. Analyse inter-modules (Dépendances et Conflits)
            if (moduleDataCache.ContainsKey("Engine") && moduleDataCache.ContainsKey("Game"))
            {
                var engineClasses = moduleDataCache["Engine"].Classes;
                var gameClasses = moduleDataCache["Game"].Classes;

                // Détection de Shadowing (même nom de classe dans deux couches différentes)
                var conflicts = engineClasses.Intersect(gameClasses);
                foreach (var conflict in conflicts)
                {
                    report.BrokenDependencies.Add($"Conflit d'architecture (Shadowing) : La classe '{conflict}' est définie à la fois dans Engine et Game.");
                }
            }

            // 3. Finalisation du rapport
            result.Details.Add($"Analyzed {allClasses.Count} classes across {projectMap.Count} modules.");
            result.Details.Add($"Issues found: {report.InconsistentNamespaces.Count} namespaces, {report.UnimplementedInterfaces.Count} interfaces, {report.OrphanSystems.Count} systems.");

            if (report.HasCriticalIssues())
            {
                result.Status = "warning";
                result.Summary = "Analysis completed with architectural warnings.";
            }

            result.Payload["Report"] = report;
            return result;
        }
    }

    /// <summary>
    /// Structure de données pour le rapport d'analyse architecturale.
    /// </summary>
    public class AnalysisReport
    {
        public List<string> InconsistentNamespaces { get; set; } = new();
        public List<string> UnimplementedInterfaces { get; set; } = new();
        public List<string> OrphanSystems { get; set; } = new();
        public List<string> BrokenDependencies { get; set; } = new();

        /// <summary>
        /// Détermine si le projet contient des erreurs bloquantes pour la compilation ou l'exécution.
        /// </summary>
        public bool HasCriticalIssues() =>
            BrokenDependencies.Count > 0 || InconsistentNamespaces.Count > 0;
    }
}
