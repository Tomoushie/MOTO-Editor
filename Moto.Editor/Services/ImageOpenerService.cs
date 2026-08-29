using Moto.Editor.Models;

namespace Moto.Editor.Services;

/// <summary>
/// Route l'ouverture d'image : onglet interne → fenêtre externe thémée.
/// Multiplateforme : WinUI 3 natif, fallback MAUI ailleurs.
/// </summary>
public sealed class ImageOpenerService
{
    /// <summary>Ouvre l'image dans une fenêtre externe thémée Moto.</summary>
    public async Task OpenExternalAsync(ImageDocument doc)
    {
#if WINDOWS
        Moto.Editor.Platforms.Windows.ExternalImageWindow.Open(doc.Path, doc.FileName);
        await Task.CompletedTask;
#else
        // Fallback macOS/Linux : alerte + ouverture via l'OS (thème non garanti)
        try
        {
            await Launcher.Default.OpenAsync(new OpenFileRequest(doc.FileName,
                new ReadOnlyFile(doc.Path)));
        }
        catch
        {
            await Application.Current!.Windows[0].Page!.DisplayAlert(
                "Fenêtre externe",
                "La fenêtre externe thémée est disponible sur Windows. Utilisation du visualiseur système.",
                "OK");
        }
#endif
    }
}
