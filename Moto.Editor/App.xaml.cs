// App.xaml.cs
using Microsoft.Maui.Controls;
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
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, e) =>
                LogCrash("TaskScheduler.UnobservedTaskException", e.Exception);

            try
            {
                InitializeComponent();
                MainPage = new MainPage();
            }
            catch (Exception ex)
            {
                LogCrash("App() constructeur", ex);
                throw;
            }
        }

        private static void LogCrash(string source, Exception? ex)
        {
            try
            {
                System.IO.File.AppendAllText(CrashLogPath,
                    $"[{DateTime.Now:O}] {source}\n{ex}\n\n");
            }
            catch { /* ne doit jamais planter la capture elle-même */ }
        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            var window = base.CreateWindow(activationState);

#if WINDOWS
            window.HandlerChanged += (s, e) =>
            {
                if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native)
                {
                    // Masque la barre de titre native : le contenu s'étend dessous.
                    native.ExtendsContentIntoTitleBar = true;
                }
            };
            window.Created += OnWindowsWindowCreated;
#endif
            return window;
        }

#if WINDOWS
        private void OnWindowsWindowCreated(object? sender, EventArgs e)
        {
            try
            {
                if (sender is not Window mauiWindow) return;
                var nativeWindow = mauiWindow.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                if (nativeWindow is null) return;

                // Récupère l'AppWindow WinUI 3 via le HWND
                IntPtr hwnd = WindowNative.GetWindowHandle(nativeWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                // Titre de la fenêtre
                appWindow.Title = "MOTO Editor";

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
            }
            catch
            {
                // Le branding ne doit jamais empêcher le démarrage.
            }
        }
#endif
    }
}
