// Moto.Core/Platform/PlatformModels.cs
using System.Collections.Generic;

namespace Moto.Core.Platform
{
    public enum TargetPlatform { Android, iOS, MacOS, Linux, Windows }

    /// <summary>Résultat de détection pour une plateforme.</summary>
    public class PlatformDetection
    {
        public TargetPlatform Platform { get; set; }
        public bool AlreadySupported { get; set; }
        public double Confidence { get; set; }
        public List<string> Signals { get; } = new();
    }

    /// <summary>Fichier à générer pour un portage.</summary>
    public class PlatformFileAction
    {
        public string RelativePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public bool RewriteCsproj { get; set; }
    }

    /// <summary>Proposition de portage complète.</summary>
    public class PlatformProposal
    {
        public TargetPlatform Platform { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public List<PlatformFileAction> Files { get; } = new();
        public string NewTargetFrameworks { get; set; } = string.Empty;
    }

    /// <summary>Rapport global d'analyse multiplateforme.</summary>
    public class PlatformReport
    {
        public bool IsMauiProject { get; set; }
        public string RootNamespace { get; set; } = string.Empty;
        public string CsprojPath { get; set; } = string.Empty;
        public string CurrentTargetFrameworks { get; set; } = string.Empty;
        public List<PlatformDetection> Detections { get; } = new();
        public List<PlatformProposal> Proposals { get; } = new();
    }
}
