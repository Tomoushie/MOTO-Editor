// Moto.Core/AI/Internal/CodeFixEngine.cs
using System.Collections.Generic;
using System.Linq;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// Moteur de correction automatique interne.
    /// Propose des fichiers manquants et des corrections structurelles.
    /// </summary>
    public class CodeFixEngine
    {
        private readonly CodeGenerationEngine _generator;

        public CodeFixEngine(CodeGenerationEngine generator)
        {
            _generator = generator;
        }

        /// <summary>
        /// Analyse la carte du projet et propose des corrections.
        /// </summary>
        public List<AiFileChange> Fix(ProjectMap map)
        {
            var changes = new List<AiFileChange>();

            var rootNamespace = _generator.InferRootNamespace(map);

            foreach (var issue in map.Issues)
            {
                if (issue.Kind == IssueKind.MissingImplementation)
                {
                    var className = issue.SymbolName.StartsWith("I")
                        ? issue.SymbolName.Substring(1)
                        : issue.SymbolName + "Implementation";

                    var ns = string.IsNullOrWhiteSpace(issue.Namespace)
                        ? $"{rootNamespace}.Generated"
                        : issue.Namespace;

                    changes.Add(_generator.GenerateClassFile(ns, className, "Generated"));
                }

                if (issue.Kind == IssueKind.MissingInterfaceForSystem)
                {
                    var interfaceName = "I" + issue.SymbolName;

                    var ns = string.IsNullOrWhiteSpace(issue.Namespace)
                        ? $"{rootNamespace}.Modules"
                        : issue.Namespace;

                    changes.Add(_generator.GenerateInterfaceFile(ns, interfaceName, "Generated"));
                }
            }

            return changes
                .GroupBy(c => c.Path)
                .Select(g => g.First())
                .ToList();
        }
    }
}
