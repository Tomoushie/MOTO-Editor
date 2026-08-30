#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Moto.Editor.Platforms.Windows;

/// <summary>
/// Fenêtre image externe (type Visionneuse Windows) avec thème Moto appliqué.
/// Crée une AppWindow secondaire rattachée au processus.
/// </summary>
public static class ExternalImageWindow
{
    /// <summary>Ouvre l'image dans une fenêtre externe thémée Moto.</summary>
    public static void Open(string imagePath, string title)
    {
        try
        {
            var window = new Window
            {
                Title = $"🖼️ {title} — MOTO Editor"
            };

            // ★ Application du thème Moto : on récupère les couleurs du dictionnaire MAUI
            var bg = ResolveMauiColor("MotoBackgroundColor", "#1E1E1E");
            var fg = ResolveMauiColor("MotoTextColor", "#FFFFFF");

            var image = new Image
            {
                Source = new BitmapImage(new Uri(imagePath)),
                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform
            };

            var grid = new Grid { Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(bg) };
            grid.Children.Add(image);

            // Barre de titre stylisée Moto
            var titleBar = new TextBlock
            {
                Text = System.IO.Path.GetFileName(imagePath),
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(fg),
                FontSize = 13,
                Margin = new Thickness(12, 8, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            grid.Children.Add(titleBar);

            window.Content = grid;
            window.ExtendsContentIntoTitleBar = false;

            // Taille initiale basée sur l'image (plafonnée)
            window.Activate();
            window.AppWindow.Resize(new Windows.Graphics.SizeInt32(900, 700));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExternalImageWindow] {ex.Message}");
        }
    }

    private static global::Windows.UI.Color ResolveMauiColor(string key, string fallback)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var res) == true
            && res is Microsoft.Maui.Graphics.Color mauiColor)
        {
            return global::Windows.UI.Color.FromArgb(
                (byte)(mauiColor.Alpha * 255),
                (byte)(mauiColor.Red * 255),
                (byte)(mauiColor.Green * 255),
                (byte)(mauiColor.Blue * 255));
        }
        return ParseHex(fallback);
    }

    private static global::Windows.UI.Color ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        return global::Windows.UI.Color.FromArgb(255,
            Convert.ToByte(hex.Substring(0, 2), 16),
            Convert.ToByte(hex.Substring(2, 2), 16),
            Convert.ToByte(hex.Substring(4, 2), 16));
    }
}
#endif
