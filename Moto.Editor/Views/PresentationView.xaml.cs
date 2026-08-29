// Moto.Editor/Views/PresentationView.xaml.cs
using System;
using Microsoft.Maui.Controls;
using Moto.Core.Export;

namespace Moto.Editor.Views
{
    public partial class PresentationView : ContentView
    {
        public event Action<PresentationRequest> GenerateRequested;

        public PresentationView()
        {
            InitializeComponent();
            KindPicker.SelectedIndex = 0;
        }

        private void OnGenerateClicked(object s, EventArgs e)
        {
            var kind = (PresentationKind)KindPicker.SelectedIndex;

            GenerateRequested?.Invoke(new PresentationRequest
            {
                Kind = kind,
                ProjectName = ProjectEntry.Text ?? "Projet",
                Author = AuthorEntry.Text ?? "MOTO Editor",
                Context = ContextEditor.Text ?? ""
            });
        }

        public void ShowStatus(string msg) => StatusLabel.Text = msg;
    }
}
