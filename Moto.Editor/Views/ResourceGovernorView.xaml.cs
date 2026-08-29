using Microsoft.Maui.Controls;
using Moto.Editor.Services; // Pour InferenceHostClient et ToastNotificationService
using Moto.Core.Performance; // Pour MetricsCollectorService

namespace Moto.Editor.Views;

public partial class ResourceGovernorView : ContentView
{
    public ResourceGovernorView()
    {
        InitializeComponent();
        UpdateCircuitBadge();

        // Abonnement aux mises à jour des métriques (toutes les 5s par exemple)
        MetricsCollectorService.MetricsUpdated += (s, e) => UpdateCircuitBadge();
    }

    private void UpdateCircuitBadge()
    {
        int count = MetricsCollectorService.GetCircuitOpenCount24h();
        CircuitCountLabel.Text = count.ToString();

        // Alerte visuelle si le seuil est proche (Règle du Runbook : >= 3)
        if (count >= 2)
        {
            CircuitCountLabel.TextColor = Colors.OrangeRed;
        }
        else
        {
            CircuitCountLabel.TextColor = (Color)Application.Current.Resources["MotoSuccessBrush"];
        }
    }

    private async void OnCollectLogsClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn)
        {
            btn.IsEnabled = false;
            btn.Text = "⏳ Collecte...";
        }

        try
        {
            // Appel au named pipe de l'InferenceHost
            var archivePath = await InferenceHostClient.CollectLogsAsync();
            await ToastNotificationService.ShowAsync("Logs collectés", $"Archive prête : {System.IO.Path.GetFileName(archivePath)}");
        }
        catch (Exception ex)
        {
            await ToastNotificationService.ShowAsync("Erreur", $"Échec de la collecte : {ex.Message}");
        }
        finally
        {
            if (sender is Button btn)
            {
                btn.IsEnabled = true;
                btn.Text = "📦 Collecter les logs";
            }
        }
    }
}
