// Moto.Editor/AI/Builders/BlueprintGenerator.cs
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Moto.Editor.AI.Builders
{
    /// <summary>
    /// Génère un projet complet depuis une description en langage naturel.
    ///
    /// Flux :
    /// 1. Ollama analyse la description et produit un BlueprintDescriptor.
    /// 2. Le générateur crée la structure ECS complète.
    /// 3. XENO-SSS∞ valide et connecte le tout.
    ///
    /// Exemple :
    /// "Je veux un jeu de plateforme" →
    ///   Player/, Enemy/, Physics/, Rendering/, Input/, GameLoop/
    ///   Chaque module avec Interface + Component + System.
    /// </summary>
    public class BlueprintGenerator
    {
        private readonly NaturalLanguageParser _parser;
        private readonly IBuilderOllamaClient _ollama;

        public BlueprintGenerator(NaturalLanguageParser parser, IBuilderOllamaClient ollama)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _ollama = ollama ?? throw new ArgumentNullException(nameof(ollama));
        }

        /// <summary>
        /// Génère un projet complet depuis une phrase.
        /// </summary>
        public async Task<BuilderResult> GenerateAsync(string description, string projectName)
        {
            var result = new BuilderResult();

            try
            {
                // Étape 1 : comprendre la demande.
                var blueprint = await _parser.ParseBlueprintAsync(description);
                blueprint.ProjectName = projectName;

                // Étape 2 : générer la structure de base.
                GenerateProjectStructure(blueprint, result);

                // Étape 3 : générer chaque module.
                foreach (var module in blueprint.Modules)
                {
                    GenerateModuleFiles(module, result);
                }

                // Étape 4 : générer chaque comportement.
                foreach (var behavior in blueprint.Behaviors)
                {
                    GenerateBehaviorFiles(behavior, result);
                }

                // Étape 5 : générer le point d'entrée.
                GenerateEntryPoint(blueprint, result);

                result.Success = true;
                result.Summary = $"Projet '{blueprint.ProjectName}' généré : {result.Files.Count} fichiers.";
                result.Explanation =
                    $"J'ai créé un projet de type '{blueprint.ProjectType}' avec " +
                    $"{blueprint.Modules.Count} module(s) et {blueprint.Behaviors.Count} comportement(s). " +
                    "Chaque module suit le pattern Interface + Component + System.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Summary = "Échec de la génération du projet.";
                result.Explanation = $"Erreur : {ex.Message}";
            }

            return result;
        }

        private void GenerateProjectStructure(BlueprintDescriptor blueprint, BuilderResult result)
        {
            // README.md
            result.Files.Add(new GeneratedFile
            {
                RelativePath = "README.md",
                Content = $"# {blueprint.ProjectName}\n\n{blueprint.Description}\n\n" +
                          "Projet généré automatiquement par MOTO Editor.\n\n" +
                          "## Structure\n\n" +
                          "Chaque module suit le pattern ECS :\n" +
                          "- `I{Name}.cs` : interface / contrat\n" +
                          "- `{Name}Component.cs` : données\n" +
                          "- `{Name}System.cs` : logique\n",
                Reason = "Documentation du projet"
            });

            // .gitignore
            result.Files.Add(new GeneratedFile
            {
                RelativePath = ".gitignore",
                Content = "bin/\nobj/\n.vs/\n*.user\n",
                Reason = "Configuration Git"
            });
        }

        private void GenerateModuleFiles(ModuleDescriptor module, BuilderResult result)
        {
            var folder = module.Name;

            // Interface
            var interfaceContent = new StringBuilder();
            interfaceContent.AppendLine("using System;");
            interfaceContent.AppendLine();
            interfaceContent.AppendLine($"namespace {module.Name}");
            interfaceContent.AppendLine("{");
            interfaceContent.AppendLine($"    /// <summary>");
            interfaceContent.AppendLine($"    /// {module.Description}");
            interfaceContent.AppendLine($"    /// </summary>");
            interfaceContent.AppendLine($"    public interface I{module.Name}");
            interfaceContent.AppendLine("    {");

            foreach (var method in module.SystemMethods)
            {
                interfaceContent.AppendLine($"        void {method}();");
            }

            interfaceContent.AppendLine("    }");
            interfaceContent.AppendLine("}");

            result.Files.Add(new GeneratedFile
            {
                RelativePath = $"{folder}/I{module.Name}.cs",
                Content = interfaceContent.ToString(),
                Reason = $"Interface du module {module.Name}"
            });

            // Component
            var componentContent = new StringBuilder();
            componentContent.AppendLine("using System;");
            componentContent.AppendLine();
            componentContent.AppendLine($"namespace {module.Name}");
            componentContent.AppendLine("{");
            componentContent.AppendLine($"    /// <summary>");
            componentContent.AppendLine($"    /// Données du module {module.Name}.");
            componentContent.AppendLine($"    /// </summary>");
            componentContent.AppendLine($"    public class {module.Name}Component");
            componentContent.AppendLine("    {");

            foreach (var prop in module.ComponentProperties)
            {
                componentContent.AppendLine($"        public {prop} {{ get; set; }}");
            }

            componentContent.AppendLine("    }");
            componentContent.AppendLine("}");

            result.Files.Add(new GeneratedFile
            {
                RelativePath = $"{folder}/{module.Name}Component.cs",
                Content = componentContent.ToString(),
                Reason = $"Composant de données du module {module.Name}"
            });

            // System
            var systemContent = new StringBuilder();
            systemContent.AppendLine("using System;");
            systemContent.AppendLine();
            systemContent.AppendLine($"namespace {module.Name}");
            systemContent.AppendLine("{");
            systemContent.AppendLine($"    /// <summary>");
            systemContent.AppendLine($"    /// Logique du module {module.Name}.");
            systemContent.AppendLine($"    /// {module.Description}");
            systemContent.AppendLine($"    /// </summary>");
            systemContent.AppendLine($"    public class {module.Name}System : I{module.Name}");
            systemContent.AppendLine("    {");
            systemContent.AppendLine($"        private readonly {module.Name}Component _component;");
            systemContent.AppendLine();
            systemContent.AppendLine($"        public {module.Name}System({module.Name}Component component)");
            systemContent.AppendLine("        {");
            systemContent.AppendLine("            _component = component ?? throw new ArgumentNullException(nameof(component));");
            systemContent.AppendLine("        }");

            foreach (var method in module.SystemMethods)
            {
                systemContent.AppendLine();
                systemContent.AppendLine($"        public void {method}()");
                systemContent.AppendLine("        {");
                systemContent.AppendLine($"            // TODO: implémenter {method}");
                systemContent.AppendLine("        }");
            }

            systemContent.AppendLine("    }");
            systemContent.AppendLine("}");

            result.Files.Add(new GeneratedFile
            {
                RelativePath = $"{folder}/{module.Name}System.cs",
                Content = systemContent.ToString(),
                Reason = $"Système logique du module {module.Name}"
            });
        }

        private void GenerateBehaviorFiles(BehaviorDescriptor behavior, BuilderResult result)
        {
            var folder = $"{behavior.Subject}{behavior.Action}";

            // Component
            var componentContent = new StringBuilder();
            componentContent.AppendLine("using System;");
            componentContent.AppendLine();
            componentContent.AppendLine($"namespace Behaviors.{behavior.Subject}");
            componentContent.AppendLine("{");
            componentContent.AppendLine($"    /// <summary>");
            componentContent.AppendLine($"    /// Données pour le comportement : {behavior.Subject} {behavior.Action} {behavior.Target}.");
            componentContent.AppendLine($"    /// </summary>");
            componentContent.AppendLine($"    public class {behavior.Subject}{behavior.Action}Component");
            componentContent.AppendLine("    {");
            componentContent.AppendLine("        public float Speed { get; set; } = 1.0f;");
            componentContent.AppendLine("        public bool IsActive { get; set; } = true;");
            componentContent.AppendLine("    }");
            componentContent.AppendLine("}");

            result.Files.Add(new GeneratedFile
            {
                RelativePath = $"Behaviors/{folder}/{behavior.Subject}{behavior.Action}Component.cs",
                Content = componentContent.ToString(),
                Reason = $"Composant du comportement {behavior.Subject} {behavior.Action}"
            });

            // System
            var systemContent = new StringBuilder();
            systemContent.AppendLine("using System;");
            systemContent.AppendLine();
            componentContent.AppendLine($"namespace Behaviors.{behavior.Subject}");
            systemContent.AppendLine($"namespace Behaviors.{behavior.Subject}");
            systemContent.AppendLine("{");
            systemContent.AppendLine($"    /// <summary>");
            systemContent.AppendLine($"    /// Comportement : {behavior.Subject} {behavior.Action} {behavior.Target}.");
            systemContent.AppendLine($"    /// </summary>");
            systemContent.AppendLine($"    public class {behavior.Subject}{behavior.Action}System");
            systemContent.AppendLine("    {");
            systemContent.AppendLine($"        private readonly {behavior.Subject}{behavior.Action}Component _component;");
            systemContent.AppendLine();
            systemContent.AppendLine($"        public {behavior.Subject}{behavior.Action}System({behavior.Subject}{behavior.Action}Component component)");
            systemContent.AppendLine("        {");
            systemContent.AppendLine("            _component = component ?? throw new ArgumentNullException(nameof(component));");
            systemContent.AppendLine("        }");
            systemContent.AppendLine();
            systemContent.AppendLine("        public void Update(float deltaTime)");
            systemContent.AppendLine("        {");
            systemContent.AppendLine("            if (!_component.IsActive) return;");
            systemContent.AppendLine($"            // TODO: implémenter {behavior.Action} vers {behavior.Target}");
            systemContent.AppendLine("        }");
            systemContent.AppendLine("    }");
            systemContent.AppendLine("}");

            result.Files.Add(new GeneratedFile
            {
                RelativePath = $"Behaviors/{folder}/{behavior.Subject}{behavior.Action}System.cs",
                Content = systemContent.ToString(),
                Reason = $"Système du comportement {behavior.Subject} {behavior.Action}"
            });
        }

        private void GenerateEntryPoint(BlueprintDescriptor blueprint, BuilderResult result)
        {
            var content = new StringBuilder();
            content.AppendLine("using System;");
            content.AppendLine();
            content.AppendLine($"namespace {blueprint.ProjectName}");
            content.AppendLine("{");
            content.AppendLine("    /// <summary>");
            content.AppendLine($"    /// Point d'entrée de {blueprint.ProjectName}.");
            content.AppendLine("    /// Généré automatiquement par MOTO Editor.");
            content.AppendLine("    /// </summary>");
            content.AppendLine("    public class Program");
            content.AppendLine("    {");
            content.AppendLine("        public static void Main(string[] args)");
            content.AppendLine("        {");
            content.AppendLine($"            Console.WriteLine(\"{blueprint.ProjectName} démarré.\");");
            content.AppendLine();
            content.AppendLine("            // TODO: initialiser les modules ici.");

            foreach (var module in blueprint.Modules)
            {
                content.AppendLine($"            // var {module.Name.ToLower()}System = new {module.Name}System(new {module.Name}Component());");
            }

            content.AppendLine();
            content.AppendLine("            Console.WriteLine(\"Appuyez sur Entrée pour quitter.\");");
            content.AppendLine("            Console.ReadLine();");
            content.AppendLine("        }");
            content.AppendLine("    }");
            content.AppendLine("}");

            result.Files.Add(new GeneratedFile
            {
                RelativePath = "Program.cs",
                Content = content.ToString(),
                Reason = "Point d'entrée du projet"
            });
        }
    }
}
