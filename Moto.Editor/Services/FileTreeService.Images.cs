using System.IO;
using System.Linq;
using Moto.Editor.Models;

namespace Moto.Editor.Services;

/// <summary>
/// A CONNECTER — étend FileTreeService pour reconnaître les images.
/// Fichier partial : n'altère pas le comportement existant du tree.
/// </summary>
public partial class FileTreeService
{
    private static readonly string[] SupportedImageExtensions =
        { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".ico" };

    /// <summary>
    /// Indique si un nœud est une image supportée par le lecteur intégré.
    /// Utilisé pour router vers ImageOpenerService.
    /// </summary>
    public bool IsImageNode(FileNode node)
    {
        if (node is null || node.IsDirectory) return false;
        string ext = Path.GetExtension(node.Path).ToLowerInvariant();
        return SupportedImageExtensions.Contains(ext) && ImageDocument.IsSupported(node.Path);
    }

    /// <summary>
    /// Hook appelé lors du clic sur un nœud : route vers l'image viewer si applicable.
    /// Retourne true si le nœud a été géré comme image (sinon comportement éditeur normal).
    /// </summary>
    public bool TryOpenAsImage(FileNode node)
    {
        if (!IsImageNode(node)) return false;
        var doc = ImageDocument.FromPath(node.Path);
        if (doc is null) return false;
        ImageOpenerService.Open(doc);
        return true;
    }
}
