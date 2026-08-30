// Moto.Core/AI/Builders/BehaviorBuilderV2.cs
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Moto.Editor.AI.Builders;

namespace Moto.Core.AI.Builders
{
    /// <summary>
    /// AI Behavior Builder v2 : comprend les verbes français
    /// (suis, attaque, fuis, évite, patrouille) et génère
    /// System + Component + intégration pipeline.
    /// </summary>
    public class BehaviorBuilderV2
    {
        private static readonly Dictionary<string, string> VerbMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "suis", "Follow" }, { "suive", "Follow" }, { "suivre", "Follow" },
            { "attaque", "Attack" }, { "attaquer", "Attack" },
            { "fuis", "Flee" }, { "fuir", "Flee" },
            { "évite", "Avoid" }, { "evite", "Avoid" }, { "éviter", "Avoid" },
            { "patrouille", "Patrol" }, { "patrouiller", "Patrol" }
        };

        /// <summary>
        /// Génère un comportement complet depuis une phrase.
        /// "Fais que l'ennemi me suive." → EnemyFollowSystem + Component + intégration.
        /// </summary>
        public List<GeneratedFile> Build(string description, out List<IntegrationAction> integrations)
        {
            integrations = new List<IntegrationAction>();

            var subject = ExtractSubject(description);
            var action = ExtractAction(description);
            var target = ExtractTarget(description);
            var speed = ExtractNumber(description, defaultSpeed: 3f);

            var behaviorName = $"{subject}{action}";
            var ns = $"Snake2000.Engine.Behaviors.{subject}";

            var files = new List<GeneratedFile>();

            files.Add(new GeneratedFile
            {
                RelativePath = $"Behaviors/{behaviorName}/{behaviorName}Component.cs",
                Reason = $"Données du comportement {subject} {action} {target}.",
                Content = BuildComponent(ns, behaviorName, speed)
            });

            files.Add(new GeneratedFile
            {
                RelativePath = $"Behaviors/{behaviorName}/{behaviorName}System.cs",
                Reason = $"Logique du comportement {subject} {action} {target}.",
                Content = BuildSystem(ns, behaviorName, action, target)
            });

            files.Add(new GeneratedFile
            {
                RelativePath = $"Behaviors/{behaviorName}/{behaviorName}Integration.cs",
                Reason = "Intégration au pipeline.",
                Content = BuildIntegration(ns, behaviorName)
            });

            integrations.Add(new IntegrationAction
            {
                TargetFile = "XenoPipeline.cs",
                Action = $"Enregistrer {behaviorName}System",
                CodeSnippet = $"var {behaviorName.ToLower()} = {ns}.{behaviorName}Integration.Create();",
                Reason = $"Connecter le comportement {behaviorName} au pipeline."
            });

            return files;
        }

        private string ExtractSubject(string text)
        {
            if (text.Contains("ennemi", StringComparison.OrdinalIgnoreCase)) return "Enemy";
            if (text.Contains("joueur", StringComparison.OrdinalIgnoreCase)) return "Player";
            if (text.Contains("npc", StringComparison.OrdinalIgnoreCase)) return "Npc";

            var match = Regex.Match(text, @"\b([A-Z][a-z]+)\b");
            return match.Success ? match.Groups[1].Value : "Entity";
        }

        private string ExtractAction(string text)
        {
            foreach (var kv in VerbMap)
            {
                if (text.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kv.Value;
                }
            }

            return "Update";
        }

        private string ExtractTarget(string text)
        {
            if (text.Contains(" me ", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("moi", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("le joueur", StringComparison.OrdinalIgnoreCase))
            {
                return "Player";
            }

            return "Target";
        }

        private float ExtractNumber(string text, float defaultSpeed)
        {
            var match = Regex.Match(text, @"(\d+(?:[.,]\d+)?)");
            return match.Success ? float.Parse(match.Groups[1].Value.Replace(",", ".")) : defaultSpeed;
        }

        private string BuildComponent(string ns, string name, float speed)
        {
            return $@"using System;

namespace {ns}
{{
    /// <summary>Données du comportement, généré par MOTO AI.</summary>
    public class {name}Component
    {{
        public float Speed {{ get; set; }} = {speed}f;
        public float DetectionRange {{ get; set; }} = 10f;
        public bool IsActive {{ get; set; }} = true;
    }}
}}";
        }

        private string BuildSystem(string ns, string name, string action, string target)
        {
            return $@"using System;

namespace {ns}
{{
    /// <summary>Comportement : {action} vers {target}. Généré par MOTO AI.</summary>
    public class {name}System
    {{
        private readonly {name}Component _component;

        public {name}System({name}Component component)
        {{
            _component = component ?? throw new ArgumentNullException(nameof(component));
        }}

        public void Update(float deltaTime)
        {{
            if (!_component.IsActive) return;

            // TODO: implémenter {action} vers {target}.
            // var direction = GetDirectionTowards{{_targetPlaceholder}}();
            // Move(direction * _component.Speed * deltaTime);
        }}
    }}
}}".Replace("_targetPlaceholder", target);
        }

        private string BuildIntegration(string ns, string name)
        {
            return $@"using System;

namespace {ns}
{{
    /// <summary>Point d'entrée d'intégration du comportement au pipeline.</summary>
    public static class {name}Integration
    {{
        public static {name}System Create()
        {{
            return new {name}System(new {name}Component());
        }}
    }}
}}";
        }
    }
}
