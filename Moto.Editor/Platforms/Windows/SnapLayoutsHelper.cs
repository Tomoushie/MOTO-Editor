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
    ///
    /// ★ RÉTABLIE (08/09) : la tentative "sans bordure permanente" (qui avait
    /// temporairement rendu cette méthode inutilisée) a été testée pour de vrai
    /// et N'A PAS supprimé la bande bleue (voir App.xaml.cs, OnWindowsWindowCreated,
    /// pour le compte-rendu complet). Cette méthode (approche "coopérative") reste
    /// donc le correctif "sûr" en usage — insuffisant seul contre le réglage
    /// Windows 11 "couleur d'accentuation" (microsoft-ui-xaml#9374), mais pas pire
    /// que l'alternative testée. Piste suivante notée dans la mémoire du chantier :
    /// DwmSetWindowAttribute (DWMWA_CAPTION_COLOR/DWMWA_BORDER_COLOR), jamais
    /// essayée, API de plus bas niveau qu'AppWindowTitleBar.
    /// </summary>
    public static void ApplyTitleBarColors(AppWindow appWindow)
    {
        // ★ AJOUT (01/09) : diagnostic direct de la barre bleue persistante. Si
        // IsCustomizationSupported() vaut false dans l'environnement réel, TOUT ce
        // qui suit dans cette méthode est un no-op silencieux documenté par
        // Microsoft (aucune exception levée, la barre native reste affichée
        // par-dessus) — exactement le symptôme signalé par Tom, dans les deux modes
        // (unpackaged ET MSIX). Le code de l'app ne vérifiait jusqu'ici jamais
        // cette condition avant d'appliquer les réglages.
        Moto.Editor.App.Breadcrumb(
            $"ApplyTitleBarColors — IsCustomizationSupported = {AppWindowTitleBar.IsCustomizationSupported()}");

        // ★ AJOUT (05/09, plantage réel trouvé par Tom) : cette méthode se
        // ré-exécute à CHAQUE changement de focus de la fenêtre (Activated ET
        // Deactivated, voir ConfigureSnapLayouts) — des centaines de fois par
        // session sans souci, confirmé par le journal. Une fois, sur ce poste,
        // set_ExtendsContentIntoTitleBar a levé ArgumentException ("Paramètre
        // incorrect") côté natif WinRT, non rattrapée, plantant TOUTE
        // l'application (Microsoft.UI.Xaml.Application.UnhandledException,
        // moto-editor-crash.log). Cause exacte non élucidée (chantier "barre
        // bleue" déjà en pause, voir CLAUDE.md/mémoire dédiée) — ce try/catch ne
        // la résout pas, il empêche seulement qu'un raté ponctuel et rare de
        // cet appel emporte toute l'appli : la barre de titre garde alors ses
        // couleurs précédentes pour cette fois, rien de plus grave.
        try
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

        // ★ CORRECTION (02/09, cause réelle trouvée) : la bande bleue persistante
        // n'était PAS un échec de cette méthode (elle s'exécute sans exception,
        // IsCustomizationSupported=true, les couleurs de boutons ci-dessus
        // s'appliquaient déjà — confirmé par le survol orange vu par Tom) mais le
        // réglage Windows 11 "Afficher la couleur d'accentuation sur les barres de
        // titre et les bordures des fenêtres", qui peut imposer une teinte système
        // par-dessus une AppWindowTitleBar personnalisée tant que TOUTES ses
        // couleurs ne sont pas explicitement fixées (recommandation officielle
        // Microsoft : learn.microsoft.com/windows/apps/develop/title-bar, section
        // "Colors" — "If you set any title bar color, we recommend that you
        // explicitly set all the colors"). Confirmé par Tom : désactiver ce réglage
        // change bien la bande. Les 4 propriétés ci-dessous manquaient à l'appel.
        appWindow.TitleBar.ForegroundColor = ToColor("#E5E7EB");
        appWindow.TitleBar.InactiveForegroundColor = ToColor("#E5E7EB");
        appWindow.TitleBar.ButtonPressedBackgroundColor = ToColor("#202126"); // BgPanel, même état "Pressed" que MotoHoverButton
        appWindow.TitleBar.ButtonPressedForegroundColor = ToColor("#E5E7EB");

        // Contrôle : si WinUI rejetait une valeur silencieusement, elle relirait
        // null/différente ici — vérifiable dans le journal sans dépendre du jugement
        // à l'œil sur une bande bleue ou non.
        Moto.Editor.App.Breadcrumb(
            $"ApplyTitleBarColors — relu : BackgroundColor={appWindow.TitleBar.BackgroundColor} " +
            $"ForegroundColor={appWindow.TitleBar.ForegroundColor}");

        // ★ AJOUT (08/09) : DwmSetWindowAttribute, extrait dans sa propre méthode
        // (voir ApplyDwmAttributeColors ci-dessous) — appelée seule (sans le reste
        // de cette méthode) par la tentative "sans bordure + DWM" combinée.
        ApplyDwmAttributeColors(appWindow);
        }
        catch (Exception ex)
        {
            Moto.Editor.App.Breadcrumb($"ApplyTitleBarColors — EXCEPTION rattrapée (barre de titre inchangée cette fois) : {ex}");
        }
    }

    /// <summary>
    /// ★ EXTRAIT (08/09) d'ApplyTitleBarColors pour pouvoir être appelée SEULE,
    /// indépendamment de l'approche "coopérative" (qui pose
    /// ExtendsContentIntoTitleBar=true, incompatible avec la tentative "sans
    /// bordure"). Testée en direct le 08/09 AVEC ExtendsContentIntoTitleBar=true
    /// (donc via ApplyTitleBarColors) : hr=0 sur les 3 attributs (acceptés pour de
    /// vrai) mais bande bleue inchangée — voir la mémoire du chantier.
    /// Pas encore testée avec ExtendsContentIntoTitleBar=false (fenêtre sans
    /// bordure) : c'est exactement ce que cette extraction permet de tenter,
    /// sans reposer ExtendsContentIntoTitleBar=true au passage.
    /// </summary>
    public static void ApplyDwmAttributeColors(AppWindow appWindow)
    {
        try
        {
            var hwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(appWindow.Id);
            if (hwnd != IntPtr.Zero)
            {
                var captionColor = ToColorRef("#17181C"); // même couleur que CustomMenuBarView/BgSide
                var borderColor = ToColorRef("#17181C");
                var textColor = ToColorRef("#E5E7EB"); // Txt1

                var hrCaption = DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(uint));
                var hrBorder = DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(uint));
                var hrText = DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref textColor, sizeof(uint));

                // hr = 0 (S_OK) si accepté ; toute autre valeur = HRESULT d'échec,
                // vérifiable dans le journal sans dépendre du jugement à l'œil.
                Moto.Editor.App.Breadcrumb(
                    $"ApplyDwmAttributeColors — caption(hr={hrCaption}) border(hr={hrBorder}) text(hr={hrText})");

                // ★ AJOUT (08/09, 6e tentative, accord de Tom — "on tente, tant pis
                // si Microsoft n'a jamais résolu ça") : MOTO Editor ne pose jamais
                // explicitement de SystemBackdrop nulle part (vérifié par grep :
                // 0 occurrence de Mica/Acrylic/SystemBackdrop avant cette ligne).
                // Sans réglage explicite, l'attribut DWM correspondant vaut
                // DWMSBT_AUTO (0) — "laisser DWM décider tout seul", qui PEUT choisir
                // un arrière-plan type Mica pour une fenêtre "moderne" sans qu'aucun
                // code de l'app ne l'ait demandé. Le fil microsoft-ui-xaml#9374 (lu le
                // 08/09) documente précisément ce déclencheur : ExtendsContentIntoTitleBar
                // + un backdrop Mica/Acrylic actif. Ici, on force DWMSBT_NONE (1) —
                // explicitement AUCUN backdrop.
                // ★★ TESTÉ EN DIRECT (08/09, 6e tentative, accord de Tom) : hr=0
                // (accepté) à chaque réapplication — MÊME RÉSULTAT que les 5
                // précédentes, bande bleue TOUJOURS inchangée. Cette hypothèse
                // (backdrop implicite) est donc écartée aussi. Gardé (comme les
                // 3 couleurs ci-dessus) : sans effet de bord négatif observé, juste
                // sans effet sur ce problème précis.
                var backdropNone = (uint)1; // DWMSBT_NONE
                var hrBackdrop = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropNone, sizeof(uint));
                Moto.Editor.App.Breadcrumb($"ApplyDwmAttributeColors — DWMWA_SYSTEMBACKDROP_TYPE=NONE (hr={hrBackdrop})");

                // ★ AJOUT (08/09, chantier "rendu 100% custom") : demande
                // explicitement les coins arrondis standards de Windows 11
                // (DWMWCP_ROUND), pour ne pas perdre ce détail visuel maintenant que
                // la fenêtre est sans bordure native de façon permanente — sans ça,
                // rien ne garantit que DWM continue de les appliquer par défaut.
                var cornerRound = (uint)2; // DWMWCP_ROUND
                var hrCorner = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerRound, sizeof(uint));
                Moto.Editor.App.Breadcrumb($"ApplyDwmAttributeColors — DWMWA_WINDOW_CORNER_PREFERENCE=ROUND (hr={hrCorner})");
            }
        }
        catch (Exception dwmEx)
        {
            Moto.Editor.App.Breadcrumb($"ApplyDwmAttributeColors — EXCEPTION (sans gravité) : {dwmEx}");
        }
    }

    // ★ AJOUT (08/09, chantier "rendu 100% custom", "plein écran manuel") :
    // état minimal — une seule fenêtre principale dans MOTO Editor (même
    // hypothèse que Application.Current.Windows[0] utilisée ailleurs dans le
    // projet, ex. CustomMenuBarView.xaml.cs).
    private static bool _isFullScreen;

    /// <summary>
    /// Bascule plein écran / fenêtré. AppWindow.SetPresenter(Overlapped) en
    /// sortie de plein écran RECRÉE un présentateur par défaut (avec bordure) —
    /// la personnalisation "sans bordure permanente" de ce chantier doit donc
    /// être réappliquée immédiatement après, sinon la bande/bordure native
    /// reviendrait à chaque sortie du plein écran.
    /// </summary>
    public static void ToggleFullScreen(Microsoft.UI.Xaml.Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        if (!_isFullScreen)
        {
            appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            _isFullScreen = true;
            Moto.Editor.App.Breadcrumb("ToggleFullScreen — entré en plein écran");
        }
        else
        {
            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
            }
            _isFullScreen = false;
            Moto.Editor.App.Breadcrumb("ToggleFullScreen — sorti du plein écran, sans-bordure réappliqué");
        }
    }

    public static void ConfigureSnapLayouts(Microsoft.UI.Xaml.Window window,
        FrameworkElement btnMin, FrameworkElement btnMax, FrameworkElement btnClose,
        FrameworkElement dragZone)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var nonClientSource = InputNonClientPointerSource.GetForWindowId(windowId);

        // ★ CHANGÉ (08/09, chantier "rendu 100% custom", accord de Tom) : la
        // fenêtre est désormais sans bordure PERMANENTE (voir App.xaml.cs) — appeler
        // ApplyTitleBarColors ici reposerait ExtendsContentIntoTitleBar=true et
        // annulerait ce mode. ApplyDwmAttributeColors seule (couleurs DWM + backdrop),
        // sans toucher à l'état sans-bordure.
        ApplyDwmAttributeColors(appWindow);

        // Zone de drag : UNIQUEMENT la zone centrale
        SetRegion(nonClientSource, NonClientRegionKind.Caption, dragZone);

        // Zones des boutons : interactives ET reconnues par Windows (survol
        // Maximiser -> flyout Snap Layouts) grâce au bon NonClientRegionKind.
        SetRegion(nonClientSource, NonClientRegionKind.Minimize, btnMin);
        SetRegion(nonClientSource, NonClientRegionKind.Maximize, btnMax);
        SetRegion(nonClientSource, NonClientRegionKind.Close, btnClose);

        // ★ AJOUT (08/09, "zones de sécurité" au sens de Tom) : sans bordure native,
        // Windows n'a plus AUCUNE zone de redimensionnement — à recréer nous-mêmes.
        // NonClientRegionKind ne définit que 4 valeurs de bordure (Top/Left/Bottom/
        // RightBorder), pas de coin séparé (vérifié sur learn.microsoft.com,
        // WindowsAppSDK 1.8 — pas une supposition) : les coins se comportent
        // correctement à l'intersection de deux bordures adjacentes.
        ConfigureResizeBorders(appWindow, nonClientSource);
    }

    /// <summary>
    /// ★ AJOUT (08/09, chantier "rendu 100% custom") : bordures de redimensionnement
    /// invisibles, recalculées à chaque changement de taille (AppWindow.Changed).
    /// Épaisseur ~6px DIP, une valeur standard de bordure de redimensionnement
    /// Windows, convertie en pixels physiques via DragZoneHelper (cohérent avec le
    /// reste de ce fichier). Top/Bottom couvrent toute la largeur (y compris les
    /// coins) ; Left/Right couvrent la hauteur restante entre les deux, pour éviter
    /// tout chevauchement de zones enregistrées deux fois.
    /// </summary>
    private const double ResizeBorderThicknessDip = 6;

    public static void ConfigureResizeBorders(AppWindow appWindow, InputNonClientPointerSource nonClientSource)
    {
        void Appliquer()
        {
            var thickness = DragZoneHelper.DipToPhysical(ResizeBorderThicknessDip);
            var size = appWindow.Size; // déjà en pixels physiques (API AppWindow)
            var innerHeight = Math.Max(0, size.Height - (2 * thickness));

            nonClientSource.SetRegionRects(NonClientRegionKind.TopBorder,
                new[] { new global::Windows.Graphics.RectInt32(0, 0, size.Width, thickness) });
            nonClientSource.SetRegionRects(NonClientRegionKind.BottomBorder,
                new[] { new global::Windows.Graphics.RectInt32(0, size.Height - thickness, size.Width, thickness) });
            nonClientSource.SetRegionRects(NonClientRegionKind.LeftBorder,
                new[] { new global::Windows.Graphics.RectInt32(0, thickness, thickness, innerHeight) });
            nonClientSource.SetRegionRects(NonClientRegionKind.RightBorder,
                new[] { new global::Windows.Graphics.RectInt32(size.Width - thickness, thickness, thickness, innerHeight) });

            Moto.Editor.App.Breadcrumb(
                $"ConfigureResizeBorders — taille={size.Width}x{size.Height} épaisseur={thickness}px");
        }

        Appliquer();
        appWindow.Changed += (s, args) =>
        {
            if (args.DidSizeChange) Appliquer();
        };
    }

    private static void SetRegion(InputNonClientPointerSource source, NonClientRegionKind kind, FrameworkElement element)
    {
        // DENSITÉ UNIFIÉE
        var scale = DragZoneHelper.CurrentDensity;

        void Appliquer()
        {
            var rect = GetScaledRect(element, scale);
            source.SetRegionRects(kind, new[] { RectInt32From(rect) });
            // ★ AJOUT (31/08, 3e passe) : journalise le rectangle réellement enregistré
            // — après 2 correctifs sans effet confirmé par Tom, plus la peine de deviner
            // à l'aveugle : ce breadcrumb permet de lire directement dans le journal
            // (%TEMP%\moto-editor-crash.log, même machine) ce qui a été appliqué et
            // quand, sans dépendre d'un nouveau tour d'aller-retour.
            Moto.Editor.App.Breadcrumb(
                $"SnapLayouts.Appliquer — {kind} : X={rect.X} Y={rect.Y} W={rect.W} H={rect.H} " +
                $"(élément chargé={element.IsLoaded}, ActualSize={element.ActualSize.X}x{element.ActualSize.Y})");
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

    // ══════════════════════════════════════════════════════════════
    // ★ AJOUT (08/09) : DwmSetWindowAttribute (dwmapi.dll) — pas d'équivalent
    // dans WinAppSDK/AppWindowTitleBar. Voir le commentaire dans
    // ApplyTitleBarColors ci-dessus pour le pourquoi.
    // ══════════════════════════════════════════════════════════════
    private const uint DWMWA_BORDER_COLOR = 34;
    private const uint DWMWA_CAPTION_COLOR = 35;
    private const uint DWMWA_TEXT_COLOR = 36;
    private const uint DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const uint DWMWA_WINDOW_CORNER_PREFERENCE = 33;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint dwAttribute, ref uint pvAttribute, uint cbAttribute);

    /// <summary>
    /// Convertit "#RRGGBB" en COLORREF (0x00BBGGRR) — ordre INVERSE de RGB,
    /// piège classique de cette API Win32 (à ne pas confondre avec ToColor()
    /// ci-dessus, qui produit un global::Windows.UI.Color pour AppWindowTitleBar).
    /// </summary>
    private static uint ToColorRef(string hex)
    {
        hex = hex.TrimStart('#');
        byte r = Convert.ToByte(hex[..2], 16);
        byte g = Convert.ToByte(hex[2..4], 16);
        byte b = Convert.ToByte(hex[4..6], 16);
        return (uint)((b << 16) | (g << 8) | r);
    }
}
#endif
