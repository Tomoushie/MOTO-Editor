using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Moto.Core.Refactor; // Ou le namespace XENO approprié

namespace Moto.Editor.Controls;

public sealed partial class XenoFeedbackControl : UserControl
{
    public string FindingId { get; set; } = "";
    public event Action<string, FeedbackType, string?>? OnFeedbackSubmitted;

    public XenoFeedbackControl()
    {
        this.InitializeComponent();
    }

    private void OnLikeClicked(object sender, RoutedEventArgs e) => Submit(FeedbackType.Like);
    private void OnDislikeClicked(object sender, RoutedEventArgs e) => Submit(FeedbackType.Dislike);

    private void OnEditClicked(object sender, RoutedEventArgs e)
    {
        // Ouvrir un petit flyout ou dialog pour la variante
        Submit(FeedbackType.Edit, "variante_utilisateur");
    }

    private void Submit(FeedbackType type, string? variant = null)
    {
        OnFeedbackSubmitted?.Invoke(FindingId, type, variant);
        ScoreText.Text = "Merci !";
        BtnLike.IsEnabled = false;
        BtnDislike.IsEnabled = false;
        BtnEdit.IsEnabled = false;
    }
}
