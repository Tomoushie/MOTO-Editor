// Moto.Editor/Views/LivePreviewView.xaml.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Moto.Core.Preview;

namespace Moto.Editor.Views
{
    public partial class LivePreviewView : ContentView
    {
        private PreviewEngine? _previewEngine;
        private PreviewWebSocketServer? _wsServer;
        private System.Timers.Timer? _autoRefreshTimer;
        private string? _lastSourcePath;
        private string? _lastContent;
        private string? _lastLanguage;

        public LivePreviewView()
        {
            InitializeComponent();
        }

        public void SetPreviewEngine(PreviewEngine engine)
        {
            _previewEngine = engine;
            _previewEngine.PreviewUpdated += OnPreviewFileGenerated;
        }

        /// <summary>
        /// Configure le serveur WebSocket pour le live-reload.
        /// </summary>
        public void SetWebSocketServer(PreviewWebSocketServer server)
        {
            _wsServer = server;
            _ = _wsServer.StartAsync();
            _wsServer.ClientCountChanged += count => MainThread.BeginInvokeOnMainThread(() =>
                FileLabel.Text = $"{System.IO.Path.GetFileName(_lastSourcePath ?? "")} · 👥 {count} client(s)");
        }

        /// <summary>
        /// Démarre la prévisualisation d'un fichier.
        /// </summary>
        public async Task StartPreviewAsync(string sourcePath, string content, string language)
        {
            _lastSourcePath = sourcePath;
            _lastContent = content;
            _lastLanguage = language;

            FileLabel.Text = System.IO.Path.GetFileName(sourcePath);
            await RenderPreviewAsync();
            StartAutoRefresh();
        }

        /// <summary>
        /// Met à jour le preview quand le contenu change (live).
        /// </summary>
        public async Task UpdatePreviewAsync(string content)
        {
            _lastContent = content;
            await RenderPreviewAsync();
        }

        public void StopPreview()
        {
            _autoRefreshTimer?.Stop();
            _autoRefreshTimer?.Dispose();
            _autoRefreshTimer = null;
        }

        private async Task RenderPreviewAsync()
        {
            if (_previewEngine == null || _lastSourcePath == null || _lastContent == null)
                return;

            var result = await _previewEngine.GeneratePreviewAsync(new PreviewRequest
            {
                SourcePath = _lastSourcePath,
                Content = _lastContent,
                Language = _lastLanguage ?? "html"
            });

            if (result.Success)
            {
                LoadPreviewFile(result.PreviewFilePath);

                // Notifie les clients WebSocket du changement
                if (_wsServer != null)
                    await _wsServer.NotifyRefreshAsync(_lastSourcePath);
            }
        }

        private void OnPreviewFileGenerated(string path)
        {
            MainThread.BeginInvokeOnMainThread(() => LoadPreviewFile(path));
        }

        private void LoadPreviewFile(string path)
        {
            try
            {
                // WebView MAUI : charge le fichier local
                PreviewBrowser.Source = new UrlWebViewSource
                {
                    Url = new Uri(path).AbsoluteUri
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Preview] Erreur chargement : {ex.Message}");
            }
        }

        private void StartAutoRefresh()
        {
            _autoRefreshTimer?.Stop();
            _autoRefreshTimer?.Dispose();
            _autoRefreshTimer = new System.Timers.Timer(800) { AutoReset = true };
            _autoRefreshTimer.Elapsed += async (s, e) =>
            {
                if (_lastContent != null)
                    await RenderPreviewAsync();
            };
            _autoRefreshTimer.Start();
        }

        private async void OnRefreshClicked(object? sender, EventArgs e)
        {
            await RenderPreviewAsync();
        }

        private async void OnOpenExternalClicked(object? sender, EventArgs e)
        {
            if (_lastSourcePath == null) return;
            try
            {
                var result = await _previewEngine!.GeneratePreviewAsync(new PreviewRequest
                {
                    SourcePath = _lastSourcePath,
                    Content = _lastContent ?? "",
                    Language = _lastLanguage ?? "html"
                });
                if (result.Success)
                    await Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync(new Uri(result.PreviewFilePath));
            }
            catch { }
        }

        private void OnCloseClicked(object? sender, EventArgs e)
        {
            StopPreview();
            IsVisible = false;
        }
    }
}
