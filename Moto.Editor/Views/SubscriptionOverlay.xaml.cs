using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Moto.Editor.Views;

public sealed partial class SubscriptionOverlay : UserControl
{
    public event Action? OnSubscribe;
    public event Action? OnCancel;

    public SubscriptionOverlay()
    {
        this.InitializeComponent();
    }

    private void OnSubscribeClicked(object sender, RoutedEventArgs e)
    {
        // TODO: lancer Stripe Checkout Session via StripePaymentService
        OnSubscribe?.Invoke();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        OnCancel?.Invoke();
    }
}
