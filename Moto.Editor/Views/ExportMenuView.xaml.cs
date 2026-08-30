// Moto.Editor/Views/ExportMenuView.xaml.cs
using System;
using Microsoft.Maui.Controls;
using Moto.Core.Export;

namespace Moto.Editor.Views
{
    public partial class ExportMenuView : ContentView
    {
        private readonly ExportEngine _engine = new();

        /// <summary>Demande d'export transmise à MainPage.</summary>
        public event Action<ExportFormat, string> ExportRequested;

        public ExportMenuView()
        {
            InitializeComponent();
            FormatPicker.SelectedIndex = 0;
        }

        public (ExportFormat Format, string Author) Pick()
        {
            var format = FormatPicker.SelectedIndex switch
            {
                1 => ExportFormat.Markdown,
                2 => ExportFormat.Html,
                3 => ExportFormat.Pdf,
                4 => ExportFormat.Docx,
                5 => ExportFormat.Odt,
                6 => ExportFormat.Rtf,
                7 => ExportFormat.Json,
                8 => ExportFormat.Csv,
                _ => ExportFormat.Txt
            };

            return (format, AuthorEntry.Text?.Trim() ?? "MOTO Editor");
        }

        private void OnExportClicked(object s, EventArgs e)
        {
            var (format, author) = Pick();
            ExportRequested?.Invoke(format, author);
        }

        private void OnCloseClicked(object s, EventArgs e) => IsVisible = false;

        public void ShowStatus(string msg) => StatusLabel.Text = msg;
    }
}
