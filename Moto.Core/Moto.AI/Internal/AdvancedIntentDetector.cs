// Moto.Core/AI/Internal/AdvancedIntentDetector.cs
using System;

namespace Moto.Core.AI.Internal
{
    public enum AdvancedIntentKind
    {
        None, TimeMachineRestore, TimeMachineSnapshot, HealthCheck,
        PatternDetect, UiGenerate, BehaviorBuild, Navigate, ArchitectureBuild
    }

    public class AdvancedIntent
    {
        public AdvancedIntentKind Kind { get; set; } = AdvancedIntentKind.None;
        public string RawText { get; set; } = string.Empty;
    }

    /// <summary>
    /// Détecte les intentions avancées avant les intentions de base.
    /// </summary>
    public class AdvancedIntentDetector
    {
        public AdvancedIntent Detect(string text)
        {
            var intent = new AdvancedIntent { RawText = text ?? string.Empty };
            var lower = (text ?? string.Empty).ToLowerInvariant();

            if (Contains(lower, "reviens", "retourne", "revenir", "annule", "état d'il y a", "etat d'il y a"))
                intent.Kind = AdvancedIntentKind.TimeMachineRestore;
            else if (Contains(lower, "snapshot", "sauvegarde l'état", "point de restauration"))
                intent.Kind = AdvancedIntentKind.TimeMachineSnapshot;
            else if (Contains(lower, "santé", "sante", "health", "audit"))
                intent.Kind = AdvancedIntentKind.HealthCheck;
            else if (Contains(lower, "pattern", "design pattern", "factory"))
                intent.Kind = AdvancedIntentKind.PatternDetect;
            else if (Contains(lower, "page", "écran", "ecran", "xaml", " ui "))
                intent.Kind = AdvancedIntentKind.UiGenerate;
            else if (Contains(lower, "fais que", "comportement", "ennemi me", "quand je"))
                intent.Kind = AdvancedIntentKind.BehaviorBuild;
            else if (Contains(lower, "où est", "ou est", "montre-moi où", "montre moi ou", "utilisé", "défini", "defini"))
                intent.Kind = AdvancedIntentKind.Navigate;
            else if (Contains(lower, "architecture", "restructure", "réorganise", "reorganise"))
                intent.Kind = AdvancedIntentKind.ArchitectureBuild;

            return intent;
        }

        private bool Contains(string text, params string[] keywords)
        {
            foreach (var k in keywords)
            {
                if (text.Contains(k, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
