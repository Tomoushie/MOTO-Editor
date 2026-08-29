// Moto.Editor/Views/DocPanelView.xaml.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Maui.Controls;
using Moto.Core.Doc;

namespace Moto.Editor.Views
{
    public partial class DocPanelView : ContentView
    {
        private List<DocFile> _files = new();

        /// <summary>Demande de régénération complète.</summary>
        public event Action RegenerateRequested;

        /// <summary>Demande d'ouverture d'un fichier doc.</summary>
        public event Action<string> OpenFileRequested;

        public DocPanelView()
        {
            InitializeComponent();
        }

        public void Load(DocReport report)
        {
            _files = report.Files.ToList();
            FilesList.ItemsSource = _files;

            SummaryLabel.Text =
                $"📊 {report.TotalFiles} fichiers analysés · " +
                $"{report.TotalSymbols} symboles · " +
                $"{_files.Count} fichiers de documentation générés.";
        }

        public void SetStatus(string message)
        {
            SummaryLabel.Text = message;
        }

        private void OnOpenClicked(object sender, EventArgs e)
        {
            if (((Button)sender).BindingContext is DocFile file)
            {
                OpenFileRequested?.Invoke(file.Path);
            }
        }

        private void OnRegenerateClicked(object sender, EventArgs e)
        {
            RegenerateRequested?.Invoke();
        }

        private void OnOpenFolderClicked(object sender, EventArgs e)
        {
            if (_files.Count > 0)
            {
                var folder = Path.GetDirectoryName(_files[0].Path);

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // Impossible d'ouvrir le dossier.
                }
            }
        }
    }
}
