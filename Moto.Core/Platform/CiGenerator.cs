// Moto.Core/Platform/CiGenerator.cs (v2 — multi-providers)
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Moto.Core.Platform
{
    /// <summary>Provider de CI cible.</summary>
    public enum CiProvider { GitHub, GitLab, Azure, All }

    /// <summary>
    /// 1. Génère les pipelines CI selon le provider choisi :
    /// GitHub Actions (1 fichier/plateforme), GitLab CI (1 fichier global),
    /// Azure DevOps (1 fichier global).
    /// </summary>
    public static class CiGenerator
    {
        public static CiProvider Parse(string value) => (value ?? "").ToLowerInvariant() switch
        {
            "gitlab" => CiProvider.GitLab,
            "azure" => CiProvider.Azure,
            "all" => CiProvider.All,
            _ => CiProvider.GitHub
        };

        /// <summary>Écrit les pipelines pour les plateformes données.</summary>
        public static void Generate(string workspace, CiProvider provider, IEnumerable<TargetPlatform> platforms)
        {
            var list = platforms.Distinct().ToList();
            if (list.Count == 0) return;

            // GitHub : un workflow par plateforme
            if (provider == CiProvider.GitHub || provider == CiProvider.All)
            {
                foreach (var p in list)
                {
                    var (file, content) = GitHubWorkflow(p);
                    Write(workspace, Path.Combine(".github", "workflows", file), content);
                }
            }

            // GitLab : un seul .gitlab-ci.yml accumulant tous les jobs
            if (provider == CiProvider.GitLab || provider == CiProvider.All)
            {
                Write(workspace, ".gitlab-ci.yml", GitLabCi(list));
            }

            // Azure : un seul azure-pipelines.yml
            if (provider == CiProvider.Azure || provider == CiProvider.All)
            {
                Write(workspace, "azure-pipelines.yml", AzurePipelines(list));
            }
        }

        // ------------------------------------------------------------------
        // GitHub Actions
        // ------------------------------------------------------------------

        private static (string File, string Content) GitHubWorkflow(TargetPlatform p)
        {
            var runner = p switch
            {
                TargetPlatform.iOS or TargetPlatform.MacOS => "macos-latest",
                TargetPlatform.Windows => "windows-latest",
                _ => "ubuntu-latest"
            };

            var step = p == TargetPlatform.Linux
                ? "dotnet publish -c Release -r linux-x64 --self-contained false"
                : $"dotnet build -c Release -f {TfmFor(p)}";

            return ($"build-{Name(p)}.yml", $@"name: build-{Name(p)}
on:
  push:
    branches: [ main ]
  pull_request:
jobs:
  build:
    runs-on: {runner}
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - name: Build {p}
        run: {step}
");
        }

        // ------------------------------------------------------------------
        // GitLab CI
        // ------------------------------------------------------------------

        private static string GitLabCi(List<TargetPlatform> list)
        {
            var sb = new StringBuilder();
            sb.AppendLine("stages:");
            sb.AppendLine("  - build");
            sb.AppendLine();

            foreach (var p in list)
            {
                var step = p == TargetPlatform.Linux
                    ? "dotnet publish -c Release -r linux-x64 --self-contained false"
                    : $"dotnet build -c Release -f {TfmFor(p)}";

                sb.AppendLine($"build-{Name(p)}:");
                sb.AppendLine("  stage: build");
                sb.AppendLine("  image: mcr.microsoft.com/dotnet/sdk:8.0");

                // Les builds iOS/Mac/Windows nécessitent des runners tagués
                if (p == TargetPlatform.iOS || p == TargetPlatform.MacOS)
                    sb.AppendLine("  tags: [ macos ]");
                if (p == TargetPlatform.Windows)
                    sb.AppendLine("  tags: [ windows ]");

                sb.AppendLine("  script:");
                sb.AppendLine($"    - {step}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // Azure DevOps
        // ------------------------------------------------------------------

        private static string AzurePipelines(List<TargetPlatform> list)
        {
            var sb = new StringBuilder();
            sb.AppendLine("trigger:");
            sb.AppendLine("  - main");
            sb.AppendLine();

            foreach (var p in list)
            {
                var pool = p switch
                {
                    TargetPlatform.iOS or TargetPlatform.MacOS => "macos-latest",
                    TargetPlatform.Windows => "windows-latest",
                    _ => "ubuntu-latest"
                };

                var step = p == TargetPlatform.Linux
                    ? "dotnet publish -c Release -r linux-x64 --self-contained false"
                    : $"dotnet build -c Release -f {TfmFor(p)}";

                sb.AppendLine($"- job: build_{Name(p)}");
                sb.AppendLine($"  displayName: Build {p}");
                sb.AppendLine($"  pool:");
                sb.AppendLine($"    vmImage: {pool}");
                sb.AppendLine("  steps:");
                sb.AppendLine("    - task: UseDotNet@2");
                sb.AppendLine("      inputs:");
                sb.AppendLine("        version: 8.0.x");
                sb.AppendLine($"    - script: {step}");
                sb.AppendLine($"      displayName: Build {p}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static string TfmFor(TargetPlatform p) => p switch
        {
            TargetPlatform.Android => "net8.0-android",
            TargetPlatform.iOS => "net8.0-ios",
            TargetPlatform.MacOS => "net8.0-maccatalyst",
            TargetPlatform.Windows => "net8.0-windows10.0.19041.0",
            _ => "net8.0"
        };

        private static string Name(TargetPlatform p) => p.ToString().ToLowerInvariant();

        private static void Write(string workspace, string relative, string content)
        {
            var path = Path.Combine(workspace, relative);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, content);
        }
    }
}
