using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Moto.Core.Refactor;

namespace Moto.Editor.Views;

public sealed partial class RefactorPanel : UserControl
{
    private List<RefactorSuggestion> _suggestions = new();
    private readonly RefactorEngine _engine;

    public RefactorPanel()
    {
        this.InitializeComponent();
        _engine = new RefactorEngine();
    }

    public void LoadSuggestions(List<RefactorSuggestion> suggestions)
    {
        _suggestions = suggestions;
        SuggestionsList.ItemsSource = _suggestions;
        SuggestionCountText.Text = $"{_suggestions.Count} suggestion{(_suggestions.Count > 1 ? "s" : "")}";
    }

    private void OnLikeClicked(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var suggestionId = button?.Tag as string;
        if (suggestionId != null)
        {
            _engine.RecordFeedback(suggestionId, FeedbackType.Like);
            UpdateSuggestionScore(suggestionId, 0.1);
        }
    }

    private void OnDislikeClicked(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var suggestionId = button?.Tag as string;
        if (suggestionId != null)
        {
            _engine.RecordFeedback(suggestionId, FeedbackType.Dislike);
            UpdateSuggestionScore(suggestionId, -0.1);
        }
    }

    private void OnEditClicked(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var suggestionId = button?.Tag as string;
        // TODO: Ouvrir un éditeur de variante
        _engine.RecordFeedback(suggestionId, FeedbackType.Edit, "user_variant");
    }

    private async void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var suggestionId = button?.Tag as string;
        var suggestion = _suggestions.FirstOrDefault(s => s.Id == suggestionId);

        if (suggestion != null)
        {
            // TODO: Appliquer la correction au fichier courant
            // var currentFile = GetCurrentFile();
            // var refactored = await _engine.ApplyFixAsync(currentFile.Content, suggestion);
            // await SaveFileAsync(currentFile.Path, refactored);

            _suggestions.Remove(suggestion);
            SuggestionsList.ItemsSource = null;
            SuggestionsList.ItemsSource = _suggestions;
            SuggestionCountText.Text = $"{_suggestions.Count} suggestion{(_suggestions.Count > 1 ? "s" : "")}";
        }
    }

    private void UpdateSuggestionScore(string suggestionId, double delta)
    {
        var suggestion = _suggestions.FirstOrDefault(s => s.Id == suggestionId);
        if (suggestion != null)
        {
            suggestion.Score = Math.Clamp(suggestion.Score + delta, 0, 1);
            SuggestionsList.ItemsSource = null;
            SuggestionsList.ItemsSource = _suggestions;
        }
    }
}
