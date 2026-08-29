using Moto.Core.AI.Internal;
using Moto.Core.Settings;
using System.Diagnostics;

namespace Moto.Editor.Views;

/// <summary>
/// Vue de gestion des modèles embarqués.
/// Permet de sélectionner le modèle, le tier et de télécharger.
/// </summary>
public partial class ModelManagerView : ContentView
{
    private readonly SettingsEngine _settings;
    private readonly ModelDownloaderService _downloader;

    public ModelManagerView(
        SettingsEngine settings,
        ModelDownloaderService downloader)
    {
        InitializeComponent();
        _settings = settings;
        _downloader = downloader;

        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        var model = _settings.GetString("ai.embedded.modelChoice", "phi-3-mini");
        var tier = _settings.GetString("ai.embedded.forcedTier", "auto");

        ModelPicker.SelectedIndex = model switch
        {
            "phi-3-mini" => 0,
            "qwen2.5-1.5b" => 1,
            "llama-3.2-1b" => 2,
            "llama-3.2-3b" => 3,
            _ => 0
        };

        TierPicker.SelectedIndex = tier switch
        {
            "lite" => 0,
            "standard" => 1,
            "full" => 2,
            _ => 1
        };
    }

    private void OnModelSelected(object? sender, EventArgs e)
    {
        var modelId = ModelPicker.SelectedIndex switch
        {
            0 => "phi-3-mini",
            1 => "qwen2.5-1.5b",
            2 => "llama-3.2-1b",
            3 => "llama-3.2-3b",
            _ => "phi-3-mini"
        };

        _settings.Set("ai.embedded.modelChoice", modelId);
        Debug.WriteLine($"[ModelManagerView] Model selected: {modelId}");
    }

    private void OnTierSelected(object? sender, EventArgs e)
    {
        var tier = TierPicker.SelectedIndex switch
        {
            0 => "lite",
            1 => "standard",
            2 => "full",
            _ => "auto"
        };

        _settings.Set("ai.embedded.forcedTier", tier);
        Debug.WriteLine($"[ModelManagerView] Tier selected: {tier}");
    }

    private async void OnDownloadClicked(object? sender, EventArgs e)
    {
        DownloadButton.IsEnabled = false;
        DownloadStatusLabel.Text = "Téléchargement en cours...";

        try
        {
            var modelId = ModelPicker.SelectedIndex switch
            {
                0 => "phi-3-mini",
                1 => "qwen2.5-1.5b",
                2 => "llama-3.2-1b",
                3 => "llama-3.2-3b",
                _ => "phi-3-mini"
            };

            await _downloader.DownloadModelAsync(
                modelId,
                progress: p =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        DownloadProgressBar.Progress = p;
                        DownloadStatusLabel.Text = $"Téléchargement: {p * 100:0}%";
                    });
                });

            DownloadStatusLabel.Text = "✅ Téléchargement terminé !";
            DownloadProgressBar.Progress = 1.0;
        }
        catch (Exception ex)
        {
            DownloadStatusLabel.Text = $"❌ Erreur: {ex.Message}";
            Debug.WriteLine($"[ModelManagerView] Download failed: {ex.Message}");
        }
        finally
        {
            DownloadButton.IsEnabled = true;
        }
    }
}
