// App.xaml.cs
using Microsoft.Maui.Controls;
using Moto.Core.Settings;
#if WINDOWS
using WinRT.Interop;
#endif

namespace Moto.Editor
{
    public partial class App : Application
    {
        // ★ Filet de sécurité (30/08) : capture toute exception non gérée au démarrage
        // dans %TEMP%\moto-editor-crash.log. Une appli WinUI qui plante au lancement
        // ne montre souvent ni fenêtre ni message en console — ce fichier est le seul
        // moyen de savoir ce qui a échoué (utile à Tom comme à moi).
        private static readonly string CrashLogPath =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "moto-editor-crash.log");

        public App()
        {
            Breadcrumb("App() — entrée");

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogCrash("TaskScheduler.UnobservedTaskException", e.Exception);

            // ★ AJOUT (30/08, suggestion externe vérifiée et retenue) : filet
            // supplémentaire au niveau WinUI natif. Une exception levée sur le thread UI
            // (ex. un gestionnaire de bouton défectueux) peut ne JAMAIS atteindre
            // AppDomain.UnhandledException ni TaskScheduler.UnobservedTaskException —
            // c'est ce genre de cas qui a produit un vrai crash chez Tom (clic sur
            // "Providers IA", cause racine réglée par ailleurs via NavigationPage, mais
            // ce filet reste utile pour toute AUTRE exception UI future du même genre).
            // e.Handled = true empêche la fermeture de l'appli ; loggé comme les autres.
#if WINDOWS
            global::Microsoft.UI.Xaml.Application.Current.UnhandledException += (s, e) =>
            {
                LogCrash("Microsoft.UI.Xaml.Application.UnhandledException", e.Exception);
                e.Handled = true;
            };
#endif

            try
            {
                Breadcrumb("App() — avant InitializeComponent");
                InitializeComponent();
                Breadcrumb("App() — avant new MainPage()");
                // ★ CORRECTION (30/08) : MainPage doit être hébergée dans une NavigationPage.
                // Sans ça, Navigation.PushAsync (utilisé par "Providers IA" dans les
                // paramètres et par le clic sur l'indicateur IA 🧠) lève une exception au
                // premier appel — Tom a eu un vrai crash au clic sur "Providers IA (clés API)".
                // NavigationPage.SetHasNavigationBar(false) masque sa barre à elle (celle de
                // MAUI, indépendante de la barre de titre Windows gérée par SnapLayoutsHelper)
                // pour ne rien changer visuellement — MainPage garde son chrome 100% custom.
                var mainPage = new MainPage();
                var navigationPage = new NavigationPage(mainPage);
                NavigationPage.SetHasNavigationBar(mainPage, false);
                MainPage = navigationPage;
                Breadcrumb("App() — MainPage créée avec succès");
            }
            catch (Exception ex)
            {
                LogCrash("App() constructeur", ex);
                throw;
            }
        }

        internal static void LogCrash(string source, Exception? ex)
        {
            try
            {
                System.IO.File.AppendAllText(CrashLogPath,
                    $"[{DateTime.Now:O}] {source}\n{ex}\n\n");
            }
            catch { /* ne doit jamais planter la capture elle-même */ }
        }

        /// <summary>Trace inconditionnelle (pas seulement en cas d'exception) pour localiser où l'exécution s'arrête.</summary>
        internal static void Breadcrumb(string message)
        {
            try
            {
                System.IO.File.AppendAllText(CrashLogPath, $"[{DateTime.Now:O}] {message}\n");
            }
            catch { }
        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            Breadcrumb("CreateWindow — entrée");
            Window window;
            try
            {
                window = base.CreateWindow(activationState);
                // ★ CORRECTION (31/08) : "MOTO Editor" restait visible dans la barre de
                // titre malgré appWindow.Title = "" (posé plus bas, niveau natif) — MAUI
                // gère SA PROPRE propriété Window.Title (dérivée d'ApplicationTitle dans
                // le .csproj) et la re-synchronise vers l'AppWindow natif après coup,
                // écrasant silencieusement ce qu'on avait posé. Vidée ici, au niveau que
                // MAUI lui-même contrôle, pour que ça tienne.
                // ★ CORRECTION (31/08, 2e passe) : string.Empty ne suffisait pas — Tom
                // voit toujours "MOTO Editor" après 2 tentatives (ici + AppWindow.Title
                // plus bas). Hypothèse : MAUI traite un titre vide comme "non défini" et
                // retombe sur ApplicationTitle (.csproj) au lieu de le laisser vide. Un
                // espace n'est PAS vide pour cette logique, mais reste visuellement blanc.
                window.Title = " ";

                // ★ AJOUT (01/09, revue croisée — MainPage.xaml, dock du bas Terminal) :
                // aucune taille minimale n'était imposée à la fenêtre. RootGrid a
                // maintenant une ligne "Auto" en plus (le dock Terminal, 120-500px de
                // haut selon la poignée) qui rivalise avec la ligne "*" du contenu
                // principal (Accueil/Éditeur/Explorateur) pour la hauteur disponible —
                // une fenêtre réduite assez petit aurait pu écraser cette ligne "*"
                // jusqu'à (quasi) 0px, reproduisant la même classe de plantage WinRT
                // déjà documentée plus haut dans ce fichier (une CollectionView/
                // ItemsRepeater — celle de l'explorateur ou celle des onglets de
                // l'éditeur — arrangée dans un rectangle dégénéré). Bornes choisies
                // pour garder au moins ~280px à la ligne "*" même avec le Terminal à
                // sa hauteur plafond (revue croisée : plafond réduit 500→360 dans
                // OnBottomDockResizePanUpdated, MainPage.Panels.cs, pour la même raison).
                window.MinimumHeight = 700;
                window.MinimumWidth = 1000;
                Breadcrumb("CreateWindow — base.CreateWindow OK");
            }
            catch (Exception ex)
            {
                LogCrash("CreateWindow — base.CreateWindow", ex);
                throw;
            }

#if WINDOWS
            window.HandlerChanged += (s, e) =>
            {
                Breadcrumb("Window.HandlerChanged");
                if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native)
                {
                    // Masque la barre de titre native : le contenu s'étend dessous.
                    native.ExtendsContentIntoTitleBar = true;
                }
            };
            window.Created += OnWindowsWindowCreated;
#endif
            Breadcrumb("CreateWindow — sortie");
            return window;
        }

#if WINDOWS
        private void OnWindowsWindowCreated(object? sender, EventArgs e)
        {
            // ★ AJOUT (01/09) : diagnostic de la barre bleue MSIX persistante. Ce bloc
            // entier (couleurs/extension de titlebar incluses) était entouré d'un
            // catch MUET (voir plus bas) — impossible jusqu'ici de savoir si une
            // exception l'interrompait avant d'atteindre ApplyTitleBarColors. Ces
            // deux traces ne changent rien visuellement, elles servent uniquement à
            // lire le vrai déroulement dans %TEMP%\moto-editor-crash.log.
            Breadcrumb("OnWindowsWindowCreated — entrée");
            try
            {
                if (sender is not Window mauiWindow) return;
                var nativeWindow = mauiWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (nativeWindow is null) return;

                // Récupère l'AppWindow WinUI 3 via le HWND
                IntPtr hwnd = WindowNative.GetWindowHandle(nativeWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                // ★ CORRECTION (30/08, 2e passe ; 31/08, 3e passe) : Tom veut ce texte
                // retiré de l'intérieur de l'appli — déjà redondant avec le nom affiché
                // par Windows sur la barre des tâches/Alt-Tab (ApplicationTitle, .csproj).
                // string.Empty seul ne suffisait pas (persistait après 2 tentatives) —
                // espace au lieu de vide, même correctif que window.Title plus haut.
                appWindow.Title = " ";

                // ★ CORRECTION (30/08, 2e passe) : couleurs/extension de la title bar
                // appliquées ICI, dès la création de la fenêtre — avant, elles
                // n'étaient posées que dans MainPage.OnPageLoaded (bien plus tard,
                // après le premier rendu), laissant Windows afficher/figer la barre
                // bleue par défaut entre-temps (repéré par Tom). Ce qui dépend des
                // FrameworkElement de MainPage (zone de drag, boutons) reste posé
                // plus tard par SnapLayoutsHelper.ConfigureSnapLayouts.
                Platforms.Windows.SnapLayoutsHelper.ApplyTitleBarColors(appWindow);

                // ★ TENTÉ PUIS ABANDONNÉ (02/09) : OverlappedPresenter.SetBorderAndTitleBar
                // (false, false) — testé prudemment, étape par étape, avec journal détaillé.
                // Résultat : la fenêtre restait visible (mieux que les 3 tentatives
                // précédentes) et le redimensionnement au bord continuait de fonctionner,
                // mais le résultat visuel a EMPIRÉ plutôt que réglé le problème : double
                // bande (une petite changeant de couleur au focus, une grande toujours
                // bleue) et disparition des boutons ─▢✕ natifs, sans que la bande native
                // ne disparaisse pour autant. Correspond exactement au comportement encore
                // non résolu documenté dans microsoft-ui-xaml#9374 (même symptôme, jamais
                // vraiment corrigé côté Microsoft). Retiré proprement — voir la mémoire du
                // chantier pour le détail complet et la piste "fenêtre sans bordure avec
                // rendu 100% custom" restée non tentée (bien plus gros chantier, hors de
                // portée d'une tentative prudente).

                // ★ CORRECTION (30/08) : aucune taille n'était fixée nulle part — la
                // fenêtre s'ouvrait à la taille par défaut de WinUI (bien plus large que
                // l'écran de contenu réel, repéré par Tom : "beaucoup trop large" au
                // lancement). Taille confortable, centrée sur l'écran principal (zone de
                // travail hors barre des tâches, via DisplayArea.WorkArea).
                // ★ CORRECTION : "Windows.Graphics...." non qualifié résout vers le
                // sous-espace de noms DU PROJET "Moto.Editor.Windows" (WindowManager y
                // vit, voir MainPage.Extensions.cs) plutôt que le namespace WinRT global
                // — même piège que Win32Interop rencontré plus tôt (SnapLayoutsHelper.cs).
                const int DefaultWidth = 1360;
                const int DefaultHeight = 860;
                var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
                    windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);

                // ★ AJOUT (02/09, persistance de session) : réutilise la taille/position
                // mémorisées à la fermeture précédente (voir l'abonnement à
                // mauiWindow.Destroying plus bas) si elles existent ET tiennent encore
                // dans l'écran actuel (un moniteur externe débranché entre deux
                // lancements ne doit jamais rouvrir la fenêtre hors champ) — sinon,
                // comportement inchangé : taille par défaut, centrée.
                int width = SettingsEngine.Shared.GetInt("window.width", DefaultWidth);
                int height = SettingsEngine.Shared.GetInt("window.height", DefaultHeight);
                int savedX = SettingsEngine.Shared.GetInt("window.x", int.MinValue);
                int savedY = SettingsEngine.Shared.GetInt("window.y", int.MinValue);

                if (width < 640 || height < 480) { width = DefaultWidth; height = DefaultHeight; }
                // ★ CORRECTIF (03/09, trouvé par Tom : panneau Terminal "caché en bas
                // de l'écran") : la POSITION sauvegardée est bien revérifiée contre
                // l'écran actuel (voir plus bas, tolérance 100px), mais la TAILLE ne
                // l'était jamais — une hauteur mémorisée plus grande que l'écran actuel
                // (ex. enregistrée sur un autre moniteur, ou après un redimensionnement
                // manuel) rouvrait systématiquement une fenêtre trop grande, poussant
                // le bas (ici : le panneau Terminal, son bouton fermer, la barre de
                // statut) hors de l'écran visible. Plafonné à la zone de travail réelle
                // (hors barre des tâches) du moniteur sur lequel la fenêtre va s'ouvrir.
                if (displayArea != null)
                {
                    width = Math.Min(width, displayArea.WorkArea.Width);
                    height = Math.Min(height, displayArea.WorkArea.Height);
                }
                appWindow.Resize(new global::Windows.Graphics.SizeInt32(width, height));

                bool restoredPosition = false;
                if (displayArea != null && savedX != int.MinValue && savedY != int.MinValue)
                {
                    var wa = displayArea.WorkArea;
                    // Le coin haut-gauche doit rester visible dans la zone de travail —
                    // tolérance de 100px pour autoriser une fenêtre partiellement hors
                    // écran comme le ferait Windows lui-même (bord de dock, etc.).
                    if (savedX >= wa.X - 100 && savedX < wa.X + wa.Width
                        && savedY >= wa.Y - 100 && savedY < wa.Y + wa.Height)
                    {
                        // ★ CORRECTIF (03/09) : le coin haut-gauche seul ne suffit pas —
                        // une position sauvegardée en haut de l'écran, combinée à la
                        // hauteur (déjà plafonnée ci-dessus mais pas forcément petite),
                        // peut quand même pousser le BAS de la fenêtre hors de l'écran.
                        // Décalé vers le haut si besoin pour garder tout le bas visible.
                        int finalY = Math.Min(savedY, wa.Y + wa.Height - height);
                        appWindow.Move(new global::Windows.Graphics.PointInt32(savedX, finalY));
                        restoredPosition = true;
                    }
                }
                if (!restoredPosition && displayArea != null)
                {
                    var centerX = displayArea.WorkArea.X + (displayArea.WorkArea.Width - width) / 2;
                    var centerY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - height) / 2;
                    appWindow.Move(new global::Windows.Graphics.PointInt32(centerX, centerY));
                }

                // ★ AJOUT (02/09, persistance de session) : sauvegarde la taille/position
                // ACTUELLES juste avant la fermeture (pas en continu — inutile de
                // réécrire le fichier de réglages à chaque pixel glissé pendant un
                // redimensionnement). Même événement déjà utilisé pour ce genre de
                // nettoyage de fin de vie ailleurs dans le projet (WindowManager.cs).
                mauiWindow.Destroying += (_, _) =>
                {
                    try
                    {
                        SettingsEngine.Shared.Set("window.width", appWindow.Size.Width);
                        SettingsEngine.Shared.Set("window.height", appWindow.Size.Height);
                        SettingsEngine.Shared.Set("window.x", appWindow.Position.X);
                        SettingsEngine.Shared.Set("window.y", appWindow.Position.Y);
                    }
                    catch { /* ne doit jamais empêcher la fermeture de l'app */ }
                };

                // ★ Icône hexagonale dans la titlebar + taskbar (si générée, voir assets/icon/)
                string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "appicon.ico");
                if (System.IO.File.Exists(iconPath))
                    appWindow.SetIcon(iconPath);

                // Menu système « À propos » + raccourci global Ctrl+Shift+A
                var services = mauiWindow.Handler?.MauiContext?.Services;
                if (services != null)
                {
                    var sysmenu = services.GetService(typeof(Moto.Editor.Platforms.Windows.SystemMenuAboutService))
                        as Moto.Editor.Platforms.Windows.SystemMenuAboutService;
                    if (sysmenu != null)
                    {
                        sysmenu.AboutRequested += () => Moto.Editor.Services.AboutLauncher.RequestShow();
                        sysmenu.Attach(hwnd);
                    }

                    var hotkey = services.GetService(typeof(Moto.Editor.Platforms.Windows.GlobalHotkeyService))
                        as Moto.Editor.Platforms.Windows.GlobalHotkeyService;
                    if (hotkey != null)
                    {
                        hotkey.RegisterAboutHotkey();
                        hotkey.AboutHotkeyPressed += () => Moto.Editor.Services.AboutLauncher.RequestShow();
                    }
                }

                Breadcrumb("OnWindowsWindowCreated — sortie (succès, ApplyTitleBarColors exécuté)");
            }
            catch (Exception ex)
            {
                // Le branding ne doit jamais empêcher le démarrage — on continue de ne
                // jamais relancer l'exception. Mais on la trace désormais réellement :
                // avant cet ajout, un échec ici (donc AUCUNE barre custom appliquée)
                // était strictement indiscernable d'une réussite dans le journal.
                LogCrash("OnWindowsWindowCreated (bloc titlebar/taille/icône)", ex);
            }
        }
#endif
    }
}
