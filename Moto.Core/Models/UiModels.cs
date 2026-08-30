// Models/UiModels.cs
using System.ComponentModel;
using Microsoft.Maui.Graphics;

namespace Moto.Editor.Models
{
    /// <summary>
    /// Fichier affiché dans la sidebar.
    /// </summary>
    public class FileItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    // EditorDocument : retiré d'ici (30/08) — doublon de Moto.Editor/Models/EditorDocument.cs
    // (celui-là a ErrorCount/HasErrors/ErrorBadge, requis par EditorPaneView/MainPage).
    // Comme MainViewModel.cs vit maintenant physiquement dans Moto.Editor (voir plus bas),
    // garder les deux créait un conflit de type entre assemblies (Moto.Core.dll vs
    // Moto.Editor.dll) pour un nom de type identique.

    /// <summary>
    /// Ligne du terminal intégré.
    /// </summary>
    public class TerminalLine
    {
        public string Text { get; set; } = string.Empty;
        public bool IsError { get; set; }

        public Color TextColor => IsError
            ? Colors.OrangeRed
            : Colors.LimeGreen;
    }

    /// <summary>
    /// Diagnostic affiché dans le panneau diagnostics.
    /// </summary>
    public class DiagnosticItem
    {
        public string Severity { get; set; } = "info";
        public string Message { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public int Line { get; set; }
    }

    /// <summary>
    /// Suggestion IA affichée dans le panneau IA.
    /// </summary>
    public class AiSuggestion
    {
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Action rapide contextuelle.
    /// </summary>
    public class AiQuickAction
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Action Action { get; set; }
    }
}
