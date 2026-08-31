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
    /// <summary>
    /// ★ AJOUT (30/08, 2e passe) : extrait de ConfigureSnapLayouts pour pouvoir être
    /// appelé TRÈS TÔT (App.xaml.cs, OnWindowsWindowCreated — dès window.Created),
    /// au lieu d'attendre MainPage.OnPageLoaded (qui ne s'exécute qu'après le
    /// premier rendu de la page, laissant le temps à Windows d'afficher/figer la
    /// barre de titre native bleue par défaut — repéré par Tom). Ne dépend d'aucun
    /// FrameworkElement MAUI (juste l'AppWindow), donc appelable immédiatement.
    /// </summary>
    public static void ApplyTitleBarColors(AppWindow appWindow)
    {
        // 1) Titre étendu
        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;

        // 2) Couleurs de la title bar cohérentes avec MotoTheme
        // ExtendsContentIntoTitleBar=true ne rend PAS la barre transparente à lui
        // seul, il fait juste passer notre contenu DESSOUS ; sans ces propriétés,
        // Windows continue de peindre la barre de titre dans sa couleur par défaut
        // (le bandeau bleu vu par Tom, par-dessus notre CustomMenuBarView).
        // Confirmé sur learn.microsoft.com.
        var bg = ToColor("#17181C"); // même couleur que CustomMenuBarView
        appWindow.TitleBar.BackgroundColor = bg;
        appWindow.TitleBar.InactiveBackgroundColor = bg;
        appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        appWindow.TitleBar.ButtonHoverBackgroundColor = ToColor("#2A2C31"); // BgHover
        appWindow.TitleBar.ButtonForegroundColor = ToColor("#E5E7EB");
        appWindow.TitleBar.ButtonInactiveForegroundColor = ToColor("#E5E7EB");
        appWindow.TitleBar.ButtonHoverForegroundColor = ToColor("#D97757"); // Accent
    }

    public static void ConfigureSnapLayouts(Microsoft.UI.Xaml.Window window,
        FrameworkElement btnMin, FrameworkElement btnMax, FrameworkElement btnClose,
        FrameworkElement dragZone)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var nonClientSource = InputNonClientPointerSource.GetForWindowId(windowId);

        // Redondant avec l'appel fait dès OnWindowsWindowCreated (App.xaml.cs), mais
        // sans coût : ré-appliquer les mêmes valeurs ne fait rien de plus que les
        // reconfirmer — gardé pour que ConfigureSnapLayouts reste utilisable seule.
        ApplyTitleBarColors(appWindow);

        // Zone de drag : UNIQUEMENT la zone centrale
        SetRegion(nonClientSource, NonClientRegionKind.Caption, dragZone);

        // Zones des boutons : interactives ET reconnues par Windows (survol
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

        // ★ CORRECTION (31/08) : ConfigureSnapLayouts n'est appelée qu'après
        // MainPage.OnPageLoaded — à ce stade, TitleBarDragZone/BtnMin/BtnMax/BtnClose
        // ont déjà eu largement le temps de déclencher LEUR PROPRE Loaded avant qu'on
        // s'y abonne ici. Résultat : Appliquer() n'était jamais appelée tant qu'aucun
        // redimensionnement ne survenait ensuite (ex. Maximiser) — la fenêtre restait
        // non-déplaçable et la barre de titre native ne se réduisait jamais vraiment
        // tant que la zone "Caption" n'avait pas de région enregistrée. Repéré par
        // Tom : "impossible de déplacer la fenêtre... sauf si on clique sur Plein
        // écran" — Maximiser déclenche un SizeChanged qui appliquait enfin la région.
        // Appelé immédiatement en plus des abonnements (qui restent utiles pour les
        // futurs redimensionnements/changements de DPI).
        //
        // ★ CORRECTION (31/08, 2e passe) : l'appel immédiat seul ne suffisait pas —
        // Tom a confirmé que la fenêtre restait non-déplaçable INDÉFINIMENT (pas
        // juste 30s-1min) tant qu'aucun redimensionnement réel ne survenait. Cause
        // probable : à l'instant précis de cet appel (dans OnPageLoaded), la passe de
        // mise en page de la fenêtre peut ne pas être totalement terminée — lire
        // ActualSize/TransformToVisual trop tôt donne un rectangle à 0 ou mal placé,
        // enregistré comme "la" zone de drag alors qu'il ne correspond à rien de
        // visible. En plus de l'appel synchrone, un second appel est maintenant
        // reporté via DispatcherQueue (priorité basse : après que toute mise en page
        // en attente soit terminée) pour corriger avec des mesures forcément à jour.
        Appliquer();
        element.DispatcherQueue?.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => Appliquer());
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
