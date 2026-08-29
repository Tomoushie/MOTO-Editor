// Moto.Core/AI/Internal/AutoPortEngine.cs
using System.Collections.Generic;
using System.Text;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// AutoPort Engine.
    /// Détecte les intentions multiplateformes et prépare les portages.
    /// </summary>
    public class AutoPortEngine
    {
        /// <summary>
        /// Plateforme cible détectée.
        /// </summary>
        public enum TargetPlatform
        {
            Windows,
            Android,
            iOS,
            MacOS,
            Linux
        }

        /// <summary>
        /// Détecte les intentions de portage dans le texte utilisateur.
        /// </summary>
        public List<TargetPlatform> DetectPlatforms(string userText)
        {
            var platforms = new List<TargetPlatform>();
            var lower = userText?.ToLowerInvariant() ?? string.Empty;

            if (lower.Contains("android")) platforms.Add(TargetPlatform.Android);
            if (lower.Contains("ios") || lower.Contains("iphone") || lower.Contains("ipad")) platforms.Add(TargetPlatform.iOS);
            if (lower.Contains("macos") || lower.Contains("mac")) platforms.Add(TargetPlatform.MacOS);
            if (lower.Contains("linux")) platforms.Add(TargetPlatform.Linux);
            if (lower.Contains("windows")) platforms.Add(TargetPlatform.Windows);

            if (lower.Contains("multiplateforme") || lower.Contains("toutes les plateformes"))
            {
                platforms.Clear();
                platforms.AddRange(new[]
                {
                    TargetPlatform.Windows,
                    TargetPlatform.Android,
                    TargetPlatform.iOS,
                    TargetPlatform.MacOS,
                    TargetPlatform.Linux
                });
            }

            return platforms;
        }

        /// <summary>
        /// Génère le plan de portage pour les plateformes détectées.
        /// </summary>
        public List<AiFileChange> GeneratePortPlan(List<TargetPlatform> platforms, ProjectMap map)
        {
            var changes = new List<AiFileChange>();

            var sb = new StringBuilder();
            sb.AppendLine("# Plan de portage multiplateforme");
            sb.AppendLine();
            sb.AppendLine("Généré par MOTO AI AutoPort Engine.");
            sb.AppendLine();

            foreach (var platform in platforms)
            {
                sb.AppendLine($"## {platform}");
                sb.AppendLine();
                sb.AppendLine(GetPlatformSteps(platform));
                sb.AppendLine();
            }

            sb.AppendLine("## Règles de portage");
            sb.AppendLine();
            sb.AppendLine("1. La logique métier reste dans `Moto.Core`.");
            sb.AppendLine("2. L'UI reste dans `Moto.Editor` (MAUI).");
            sb.AppendLine("3. Les appels plateforme-spécifiques passent par des interfaces.");
            sb.AppendLine("4. Aucun `#if` dans la logique métier.");
            sb.AppendLine("5. Tester chaque plateforme avant de passer à la suivante.");

            changes.Add(new AiFileChange
            {
                Path = "Docs/AUTOPORT.md",
                Content = sb.ToString(),
                Reason = "Plan de portage multiplateforme.",
                ChangeType = FileChangeType.Create
            });

            // Générer le fichier csproj MAUI multiplateforme
            changes.Add(new AiFileChange
            {
                Path = "Moto.Editor/Moto.Editor.multiplatform.csproj",
                Content = GenerateMultiplatformCsproj(platforms),
                Reason = "Projet MAUI multiplateforme.",
                ChangeType = FileChangeType.Create
            });

            return changes;
        }

        private string GetPlatformSteps(TargetPlatform platform)
        {
            return platform switch
            {
                TargetPlatform.Android =>
                    "- Ajouter `net8.0-android` dans TargetFrameworks\n" +
                    "- Configurer AndroidManifest.xml\n" +
                    "- Tester sur émulateur API 33+\n" +
                    "- Vérifier les permissions",

                TargetPlatform.iOS =>
                    "- Ajouter `net8.0-ios` dans TargetFrameworks\n" +
                    "- Configurer Info.plist\n" +
                    "- Tester sur simulateur iOS 16+\n" +
                    "- Vérifier les entitlements",

                TargetPlatform.MacOS =>
                    "- Ajouter `net8.0-maccatalyst` dans TargetFrameworks\n" +
                    "- Configurer Info.plist pour macOS\n" +
                    "- Tester sur macOS 13+\n" +
                    "- Vérifier le sandboxing",

                TargetPlatform.Linux =>
                    "- Utiliser GTK ou Avalonia pour le support Linux\n" +
                    "- MAUI ne supporte pas Linux nativement\n" +
                    "- Alternative : séparer l'UI dans un projet Avalonia",

                TargetPlatform.Windows =>
                    "- Déjà supporté par MAUI\n" +
                    "- Vérifier le packaging MSIX\n" +
                    "- Tester sur Windows 10 19041+",

                _ => "- Aucune étape spécifique."
            };
        }

        private string GenerateMultiplatformCsproj(List<TargetPlatform> platforms)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
            sb.AppendLine();
            sb.AppendLine("  <PropertyGroup>");

            var frameworks = new List<string>();

            foreach (var p in platforms)
            {
                switch (p)
                {
                    case TargetPlatform.Android: frameworks.Add("net8.0-android"); break;
                    case TargetPlatform.iOS: frameworks.Add("net8.0-ios"); break;
                    case TargetPlatform.MacOS: frameworks.Add("net8.0-maccatalyst"); break;
                    case TargetPlatform.Windows: frameworks.Add("net8.0-windows10.0.19041.0"); break;
                }
            }

            if (frameworks.Count > 0)
            {
                sb.AppendLine($"    <TargetFrameworks>{string.Join(";", frameworks)}</TargetFrameworks>");
            }

            sb.AppendLine("    <OutputType>Exe</OutputType>");
            sb.AppendLine("    <UseMaui>true</UseMaui>");
            sb.AppendLine("    <SingleProject>true</SingleProject>");
            sb.AppendLine("  </PropertyGroup>");
            sb.AppendLine();
            sb.AppendLine("</Project>");

            return sb.ToString();
        }
    }
}
