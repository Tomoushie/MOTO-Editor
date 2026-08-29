using Moto.Editor.Models;

namespace Moto.Editor.Views;

/// <summary>
/// Visualiseur d'image en onglet. Le bouton ⛶ ouvre une fenêtre externe thémée.
/// </summary>
public partial class ImageViewerView : ContentView
{
    private ImageDocument? _document;
    private double _zoom = 1.0;

    public event Action<ImageDocument>? OpenExternalRequested;

    public ImageViewerView() => InitializeComponent();

    public void LoadDocument(ImageDocument doc)
    {
        _document = doc;
        FileNameLabel.Text = doc.FileName;
        ImageDisplay.Source = ImageSource.FromFile(doc.Path);

        // Infos dimensions (lazy, non bloquant)
        _ = LoadImageInfoAsync(doc);
    }

    private async Task LoadImageInfoAsync(ImageDocument doc)
    {
        try
        {
            var info = new FileInfo(doc.Path);
            ImageInfoLabel.Text = $"{FormatSize(info.Length)}";
        }
        catch { /* non bloquant */ }
        await Task.CompletedTask;
    }

    private static string FormatSize(long bytes)
        => bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
        };

    /// <summary>★ Bouton ⛶ : même comportement que le plein écran de l'éditeur.</summary>
    private void OnOpenExternalClicked(object? sender, EventArgs e)
    {
        if (_document != null)
            OpenExternalRequested?.Invoke(_document);
    }

    private void OnZoomInClicked(object? sender, EventArgs e) => ApplyZoom(1.25);
    private void OnZoomOutClicked(object? sender, EventArgs e) => ApplyZoom(0.8);

    private void ApplyZoom(double factor)
    {
        _zoom = Math.Clamp(_zoom * factor, 0.1, 8.0);
        ImageDisplay.Scale = _zoom;
    }

    private void OnFitClicked(object? sender, EventArgs e)
    {
        _zoom = 1.0;
        ImageDisplay.Scale = 1.0;
        ImageDisplay.Aspect = Aspect.AspectFit;
    }
}
