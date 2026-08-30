// Moto.Core/Platform/PlatformDetector.cs — AJOUT (4) : détection intelligente
// Ajoute ce bloc à la classe PlatformDetector existante :

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Moto.Core.Platform
{
    public partial class PlatformDetector
    {
    /// <summary>
    /// Analyse basique du workspace : repère le .csproj principal, détecte si c'est un
    /// projet MAUI et extrait les TargetFramework(s) actuels. Base minimale sur laquelle
    /// PlatformEngine (portages, CI) s'appuie ; les détections fines (2) restent à enrichir.
    /// </summary>
    public PlatformReport Analyze(string workspace)
    {
        var report = new PlatformReport();

        try
        {
            var csproj = Directory.Exists(workspace)
                ? Directory.EnumerateFiles(workspace, "*.csproj", SearchOption.AllDirectories).FirstOrDefault()
                : null;

            if (csproj != null)
            {
                report.CsprojPath = csproj;
                var content = File.ReadAllText(csproj);

                report.IsMauiProject = content.Contains("<UseMaui>true</UseMaui>", StringComparison.OrdinalIgnoreCase)
                    || content.Contains("Microsoft.Maui.Controls", StringComparison.OrdinalIgnoreCase);

                var tfmMatch = Regex.Match(content, @"<TargetFrameworks?>([^<]+)</TargetFrameworks?>");
                if (tfmMatch.Success)
                    report.CurrentTargetFrameworks = tfmMatch.Groups[1].Value.Trim();

                var nsMatch = Regex.Match(content, @"<RootNamespace>([^<]+)</RootNamespace>");
                report.RootNamespace = nsMatch.Success
                    ? nsMatch.Groups[1].Value.Trim()
                    : Path.GetFileNameWithoutExtension(csproj);
            }
        }
        catch
        {
            // Analyse best-effort : un workspace illisible ne doit pas planter l'appelant.
        }

        return report;
    }

    /// <summary>Regex combinée : tout pattern plateforme connu.</summary>
private static readonly Regex CombinedSignal = new Regex(
    @"(#if\s+(ANDROID|IOS|MACCATALYST|WINDOWS)|__ANDROID__|__IOS__|__MACOS__|" +
    @"using\s+(Android|UIKit|AppKit)|OperatingSystem\.Is(Linux|Windows)|" +
    @"MauiAppCompatActivity|UIApplicationDelegate|" +
    @"(?i)(port(age|er)?|support(er)?|cibler)\w*\s+(android|ios|macos|linux|windows))",
    RegexOptions.Compiled);

/// <summary>
/// 4. Détection continue intelligente : true si le fichier contient
/// un pattern plateforme (évite les re-analyses inutiles).
/// </summary>
public static bool ContainsPlatformSignal(string filePath)
{
    try
    {
        var info = new FileInfo(filePath);

        // Fichiers > 2 Mo : skip (trop lourds pour un scan synchrone)
        if (!info.Exists || info.Length > 2 * 1024 * 1024) return false;

        var content = File.ReadAllText(filePath);
        return CombinedSignal.IsMatch(content);
    }
    catch
    {
        return false;
    }
}
    }
}
