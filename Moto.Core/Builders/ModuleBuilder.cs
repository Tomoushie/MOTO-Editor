// Moto.Editor/AI/Builders/ModuleBuilder.cs
using System;
using System.Text;
using System.Threading.Tasks;

namespace Moto.Editor.AI.Builders
{
    /// <summary>
    /// Génère un module ECS complet depuis une phrase.
    ///
    /// Exemple :
    /// "Ajoute un système de santé" →
    ///   Health/IHealth.cs
    ///   Health/HealthComponent.cs
    ///   Health/HealthSystem.cs
    ///   + intégration dans le pipeline existant
    /// </summary>
    public class ModuleBuilder
    {
        private readonly NaturalLanguageParser _parser;

        public ModuleBuilder(NaturalLanguageParser parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        /// <summary>
        /// Génère un module ECS depuis une phrase.
        /// </summary>
        public async Task<BuilderResult> BuildAsync(string description)
        {
            var result = new BuilderResult();

            try
            {
                var module = await _parser.ParseModuleAsync(description);

                if (string.IsNullOrWhiteSpace(module.Name))
                {
                    result.Success = false;
                    result.Summary = "Impossible de déterminer le nom du module.";
                    return result;
                }

                GenerateInterface(module, result);
                GenerateComponent(module, result);
                GenerateSystem(module, result);
                GenerateIntegration(module, result);

                result.Success = true;
                result.Summary = $"Module '{module.Name}' généré : {result.Files.Count} fichiers.";
                result.Explanation =
                    $"J'ai créé le module '{module.Name}' avec son interface, son composant de données " +
                    "et son système logique. L'intégration dans le pipeline est proposée.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Summary = "Échec de la génération du module.";
                result.Explanation = $"Erreur : {ex.Message}";
            }

            return result;
        }

        private void GenerateInterface(ModuleDescriptor module, BuilderResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine($"namespace Snake2000.Engine.Modules.{module.Name}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// {module.Description}");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public interface I{module.Name}");
            sb.AppendLine("    {");

            if (module.SystemMethods.Count == 0)
            {
                sb.AppendLine("        void Initialize();");
                sb.AppendLine("        void Update(float deltaTime);");
            }
            else
            {
                foreach (var method in module.SystemMethods)
                {
                    sb.AppendLine($"        void {method}();");
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            result.Files.Add(new GeneratedFile
            {
                RelativePath = $"Modules/{module.Name}/I{module.Name}.cs",
                Content = sb.ToString(),
                Reason = $"Interface du module {module.Name}"
            });
        }

        private void GenerateComponent(ModuleDescriptor module, BuilderResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine($"namespace Snake2000.Engine.Modules.{module.Name}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Données du module {module.Name}.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public class {module.Name}Component");
            sb.AppendLine("    {");

            if (module.ComponentProperties.Count == 0)
            {
                sb.AppendLine("        public bool IsEnabled { get; set; } = true;");
            }
            else
            {
                foreach (var prop in module.ComponentProperties)
                {
                    sb.AppendLine($"        public {prop} {{ get; set; }}");
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            result.Files.Add(new GeneratedFile
            {
                RelativePath = $"Modules/{module.Name}/{module.Name}Component.cs",
                Content = sb.ToString(),
                Reason = $"Composant de données du module {module.Name}"
            });
        }

        private void GenerateSystem(ModuleDescriptor module, BuilderResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine($"namespace Snake2000.Engine.Modules.{module.Name}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Logique du module {module.Name}.");
            sb.AppendLine($"    /// {module.Description}");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public class {module.Name}System : I{module.Name}");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly {module.Name}Component _component;");
            sb.AppendLine();
            sb.AppendLine($"        public {module.Name}System({module.Name}Component component)");
            sb.AppendLine("        {");
            sb.AppendLine("            _component = component ?? throw new ArgumentNullException(nameof(component));");
            sb.AppendLine("        }");

            if (module.SystemMethods.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("        public void Initialize()");
                sb.AppendLine("        {");
                sb.AppendLine("            // TODO: initialisation");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine("        public void Update(float deltaTime)");
                sb.AppendLine("        {");
                sb.AppendLine("            if (!_component.IsEnabled) return;");
                sb.AppendLine("            // TODO: logique de mise à jour");
                sb.AppendLine("        }");
            }
            else
            {
                foreach (var method in module.SystemMethods)
                {
                    sb.AppendLine();
                    sb.AppendLine($"        public void {method}()");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            // TODO: implémenter {method}");
                    sb.AppendLine("        }");
                }
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            result.Files.Add(new GeneratedFile
            {
                RelativePath = $"Modules/{module.Name}/{module.Name}System.cs",
                Content = sb.ToString(),
                Reason = $"Système logique du module {module.Name}"
            });
        }

        private void GenerateIntegration(ModuleDescriptor module, BuilderResult result)
        {
            // Intégration dans le pipeline XENO-SSS∞
            result.Integrations.Add(new IntegrationAction
            {
                TargetFile = "XenoPipeline.cs",
                Action = "Ajouter l'initialisation du système",
                CodeSnippet = $"var {module.Name.ToLower()}System = new {module.Name}System(new {module.Name}Component());",
                Reason = $"Connecter {module.Name}System au pipeline"
            });

            // Dépendances
            foreach (var dep in module.Dependencies)
            {
                result.Integrations.Add(new IntegrationAction
                {
                    TargetFile = $"Modules/{module.Name}/{module.Name}System.cs",
                    Action = $"Ajouter la dépendance vers {dep}",
                    CodeSnippet = $"// Dépendance : {dep}",
                    Reason = $"Le module {module.Name} dépend de {dep}"
                });
            }
        }
    }
}
