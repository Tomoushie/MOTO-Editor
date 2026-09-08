// Moto.Editor/Pages/BeginnerAssistantPage.xaml.cs
// Point d'entrée UI pour BeginnerAssistant (voir Moto.Core/Moto.AI/Beginner),
// désormais branché sur l'orchestrateur XENO-SSS∞ réel via IOrchestratorClient.
// ★ AJOUT (08/09, Tom) : affiche la requête envoyée, la réponse (diff proposé),
// et n'écrit sur le vrai fichier qu'après un clic explicite sur "Appliquer" —
// jamais automatiquement.
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Moto.Editor.AI.Beginner;

namespace Moto.Editor.Pages
{
    public partial class BeginnerAssistantPage : ContentPage
    {
        private readonly BeginnerAssistant _assistant;
        private BeginnerResult? _lastResult;

        public BeginnerAssistantPage()
        {
            InitializeComponent();

            var services = Handler?.MauiContext?.Services
                ?? Application.Current?.Handler?.MauiContext?.Services;

            _assistant = services?.GetService<BeginnerAssistant>()
                ?? throw new InvalidOperationException(
                    "BeginnerAssistant introuvable dans le conteneur DI (services indisponibles à la construction de la page).");

            ActionPicker.SelectedIndex = 0;
        }

        private async void OnSendClicked(object sender, EventArgs e)
        {
            var filePath = FilePathEntry.Text?.Trim() ?? string.Empty;
            var actionName = ActionPicker.SelectedItem as string ?? "FixFile";

            if (string.IsNullOrWhiteSpace(filePath) &&
                actionName != nameof(BeginnerAction.Teach))
            {
                await DisplayAlert("Beginner Assistant", "Indique le chemin du fichier.", "OK");
                return;
            }

            var content = ContentEditor.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content) && File.Exists(filePath))
            {
                try
                {
                    content = await File.ReadAllTextAsync(filePath);
                }
                catch (Exception ex)
                {
                    content = string.Empty;
                    RequestSummaryLabel.Text = $"(lecture du fichier impossible : {ex.Message})";
                }
            }

            var action = Enum.Parse<BeginnerAction>(actionName);
            var request = new BeginnerRequest
            {
                Action = action,
                FilePath = filePath,
                Content = content,
                CompilerErrors = CompilerErrorsEntry.Text?.Trim() ?? string.Empty,
                Topic = TopicEntry.Text?.Trim() ?? string.Empty
            };

            RequestSummaryLabel.Text =
                $"Action : {action}\n" +
                $"Fichier : {(string.IsNullOrWhiteSpace(filePath) ? "(aucun)" : filePath)}\n" +
                $"Contenu envoyé : {content.Length} caractère(s)";

            ResultLabel.Text = "L'assistant réfléchit (appel réel à l'orchestrateur)...";
            DiffSection.IsVisible = false;
            ApplyButton.IsEnabled = false;

            _lastResult = await _assistant.ExecuteAsync(request);

            ResultLabel.Text = $"{_lastResult.Title}\n\n{_lastResult.Explanation}";
            SuggestionsLabel.Text = _lastResult.Suggestions.Count > 0
                ? "Suggestions : " + string.Join(" · ", _lastResult.Suggestions)
                : string.Empty;

            var patch = _lastResult.Patches.FirstOrDefault();
            if (_lastResult.RequiresUserConfirmation && patch != null)
            {
                OriginalEditor.Text = patch.OriginalContent;
                ProposedEditor.Text = patch.ProposedContent;
                DiffSection.IsVisible = true;
                ApplyButton.IsEnabled = true;
            }
        }

        private async void OnApplyClicked(object sender, EventArgs e)
        {
            var patch = _lastResult?.Patches.FirstOrDefault();
            if (patch == null)
            {
                await DisplayAlert("Beginner Assistant", "Aucun patch à appliquer.", "OK");
                return;
            }

            var confirmed = await DisplayAlert(
                "Beginner Assistant",
                $"Écrire réellement sur disque :\n{patch.FilePath}\n\nCette action remplace le contenu actuel du fichier. Continuer ?",
                "Oui, écrire",
                "Annuler");

            if (!confirmed)
            {
                return;
            }

            try
            {
                await File.WriteAllTextAsync(patch.FilePath, patch.ProposedContent);
                await DisplayAlert("Beginner Assistant", "Fichier écrit avec succès.", "OK");

                // Empêche une double-application accidentelle du même patch.
                ApplyButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Beginner Assistant", $"Échec de l'écriture : {ex.Message}", "OK");
            }
        }
    }
}
