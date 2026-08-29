// Moto.Editor/Views/ModelConsentDialog.xaml.cs
using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Moto.Core.AI.Embedded;

namespace Moto.Editor.Views;

public sealed partial class ModelConsentDialog : UserControl
{
    private readonly ModelTier _tier;
    private readonly ModelSpec _spec;
    private readonly ModelSecurityService _security;
    private readonly ModelDownloader _downloader;

    public event Action<ModelTier>? OnConsentGiven;
    public event Action? OnCancelled;

    public ModelConsentDialog(ModelTier tier)
    {
        this.InitializeComponent();
        _tier = tier;
        _spec = ModelTierConfig.Specs[tier];
        _security = App.Services.GetRequiredService<ModelSecurityService>();
        _downloader = App.Services.GetRequiredService<ModelDownloader>();

        ModelNameText.Text = _spec.Name;
        ModelDescText.Text = $"Tier {tier} · RAM min: {_spec.MinRamMB / 1024} GB";
        ModelSizeText.Text = $"{_spec.CompressedSizeGB:F1} GB";

        CheckDiskSpace();

        _downloader.ProgressChanged += p => DispatcherQueue.TryEnqueue(() =>
        {
            DownloadProgress.Value = p.Percent;
            ProgressText.Text = $"{p.Percent:F1}% ({FormatSize(p.BytesDownloaded)} / {FormatSize(p.TotalBytes)})";
        });
    }

    private void CheckDiskSpace()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))!);
            var freeGB = drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
            DiskSpaceText.Text = $"Espace disponible : {freeGB:F1} GB";

            if (freeGB < _spec.CompressedSizeGB * 1.5)
            {
                DiskSpaceWarning.Text = $"⚠️ Espace insuffisant ! {_spec.CompressedSizeGB:F1} GB requis, {freeGB:F1} GB disponible.";
                DiskSpaceWarning.Visibility = Visibility.Visible;
                DownloadBtn.IsEnabled = false;
            }
        }
        catch
        {
            DiskSpaceText.Text = "Espace disponible : impossible à déterminer";
        }
    }

    private async void OnDownloadClicked(object sender, RoutedEventArgs e)
    {
        DownloadBtn.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;

        try
        {
            var config = new EmbeddedLlmConfig
            {
                ModelFileName = _spec.FileName,
                DownloadUrl = _spec.DownloadUrl,
                ExpectedSizeBytes = (long)(_spec.CompressedSizeGB * 1024 * 1024 * 1024)
            };

            await _downloader.DownloadAsync(config);

            // Vérification checksum post-téléchargement
            ProgressText.Text = "🔍 Vérification d'intégrité...";
            // TODO: obtenir le hash attendu depuis un manifeste distant
            var integrity = await _security.VerifyIntegrityAsync(_spec.FileName, "expected_hash_placeholder");

            if (integrity.IsValid)
            {
                _security.RegisterVerifiedModel(_spec.FileName, integrity.ActualHash, _tier, config.ExpectedSizeBytes);
                ProgressText.Text = "✅ Modèle vérifié et prêt !";
                OnConsentGiven?.Invoke(_tier);
            }
            else
            {
                ProgressText.Text = $"❌ {integrity.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            ProgressText.Text = $"❌ Erreur : {ex.Message}";
        }
        finally
        {
            DownloadBtn.IsEnabled = true;
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e) => OnCancelled?.Invoke();

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / 1024.0 / 1024.0:F1} MB",
        _ => $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB"
    };
}
