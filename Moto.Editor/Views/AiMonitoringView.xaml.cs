using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.Logging;
using Moto.Core.Monitoring;

namespace Moto.Editor.Views;

/// <summary>
/// Item 64 — Affiche le badge CircuitOpen (24h) et déclenche la collecte de logs.
/// Couleurs d'état appliquées en code-behind pour garantir la compilation
/// indépendamment des clés exactes de MotoTheme (à remplacer par les brushes
/// MotoSuccessBrush / MotoDangerBrush si présents).
/// </summary>
public partial class AiMonitoringView : ContentView
{
    private readonly CircuitBreakerStateService _circuit;
    private readonly StructuredLogCollector _log;

    public AiMonitoringView(CircuitBreakerStateService circuit, StructuredLogCollector log)
    {
        InitializeComponent();
        _circuit = circuit;
        _log = log;

        _circuit.StateChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
        Refresh();
    }

    private void Refresh()
    {
        int count = _circuit.OpenCountLast24h;
        CircuitCountLabel.Text = count.ToString();
        StatusLabel.Text = $"Circuit : {_circuit.State} · Fallbacks : {_circuit.FallbackCount}";

        // Alerte visuelle si proche du seuil (>=3)
        bool alert = count >= 2 || _circuit.State == "Open";
        CircuitBadge.Background = alert
            ? new SolidColorBrush(Colors.OrangeRed)
            : new SolidColorBrush(Colors.SeaGreen);
        CircuitCountLabel.TextColor = Colors.White;
    }

    private async void OnCollectLogsClicked(object? sender, EventArgs e)
    {
        CollectLogsButton.IsEnabled = false;
        CollectLogsButton.Text = "⏳ Collecte...";
        try
        {
            string archive = await _log.CreateArchiveAsync();
            _log.Info("AiMonitoringView", "Logs collectés", new { archive });
            StatusLabel.Text = $"✅ Archive : {System.IO.Path.GetFileName(archive)}";
        }
        catch (Exception ex)
        {
            _log.Error("AiMonitoringView", "Échec collecte logs", new { ex.Message });
            StatusLabel.Text = $"❌ Échec : {ex.Message}";
        }
        finally
        {
            CollectLogsButton.IsEnabled = true;
            CollectLogsButton.Text = "📦 Collecter les logs";
        }
    }
}
