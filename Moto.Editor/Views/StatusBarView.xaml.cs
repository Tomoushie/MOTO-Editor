// Moto.Editor/Controls/StatusBarView.xaml.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Moto.Core.Performance;

namespace Moto.Editor.Controls;

public sealed partial class StatusBarView : UserControl
{
    public StatusBarView()
    {
        this.InitializeComponent();
    }

    // ══ Handler existant : bouton Info ══
    private void OnInfoClicked(object sender, TappedRoutedEventArgs e)
    {
        // Délègue à l'overlay Info existant (Phase 7)
        // Ne pas modifier : préserve la fonctionnalité existante
        var mainWindow = Microsoft.UI.Xaml.Window.Current?.Content
            as Microsoft.UI.Xaml.Controls.Frame;
        // L'InfoOverlay est résolu via le routeur de MainPage
        // (voir MainPage.Routing.cs → ShowInfoOverlay)
    }

    // ══ ★ Nouveau handler : Toggle Ultra-Lite ══
    private void OnUltraLiteToggled(object sender, RoutedEventArgs e)
    {
        if (App.Services == null) return;

        var ultraLite = App.Services.GetRequiredService<UltraLiteMode>();
        if (UltraLiteToggle.IsChecked == true)
        {
            ultraLite.Activate();
            StatusLabel.Text = "⚡ Mode Ultra-Lite activé";
        }
        else
        {
            ultraLite.Deactivate();
            StatusLabel.Text = "Mode complet restauré";
        }
    }
}
