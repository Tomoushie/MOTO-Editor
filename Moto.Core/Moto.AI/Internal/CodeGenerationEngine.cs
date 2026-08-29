// Moto.Core/AI/Internal/CodeGenerationEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// Moteur de génération interne de MOTO AI.
    /// Génère des modules, interfaces, composants, systèmes et fichiers d'architecture.
    /// </summary>
    public class CodeGenerationEngine
    {
        /// <summary>
        /// Génère un module ECS complet :
        /// - interface
        /// - composant
        /// - système
        /// </summary>
        public List<AiFileChange> GenerateModule(ProjectMap map, string moduleName)
        {
            moduleName = NormalizeName(moduleName);

            var rootNamespace = InferRootNamespace(map);
            var moduleNamespace = $"{rootNamespace}.Modules.{moduleName}";
            var folder = $"Modules/{moduleName}";

            var changes = new List<AiFileChange>();

            changes.Add(new AiFileChange
            {
                Path = $"{folder}/I{moduleName}.cs",
                Reason = $"Interface du module {moduleName}.",
                ChangeType = FileChangeType.Create,
                Content = GenerateInterface(moduleNamespace, moduleName)
            });

            changes.Add(new AiFileChange
            {
                Path = $"{folder}/{moduleName}Component.cs",
                Reason = $"Composant de données du module {moduleName}.",
                ChangeType = FileChangeType.Create,
                Content = GenerateComponent(moduleNamespace, moduleName)
            });

            changes.Add(new AiFileChange
            {
                Path = $"{folder}/{moduleName}System.cs",
                Reason = $"Système logique du module {moduleName}.",
                ChangeType = FileChangeType.Create,
                Content = GenerateSystem(moduleNamespace, moduleName)
            });

            return changes;
        }

        /// <summary>
        /// Génère une interface simple.
        /// </summary>
        public AiFileChange GenerateInterfaceFile(string ns, string interfaceName, string folder = "Generated")
        {
            var cleanName = interfaceName.StartsWith("I") ? interfaceName : "I" + interfaceName;

            return new AiFileChange
            {
                Path = $"{folder}/{cleanName}.cs",
                Reason = $"Interface générée : {cleanName}.",
                ChangeType = FileChangeType.Create,
                Content = GenerateInterface(ns, cleanName.Substring(1))
            };
        }

        /// <summary>
        /// Génère une classe simple.
        /// </summary>
        public AiFileChange GenerateClassFile(string ns, string className, string folder = "Generated")
        {
            return new AiFileChange
            {
                Path = $"{folder}/{className}.cs",
                Reason = $"Classe générée : {className}.",
                ChangeType = FileChangeType.Create,
                Content = GenerateClass(ns, className)
            };
        }

        /// <summary>
        /// Déduit le namespace racine du projet.
        /// </summary>
        public string InferRootNamespace(ProjectMap map)
        {
            if (map.Namespaces.Count == 0)
            {
                return "Moto.Project";
            }

            var first = map.Namespaces.First();
            var parts = first.Split('.');

            if (parts.Length >= 2)
            {
                return $"{parts[0]}.{parts[1]}";
            }

            return first;
        }

        private string GenerateInterface(string ns, string moduleName)
        {
            var interfaceName = moduleName.StartsWith("I") ? moduleName : "I" + moduleName;

            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Interface du module {moduleName}.");
            sb.AppendLine("    /// Générée par MOTO AI.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public interface {interfaceName}");
            sb.AppendLine("    {");
            sb.AppendLine("        void Initialize();");
            sb.AppendLine("        void Update(float deltaTime);");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateComponent(string ns, string moduleName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Composant de données du module {moduleName}.");
            sb.AppendLine("    /// Généré par MOTO AI.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public class {moduleName}Component");
            sb.AppendLine("    {");
            sb.AppendLine("        public bool IsEnabled { get; set; } = true;");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateSystem(string ns, string moduleName)
        {
            var interfaceName = moduleName.StartsWith("I") ? moduleName : "I" + moduleName;

            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Système logique du module {moduleName}.");
            sb.AppendLine("    /// Généré par MOTO AI.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public class {moduleName}System : {interfaceName}");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly {moduleName}Component _component;");
            sb.AppendLine();
            sb.AppendLine($"        public {moduleName}System({moduleName}Component component)");
            sb.AppendLine("        {");
            sb.AppendLine("            _component = component ?? throw new ArgumentNullException(nameof(component));");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void Initialize()");
            sb.AppendLine("        {");
            sb.AppendLine("            // TODO: initialisation du système.");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void Update(float deltaTime)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!_component.IsEnabled) return;");
            sb.AppendLine("            // TODO: logique de mise à jour.");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateClass(string ns, string className)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Classe générée par MOTO AI.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public class {className}");
            sb.AppendLine("    {");
            sb.AppendLine("        // TODO: compléter cette classe selon l'intention du développeur.");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "NewModule";
            }

            var sb = new StringBuilder();

            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
            }

            if (sb.Length == 0)
            {
                return "NewModule";
            }

            var result = sb.ToString();

            if (!char.IsLetter(result[0]))
            {
                result = "M" + result;
            }

            return char.ToUpperInvariant(result[0]) + result.Substring(1);
        }
    }
}
