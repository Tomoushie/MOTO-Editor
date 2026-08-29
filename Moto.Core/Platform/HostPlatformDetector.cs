// Moto.Core/Platform/HostPlatformDetector.cs
using System;

namespace Moto.Core.Platform;

public enum MotoHostOs { Windows, MacOS, Linux, Unknown }

/// <summary>
/// Détecte l'OS hôte à l'exécution (utilisé par le runtime ET l'installateur).
/// Distinct de PlatformDetector (portage de projets).
/// </summary>
public static class HostPlatformDetector
{
    public static MotoHostOs Current { get; } = Detect();

    public static bool IsWindows => Current == MotoHostOs.Windows;
    public static bool IsMacOS   => Current == MotoHostOs.MacOS;
    public static bool IsLinux   => Current == MotoHostOs.Linux;

    private static MotoHostOs Detect()
    {
        if (OperatingSystem.IsWindows()) return MotoHostOs.Windows;
        if (OperatingSystem.IsMacOS())   return MotoHostOs.MacOS;
        if (OperatingSystem.IsLinux())   return MotoHostOs.Linux;
        return MotoHostOs.Unknown;
    }

    /// <summary>Suffixe d'artefact attendu pour l'OS courant (pour le bootstrapper).</summary>
    public static string ArtifactSuffix => Current switch
    {
        MotoHostOs.Windows => "win-x64",
        MotoHostOs.MacOS   => "osx-x64",
        MotoHostOs.Linux   => "linux-x64",
        _ => "unknown"
    };
}
