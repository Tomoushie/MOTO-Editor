// Moto.Editor/Platforms/Windows/WindowsShellAdapter.cs
#if WINDOWS
using System;
using Moto.Core.Platform;

namespace Moto.Editor.Platforms.Windows;

/// <summary>Adapter Windows : réutilise les services natifs existants.</summary>
public sealed class WindowsShellAdapter : IPlatformShell
{
    private readonly SystemMenuAboutService _sysMenu;
    private readonly GlobalHotkeyService _hotkey;

    public WindowsShellAdapter(SystemMenuAboutService sysMenu, GlobalHotkeyService hotkey)
    {
        _sysMenu = sysMenu;
        _hotkey = hotkey;
    }

    public void ShowToast(string title, string message)
        => ToastNotificationService.Show(title, message);

    public void SetWindowIcon(string iconPath)
    {
        // déjà géré dans App.xaml.cs (AppWindow.SetIcon)
    }

    public bool TryRegisterGlobalHotkey(string combo, Action onTriggered)
    {
        _hotkey.RegisterAboutHotkey();
        _hotkey.AboutHotkeyPressed += onTriggered;
        return true;
    }

    public void AddSystemMenuAbout(Action onAbout)
        => _sysMenu.AboutRequested += onAbout;
}
#endif
