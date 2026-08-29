using Moto.Editor.Models;

namespace Moto.Editor.Controls;

/// <summary>
/// Item 56 — Relie AddTabForImage au gestionnaire d'onglets interne.
/// Fichier partial : ne modifie pas EditorPaneView.cs existant.
/// </summary>
public partial class EditorPaneView
{
    /// <summary>
    /// Point d'entrée public appelé par ImageOpenerService.
    /// Délègue à la méthode interne de création d'onglets.
    /// </summary>
    public void OpenImageDocument(ImageDocument imageDocument)
    {
        if (imageDocument is null) return;
        AddTabForImage(imageDocument); // méthode partielle déjà déclarée
    }

    /// <summary>
    /// Implémentation réelle de la greffe image.
    /// Réutilise le pipeline d'onglets existant (aucune duplication).
    /// </summary>
    partial void AddTabForImage(ImageDocument imageDocument)
    {
        // Vérifie si l'image est déjà ouverte (évite les doublons d'onglets)
        var existing = FindTabByPath(imageDocument.FilePath);
        if (existing != null)
        {
            ActivateTab(existing);
            return;
        }
        CreateImageTab(imageDocument);
    }
}
