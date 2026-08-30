using Moto.Editor.Models;

namespace Moto.Editor.Controls;

/// <summary>
/// Item 56 — Relie AddTabForImage au gestionnaire d'onglets interne.
/// Fichier partial : ne modifie pas EditorPaneView.cs existant.
/// </summary>
public partial class EditorPaneView
{
    // Déclaration de la méthode partielle (obligatoire même sans corps ici) ;
    // l'implémentation réelle est dans EditorPaneView.ImageTabs.cs.
    partial void AddTabForImage(ImageDocument imageDocument);

    /// <summary>
    /// Point d'entrée public appelé par ImageOpenerService.
    /// Délègue à la méthode interne de création d'onglets.
    /// </summary>
    public void OpenImageDocument(ImageDocument imageDocument)
    {
        if (imageDocument is null) return;
        AddTabForImage(imageDocument); // implémentation réelle dans EditorPaneView.ImageTabs.cs
    }
}
