// Moto.Editor/Pages/MotoAiPage.xaml.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Moto.Core.AI.Internal.Models;
using Moto.Editor.Services;

namespace Moto.Editor.Pages
{
    /// <summary>
    /// Écran pour utiliser MOTO AI directement dans l'éditeur.
    /// ★ MODIFIÉ (27/09, option C choisie par Tom) : ContentView affichée DANS la fenêtre (Views/ScreenHostView), plus une
    /// page poussée par-dessus MainPage. Le nom de classe « …Page » est gardé tel quel (référencé ailleurs).
    /// </summary>
    public partial class MotoAiPage : ContentView
    {
        private readonly MotoAiService _service = new MotoAiService();
        private AiResponse _lastResponse;

        public MotoAiPage()
        {
            InitializeComponent();

            ModePicker.SelectedIndex = 0;
        }

        // ★ AJOUT (27/09) : une ContentView n'a pas de DisplayAlert — mêmes appels qu'avant, affichés par la page de la fenêtre.
        private Task DisplayAlert(string title, string message, string cancel) =>
            Views.ScreenHostView.AlertAsync(this, title, message, cancel);

        private Task<bool> DisplayAlert(string title, string message, string accept, string cancel) =>
            Views.ScreenHostView.ConfirmAsync(this, title, message, accept, cancel);

        private async void OnRunClicked(object sender, EventArgs e)
        {
            var workspace = WorkspaceEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(workspace))
            {
                await DisplayAlert("MOTO AI", "Indique le chemin du workspace.", "OK");
                return;
            }

            var request = new AiRequest
            {
                WorkspacePath = workspace,
                UserText = InstructionEntry.Text?.Trim() ?? string.Empty,
                Mode = ModePicker.SelectedIndex == 0
                    ? AiMode.Beginner
                    : AiMode.Expert
            };

            ResultLabel.Text = "MOTO AI réfléchit...";

            _lastResponse = await _service.ExecuteAsync(request);

            ResultLabel.Text =
                $"{_lastResponse.Title}\n\n" +
                $"{_lastResponse.Summary}\n\n" +
                $"{_lastResponse.Explanation}";

            StepsView.ItemsSource = _lastResponse.Steps.ToList();
            ChangesView.ItemsSource = _lastResponse.FileChanges.ToList();
            SuggestionsView.ItemsSource = _lastResponse.Suggestions.ToList();
        }

        private async void OnApplyClicked(object sender, EventArgs e)
        {
            if (_lastResponse == null || _lastResponse.FileChanges.Count == 0)
            {
                await DisplayAlert("MOTO AI", "Aucun fichier à appliquer.", "OK");
                return;
            }

            var workspace = WorkspaceEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(workspace))
            {
                await DisplayAlert("MOTO AI", "Indique le chemin du workspace.", "OK");
                return;
            }

            var confirmed = await DisplayAlert(
                "MOTO AI",
                $"{_lastResponse.FileChanges.Count} fichier(s) seront créés ou proposés.\nContinuer ?",
                "Oui",
                "Non"
            );

            if (!confirmed)
            {
                return;
            }

            await _service.ApplyChangesAsync(_lastResponse.FileChanges, workspace);

            await DisplayAlert("MOTO AI", "Fichiers appliqués sans écraser les fichiers existants.", "OK");
        }
    }
}
