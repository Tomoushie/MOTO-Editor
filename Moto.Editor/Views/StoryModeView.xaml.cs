// Moto.Editor/Views/StoryModeView.xaml.cs
using System;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Views
{
    public partial class StoryModeView : ContentView
    {
        /// <summary>Texte de l'histoire envoyé à MainPage pour génération.</summary>
        public event Action<string> GenerateRequested;

        public StoryModeView()
        {
            InitializeComponent();
        }

        private void OnGenerateClicked(object sender, EventArgs e)
        {
            var story = StoryInput.Text?.Trim();

            if (string.IsNullOrWhiteSpace(story)) return;

            GenerateRequested?.Invoke(story);
        }

        public void ShowResult(string narration, int fileCount)
        {
            ResultLabel.Text = $"{narration}\n\n📦 {fileCount} fichiers générés.";
        }
    }
}
