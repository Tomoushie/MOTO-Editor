// Moto.Editor/Controls/InfoOverlay.xaml.cs
using System;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Moto.Core.Updates;

namespace Moto.Editor.Controls
{
    public partial class InfoOverlay : ContentView
    {
        private UpdateManager? _updateManager;
        private readonly string _version;

        public event Action? UpdateCheckRequested;

        public InfoOverlay(string version = "1.0.0")
        {
            InitializeComponent();
            _version = version;
            VersionLabel.Text = $"Version {version}";
            BuildLabel.Text = $"Build {DateTime.UtcNow:yyyyMMdd-HHmm}";
        }

        public void SetUpdateManager(UpdateManager manager)
        {
            _updateManager = manager;
        }

        public void Show() => IsVisible = true;
        public void Hide() => IsVisible = false;

        private void OnCloseClicked(object? sender, EventArgs e) => Hide();

        private async void OnCheckUpdateClicked(object? sender, EventArgs e)
        {
            CheckUpdateBtn.IsEnabled = false;
            CheckUpdateBtn.Text = "Vérification…";
            UpdateCheckRequested?.Invoke();

            if (_updateManager != null)
            {
                var update = await _updateManager.CheckForUpdateAsync();
                if (update == null)
                {
                    await Application.Current!.MainPage!.DisplayAlert(
                        "Mise à jour",
                        "Vous utilisez la dernière version.",
                        "OK");
                }
            }

            CheckUpdateBtn.IsEnabled = true;
            CheckUpdateBtn.Text = "🔄 Rechercher MAJ";
        }

        private async void OnChangelogClicked(object? sender, EventArgs e)
        {
            var changelog = @"MOTO Editor v1.0.0 (2026-01-15)

🎉 Version initiale

✨ Fonctionnalités :
• Éditeur de code avec coloration syntaxique
• IA locale via Ollama (Cortex, Neural, Workspace)
• Terminal intégré + Sandbox
• Collaboration temps réel (CRDT)
• Debugger DAP + LSP Roslyn
• Système de plugins + Marketplace
• Multi-fenêtres + Live Preview
• 4 langues supportées (FR, EN, RU, ZH)

🔧 Technique :
• Architecture MAUI + WinUI 3
• 100% local, sans cloud
• Pipeline XENO-SSS∞ v5";

            await Application.Current!.MainPage!.DisplayAlert("Changelog", changelog, "Fermer");
        }

        private async void OnEmailClicked(object? sender, EventArgs e)
        {
            try
            {
                await Launcher.Default.OpenAsync(new Uri("mailto:nowaktombe@protonmail.com?subject=MOTO%20Editor"));
            }
            catch
            {
                await Clipboard.Default.SetTextAsync("nowaktombe@protonmail.com");
                await Application.Current!.MainPage!.DisplayAlert(
                    "Email copié", "L'adresse a été copiée.", "OK");
            }
        }
    }
}
