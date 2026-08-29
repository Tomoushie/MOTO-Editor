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

    /// <summary>
    /// Document ouvert dans un onglet.
    /// </summary>
    public class EditorDocument : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _text = string.Empty;
        private string _path = string.Empty;

        public string Title
        {
            get => _title;
            set => SetField(ref _title, value);
        }

        public string Text
        {
            get => _text;
            set => SetField(ref _text, value);
        }

        public string Path
        {
            get => _path;
            set => SetField(ref _path, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

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
