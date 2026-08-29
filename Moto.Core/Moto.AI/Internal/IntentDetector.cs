// Moto.Core/AI/Internal/IntentDetector.cs
using System;
using System.Text.RegularExpressions;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// Détecte l'intention du développeur à partir d'une phrase simple.
    /// Fonctionne hors ligne via règles locales.
    /// </summary>
    public class IntentDetector
    {
        private static readonly Regex ModuleRegex = new Regex(
            @"\bmodule\s+(\w+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        /// <summary>
        /// Analyse la phrase utilisateur et retourne une intention.
        /// </summary>
        public AiIntent Detect(string userText)
        {
            var intent = new AiIntent
            {
                RawText = userText ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(userText))
            {
                intent.Kind = AiIntentKind.Unknown;
                intent.Description = "Aucune demande fournie.";
                return intent;
            }

            var lower = userText.ToLowerInvariant();

            if (Contains(lower, "comprends", "analyse", "carte", "projet", "structure"))
            {
                intent.Kind = AiIntentKind.UnderstandProject;
                intent.Description = "Comprendre le projet et construire sa carte mentale.";
                intent.Confidence = 0.7;
            }

            if (Contains(lower, "répare", "corrige", "fix", "erreur", "cassé"))
            {
                intent.Kind = AiIntentKind.FixProject;
                intent.Description = "Corriger automatiquement le projet.";
                intent.Confidence = 0.9;
            }

            if (Contains(lower, "améliore", "optimise", "refactor", "improve", "nettoie"))
            {
                intent.Kind = AiIntentKind.ImproveProject;
                intent.Description = "Améliorer automatiquement le projet.";
                intent.Confidence = 0.85;
            }

            if (Contains(lower, "explique", "explication", "comprendre ce fichier"))
            {
                intent.Kind = AiIntentKind.ExplainCode;
                intent.Description = "Expliquer le code de manière pédagogique.";
                intent.Confidence = 0.9;
            }

            if (Contains(lower, "apprends", "apprendre", "enseigne", "professeur", "leçon"))
            {
                intent.Kind = AiIntentKind.TeachConcept;
                intent.Description = "Enseigner un concept.";
                intent.Confidence = 0.9;
            }

            if (Contains(lower, "doc", "readme", "documentation", "structure.md"))
            {
                intent.Kind = AiIntentKind.AutoDoc;
                intent.Description = "Générer la documentation du projet.";
                intent.Confidence = 0.9;
            }

            if (Contains(lower, "lien", "connecte", "autolink", "intégration"))
            {
                intent.Kind = AiIntentKind.AutoLink;
                intent.Description = "Connecter les modules entre eux.";
                intent.Confidence = 0.8;
            }

            if (Contains(lower, "portage", "android", "ios", "macos", "linux", "multiplateforme"))
            {
                intent.Kind = AiIntentKind.AutoPort;
                intent.Description = "Préparer un portage multiplateforme.";
                intent.Confidence = 0.8;
            }

            if (Contains(lower, "pipeline", "orchestrateur", "xeno"))
            {
                intent.Kind = AiIntentKind.GenerateArchitecture;
                intent.Description = "Générer ou compléter un pipeline.";
                intent.Confidence = 0.75;
            }

            if (Contains(lower, "module", "système", "system", "classe", "interface", "génère", "ajoute", "crée"))
            {
                if (intent.Kind == AiIntentKind.Unknown)
                {
                    intent.Kind = AiIntentKind.GenerateModule;
                    intent.Description = "Générer un module ECS cohérent.";
                    intent.Confidence = 0.8;
                }
            }

            intent.ModuleName = ExtractModuleName(lower);

            return intent;
        }

        private string ExtractModuleName(string lower)
        {
            if (Contains(lower, "santé", "health", "vie", "hp"))
            {
                return "Health";
            }

            if (Contains(lower, "mouvement", "movement", "move", "déplacement"))
            {
                return "Movement";
            }

            if (Contains(lower, "combat", "attaque", "attack", "dégâts", "damage"))
            {
                return "Combat";
            }

            if (Contains(lower, "inventaire", "inventory"))
            {
                return "Inventory";
            }

            if (Contains(lower, "input", "contrôles", "controls", "clavier", "manette"))
            {
                return "Input";
            }

            if (Contains(lower, "rendu", "render", "rendering", "affichage"))
            {
                return "Rendering";
            }

            if (Contains(lower, "camera", "caméra"))
            {
                return "Camera";
            }

            if (Contains(lower, "audio", "son", "sound", "musique"))
            {
                return "Audio";
            }

            var match = ModuleRegex.Match(lower);

            if (match.Success)
            {
                return Normalize(match.Groups[1].Value);
            }

            return string.Empty;
        }

        private bool Contains(string text, params string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            value = value.Trim();

            if (value.Length == 0)
            {
                return string.Empty;
            }

            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }
    }
}
