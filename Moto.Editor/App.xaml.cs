// App.xaml.cs
using Microsoft.Maui.Controls;
#if WINDOWS
using WinRT.Interop;
#endif

namespace Moto.Editor
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            MainPage = new MainPage();
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
