using System;
using System.Linq;
using Moto.Editor.Models;

namespace Moto.Editor.Controls;

/// <summary>
/// A CONNECTER — relie AddTabForImage au gestionnaire d'onglets interne.
/// Fichier partial : ne modifie pas EditorPaneView.cs existant.
/// </summary>
public partial class EditorPaneView
{
    /// <summary>
    /// Implémentation réelle de la greffe image (déjà déclarée en partial).
    /// Réutilise le pipeline d'onglets existant, évite les doublons.
    /// </summary>
    partial void AddTabForImage(ImageDocument imageDocument)
    {
        if (imageDocument is null) return;

        // 1. Évite les doublons : si l'image est déjà ouverte, on l'active
        var existing = FindTabByPath(imageDocument.FilePath);
        if (existing != null)
        {
            ActivateTab(existing);
            return;
        }

        // 2. Crée l'onglet via le pipeline interne existant
        CreateImageTab(imageDocument);
    }

    /// <summary>
    /// Recherche un onglet par chemin de fichier.
    /// NOTE : adaptez le nom si votre méthode interne diffère (ex: GetTabByPath).
    /// </summary>
    private object? FindTabByPath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        // Parcours des onglets ouverts (à brancher sur votre collection réelle)
        return OpenTabs.FirstOrDefault(t => t.FilePath == filePath);
    }

    /// <summary>Active un onglet existant (à brancher sur votre méthode réelle).</summary>
    private void ActivateTab(object tab)
    {
        // Sélectionne l'onglet dans le TabControl interne
        SelectedTab = tab;
    }

    /// <summary>Crée un onglet image via ImageViewerView (déjà existant).</summary>
    private void CreateImageTab(ImageDocument imageDocument)
    {
        var viewer = new Moto.Editor.Views.ImageViewerView(imageDocument);
        AddTab(imageDocument.DisplayName, viewer, imageDocument.FilePath);
    }
}
