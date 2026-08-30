// Moto.Editor/Platforms/Windows/SnapLayoutsHelper.cs
#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using Windows.Graphics;
using System;
using Moto.Editor.Helpers; // Ajout pour DragZoneHelper

namespace Moto.Editor.Platforms.Windows;

/// <summary>
/// Configure les zones de drag et les boutons de fenêtre pour que Windows 11
/// déclenche automatiquement le flyout Snap Layouts au survol du bouton Maximiser.
/// </summary>
public static class SnapLayoutsHelper
{
    public static void ConfigureSnapLayouts(Microsoft.UI.Xaml.Window window,
        FrameworkElement btnMin, FrameworkElement btnMax, FrameworkElement btnClose,
        FrameworkElement dragZone)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        // 1) Titre étendu
        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;

        // 2) Couleurs de la title bar cohérentes avec MotoTheme
        appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        appWindow.TitleBar.ButtonHoverBackgroundColor = ToColor("#2A2C31"); // BgHover
        appWindow.TitleBar.ButtonForegroundColor = ToColor("#E5E7EB");
        appWindow.TitleBar.ButtonHoverForegroundColor = ToColor("#D97757"); // Accent

        // 3) Zones de drag : UNIQUEMENT la zone centrale
        SetDragRegion(appWindow, dragZone);

        // 4) Zones interactives pour les boutons (non-drag, hit-testable)
        SetInteractiveRegion(appWindow, btnMin);
        SetInteractiveRegion(appWindow, btnMax);
        SetInteractiveRegion(appWindow, btnClose);
    }

    private static void SetDragRegion(AppWindow appWindow, FrameworkElement element)
    {
        // DENSITÉ UNIFIÉE
        var scale = DragZoneHelper.CurrentDensity;

        element.Loaded += (_, _) =>
        {
            var rect = GetScaledRect(element, scale);
            var region = RectInt32.From(rect);
            appWindow.TitleBar.SetDragRegionForCustomTitleBar(region);
        };

        element.SizeChanged += (_, _) =>
        {
            var rect = GetScaledRect(element, scale);
            var region = RectInt32.From(rect);
            appWindow.TitleBar.SetDragRegionForCustomTitleBar(region);
        };
    }

    private static void SetInteractiveRegion(AppWindow appWindow, FrameworkElement element)
    {
        element.Loaded += (_, _) =>
        {
            // DENSITÉ UNIFIÉE
            var scale = DragZoneHelper.CurrentDensity;
            var rect = GetScaledRect(element, scale);
            var region = RectInt32.From(rect);
            appWindow.TitleBar.SetNonClientInputRegion(region);
        };
    }

    private static (int X, int Y, int W, int H) GetScaledRect(FrameworkElement el, double scale)
    {
        var transform = el.TransformToVisual(el.XamlRoot.Content);
        var pos = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

        // Utilisation directe des méthodes de conversion de DragZoneHelper pour éviter les doubles multiplications
        return (
            DragZoneHelper.DipToPhysical(pos.X),
            DragZoneHelper.DipToPhysical(pos.Y),
            DragZoneHelper.DipToPhysical(el.ActualSize.X),
            DragZoneHelper.DipToPhysical(el.ActualSize.Y)
        );
    }

    private static global::Windows.UI.Color ToColor(string hex)
    {
        hex = hex.TrimStart('#');
        return global::Windows.UI.Color.FromArgb(255,
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }
}

/// <summary>Helper pour convertir (X,Y,W,H) en RectInt32.</summary>
internal readonly struct RectInt32
{
    public static global::Windows.Graphics.RectInt32 From((int X, int Y, int W, int H) r)
        => new(r.X, r.Y, r.W, r.H);
}
#endif
