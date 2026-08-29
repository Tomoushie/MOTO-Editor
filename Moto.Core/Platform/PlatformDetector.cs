// Moto.Core/Platform/PlatformDetector.cs — AJOUT (4) : détection intelligente
// Ajoute ce bloc à la classe PlatformDetector existante :

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Moto.Core.Platform
{
    public partial class PlatformDetector
    {

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
