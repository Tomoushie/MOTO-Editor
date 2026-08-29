// Moto.Editor/App.xaml.cs
#if WINDOWS
// Après SetIcon(...) dans OnWindowsWindowCreated :
var services = mauiWindow.Handler?.MauiContext?.Services;
if (services != null)
{
    // Menu système « À propos »
    var sysmenu = services.GetService(typeof(Moto.Editor.Platforms.Windows.SystemMenuAboutService))
        as Moto.Editor.Platforms.Windows.SystemMenuAboutService;
    if (sysmenu != null)
    {
        sysmenu.AboutRequested += () => Moto.Editor.Services.AboutLauncher.RequestShow();
        sysmenu.Attach(hwnd);
    }

    // Raccourci global Ctrl+Shift+A
    var hotkey = services.GetService(typeof(Moto.Editor.Platforms.Windows.GlobalHotkeyService))
        as Moto.Editor.Platforms.Windows.GlobalHotkeyService;
    if (hotkey != null)
    {
        hotkey.RegisterAboutHotkey();
        hotkey.AboutHotkeyPressed += () => Moto.Editor.Services.AboutLauncher.RequestShow();
    }
}
#endif
            return window;
        }

#if WINDOWS
        private void OnWindowsWindowCreated(object? sender, EventArgs e)
        {
            try
            {
                if (sender is not Window mauiWindow) return;
                var nativeWindow = mauiWindow.Handler?.PlatformView as WindowsUI.Window;
                if (nativeWindow is null) return;

                // Récupère l'AppWindow WinUI 3 via le HWND
                IntPtr hwnd = WindowNative.GetWindowHandle(nativeWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.AppWindow.GetFromWindowId(windowId);

                // Titre de la fenêtre
                appWindow.Title = "MOTO Editor";

                // ★ Icône hexagonale dans la titlebar + taskbar
                string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "appicon.ico");
                if (System.IO.File.Exists(iconPath))
                    appWindow.SetIcon(iconPath);
            }
            catch
            {
                // Le branding ne doit jamais empêcher le démarrage.
            }
        }
#endif
    }
}
