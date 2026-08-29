// Moto.Editor/Helpers/DragZoneHelper.cs
using Microsoft.Maui.Devices;

namespace Moto.Editor.Helpers;

/// <summary>
/// Calcule la zone de drag en tenant compte de la densité de pixels.
/// - Windows/MAUI : coordonnées en device-independent pixels (DIP)
/// - AppWindow (WinUI3) : attend des pixels physiques → multiplication par Density
/// - Avalonia/Linux : idem, mais via RenderScaling
/// </summary>
public static class DragZoneHelper
{
    /// <summary>
    /// Densité courante (1.0 = 100%, 1.25 = 125%, 1.5 = 150%).
    /// Sur Windows/MAUI : DeviceDisplay.MainDisplayInfo.Density
    /// Sur Avalonia/Linux : à remplacer par VisualRoot.RenderScaling
    /// </summary>
    public static double CurrentDensity
    {
        get
        {
#if WINDOWS
            return DeviceDisplay.MainDisplayInfo.Density;
#elif LINUX || AVALONIA
            // Fallback Avalonia : utiliser le RenderScaling de la fenêtre
            // Exemple : return Avalonia.Application.Current.MainWindow.RenderScaling;
            return 1.0; // Placeholder
#else
            return DeviceDisplay.MainDisplayInfo.Density;
#endif
        }
    }

    /// <summary>
    /// Convertit une taille DIP en pixels physiques.
    /// À utiliser pour toute zone passée à SetDragRegionForCustomTitleBar.
    /// </summary>
    public static int DipToPhysical(double dip)
        => (int)Math.Round(dip * CurrentDensity);

    /// <summary>
    /// Vérifie que la densité est cohérente entre MAUI et WinUI3.
    /// À appeler au démarrage pour logger un warning si mismatch (écran 125/150%).
    /// </summary>
    public static bool ValidateDensityConsistency(double winUiScaleFactor)
    {
        var delta = Math.Abs(CurrentDensity - winUiScaleFactor);
        return delta < 0.01; // tolérance flottant
    }
}
