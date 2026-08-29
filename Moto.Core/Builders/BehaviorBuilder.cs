// Moto.Editor/AI/Builders/BehaviorBuilder.cs
using System;
using System.Text;
using System.Threading.Tasks;

namespace Moto.Editor.AI.Builders
{
    /// <summary>
    /// Génère un comportement ECS depuis une phrase.
    ///
    /// Exemple :
    /// "Fais que l'ennemi me suive" →
    ///   Behaviors/EnemyFollow/EnemyFollowComponent.cs
    ///   Behaviors/EnemyFollow/EnemyFollowSystem.cs
    ///   + intégration automatique
    /// </summary>
    public class BehaviorBuilder
    {
        private readonly NaturalLanguageParser _parser;

        public BehaviorBuilder(NaturalLanguageParser parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        /// <summary>
        /// Génère un comportement depuis une phrase.
        /// </summary>
        public async Task<BuilderResult> BuildAsync(string description)
        {
            var result = new BuilderResult();

            try
            {
                var behavior = await _parser.ParseBehaviorAsync(description);

                if (string.IsNullOrWhiteSpace(behavior.Subject) ||
                    string.IsNullOrWhiteSpace(behavior.Action))
                {
                    result.Success = false;
                    result.Summary = "Impossible de comprendre le comportement demandé.";
                    return result;
                }

                var behaviorName = $"{behavior.Subject}{behavior.Action}";

                GenerateComponent(behavior, behaviorName, result);
                GenerateSystem(behavior, behaviorName, result);
                GenerateIntegration(behavior, behaviorName, result);

                result.Success = true;
                result.Summary = $"Comportement '{behaviorName}' généré : {result.Files.Count} fichiers.";
                result.Explanation =
                    $"J'ai créé le comportement '{behavior.Subject} {behavior.Action} {behavior.Target}' " +
                    "avec son composant de données et son système logique.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Summary = "Échec de la génération du comportement.";
                result.Explanation = $"Erreur : {ex.Message}";
            }

            return result;
        }

        private void GenerateComponent(BehaviorDescriptor behavior, string behaviorName, BuilderResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine($"namespace Snake2000.Engine.Behaviors.{behavior.Subject}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Données pour le comportement : {behavior.Subject} {behavior.Action} {behavior.Target}.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public class {behaviorName}Component");
            sb.AppendLine("    {");
            sb.AppendLine("        /// <summary>Vitesse du comportement.</summary>");
            sb.AppendLine("        public float Speed { get; set; } = 1.0f;");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Le comportement est-il actif ?</summary>");
            sb.AppendLine("        public bool IsActive { get; set; } = true;");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>Distance maximale d'activation.</summary>");
            sb.AppendLine("        public float MaxDistance { get; set; } = 100.0f;");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            result.Files.Add(new GeneratedFile
            {
                RelativePath = $"Behaviors/{behaviorName}/{behaviorName}Component.cs",
                Content = sb.ToString(),
                Reason = $"Composant du comportement {behaviorName}"
            });
        }

        private void GenerateSystem(BehaviorDescriptor behavior, string behaviorName, BuilderResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine($"namespace Snake2000.Engine.Behaviors.{behavior.Subject}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Comportement : {behavior.Subject} {behavior.Action} {behavior.Target}.");
            sb.AppendLine("    /// Généré automatiquement par MOTO Editor.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public class {behaviorName}System");
            sb.AppendLine("    {");
            sb.AppendLine($"        private readonly {behaviorName}Component _component;");
            sb.AppendLine();
            sb.AppendLine($"        public {behaviorName}System({behaviorName}Component component)");
            sb.AppendLine("        {");
            sb.AppendLine("            _component = component ?? throw new ArgumentNullException(nameof(component));");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Met à jour le comportement à chaque frame.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public void Update(float deltaTime)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!_component.IsActive) return;");
            sb.AppendLine();
            sb.AppendLine($"            // TODO: implémenter {behavior.Action} vers {behavior.Target}");
            sb.AppendLine($"            // Exemple :");
            sb.AppendLine($"            // var direction = GetDirectionTowards{behavior.Target}();");
            sb.AppendLine($"            // Move(direction * _component.Speed * deltaTime);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            result.Files.Add(new GeneratedFile
            {
                RelativePath = $"Behaviors/{behaviorName}/{behaviorName}System.cs",
                Content = sb.ToString(),
                Reason = $"Système du comportement {behaviorName}"
            });
        }

        private void GenerateIntegration(BehaviorDescriptor behavior, string behaviorName, BuilderResult result)
        {
            result.Integrations.Add(new IntegrationAction
            {
                TargetFile = "XenoPipeline.cs",
                Action = "Ajouter le comportement au pipeline",
                CodeSnippet = $"var {behaviorName.ToLower()}System = new {behaviorName}System(new {behaviorName}Component());",
                Reason = $"Connecter {behaviorName}System au pipeline"
            });
        }
    }
}
