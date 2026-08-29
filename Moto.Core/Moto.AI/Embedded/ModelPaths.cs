// Moto.Core/AI/Embedded/ModelPaths.cs
using System;
using System.IO;

namespace Moto.Core.AI.Embedded;

/// <summary>
/// Centralise et sécurise tous les chemins d'accès aux modèles IA.
/// Empêche le path traversal et garantit la cohérence entre les composants.
3. /// </summary>
public static class ModelPaths
{
    private static readonly string BaseDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MotoEditor", "models");

    private static readonly string ManifestPath = Path.Combine(BaseDirectory, "manifest.json");

    static ModelPaths()
    {
        Directory.CreateDirectory(BaseDirectory);
    }

    /// <summary>
    /// Retourne le chemin sécurisé d'un fichier modèle.
    /// </summary>
    public static string GetModelPath(string fileName)
    {
        // Nettoyage agressif pour empêcher le path traversal (ex: "../../../etc/passwd")
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
            throw new ArgumentException("Nom de fichier invalide", nameof(fileName));

        return Path.Combine(BaseDirectory, safeName);
    }

    /// <summary>
    /// Retourne le chemin du manifeste de sécurité.
    /// </summary>
    public static string GetManifestPath() => ManifestPath;

    /// <summary>
    /// Vérifie si un modèle existe de manière sécurisée.
    /// </summary>
    public static bool ModelExists(string fileName) => File.Exists(GetModelPath(fileName));

    /// <summary>
    /// Retourne la taille du modèle en octets, ou 0 s'il n'existe pas.
    /// </summary>
    public static long GetModelSizeBytes(string fileName)
    {
        var path = GetModelPath(fileName);
        return File.Exists(path) ? new FileInfo(path).Length : 0;
    }
}
