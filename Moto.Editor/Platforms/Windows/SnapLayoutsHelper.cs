// Moto.Editor/Platforms/Windows/SnapLayoutsHelper.cs
#if WINDOWS
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using System;
using Moto.Editor.Helpers; // DragZoneHelper

namespace Moto.Editor.Platforms.Windows;

/// <summary>
/// Configure les zones de drag et les boutons de fenêtre pour que Windows 11
/// déclenche automatiquement le flyout Snap Layouts au survol du bouton Maximiser.
/// ★ CORRECTION (30/08) : réintégré après avoir été mis de côté (Border MAUI vs
/// FrameworkElement WinUI, résolu dans MainPage.OnPageLoaded via Handler.PlatformView)
/// — et son API interne mise à jour : AppWindowTitleBar.SetDragRegionForCustomTitleBar
/// / SetNonClientInputRegion n'existent plus sur cette version de WindowsAppSDK (1.8) ;
/// remplacées par InputNonClientPointerSource.SetRegionRects(NonClientRegionKind, ...),
/// l'API courante depuis WindowsAppSDK 1.4 (vérifié sur learn.microsoft.com).
/// </summary>
public static class SnapLayoutsHelper
{
    public static void ConfigureSnapLayouts(Microsoft.UI.Xaml.Window window,
        FrameworkElement btnMin, FrameworkElement btnMax, FrameworkElement btnClose,
        FrameworkElement dragZone)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var nonClientSource = InputNonClientPointerSource.GetForWindowId(windowId);

        // 1) Titre étendu
        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;

        // 2) Couleurs de la title bar cohérentes avec MotoTheme
        appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        appWindow.TitleBar.ButtonHoverBackgroundColor = ToColor("#2A2C31"); // BgHover
        appWindow.TitleBar.ButtonForegroundColor = ToColor("#E5E7EB");
        appWindow.TitleBar.ButtonHoverForegroundColor = ToColor("#D97757"); // Accent

        // 3) Zone de drag : UNIQUEMENT la zone centrale
        SetRegion(nonClientSource, NonClientRegionKind.Caption, dragZone);

        // 4) Zones des boutons : interactives ET reconnues par Windows (survol
        // Maximiser -> flyout Snap Layouts) grâce au bon NonClientRegionKind.
        SetRegion(nonClientSource, NonClientRegionKind.Minimize, btnMin);
        SetRegion(nonClientSource, NonClientRegionKind.Maximize, btnMax);
        SetRegion(nonClientSource, NonClientRegionKind.Close, btnClose);
    }

    private static void SetRegion(InputNonClientPointerSource source, NonClientRegionKind kind, FrameworkElement element)
    {
        // DENSITÉ UNIFIÉE
        var scale = DragZoneHelper.CurrentDensity;

        void Appliquer()
        {
            var rect = GetScaledRect(element, scale);
            source.SetRegionRects(kind, new[] { RectInt32From(rect) });
        }

        element.Loaded += (_, _) => Appliquer();
        element.SizeChanged += (_, _) => Appliquer();
    }

    private static (int X, int Y, int W, int H) GetScaledRect(FrameworkElement el, double scale)
    {
        var transform = el.TransformToVisual(el.XamlRoot.Content);
        var pos = transform.TransformPoint(new global::Windows.Foundation.Point(0, 0));

        // Utilisation directe des méthodes de conversion de DragZoneHelper pour éviter les doubles multiplications
        return (
            DragZoneHelper.DipToPhysical(pos.X),
            DragZoneHelper.DipToPhysical(pos.Y),
            DragZoneHelper.DipToPhysical(el.ActualSize.X),
            DragZoneHelper.DipToPhysical(el.ActualSize.Y)
        );
    }

    private static global::Windows.Graphics.RectInt32 RectInt32From((int X, int Y, int W, int H) r)
        => new(r.X, r.Y, r.W, r.H);

    private static global::Windows.UI.Color ToColor(string hex)
    {
        hex = hex.TrimStart('#');
        return global::Windows.UI.Color.FromArgb(255,
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }
}
#endif
