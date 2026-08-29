// Moto.Editor/Platforms/Windows/SystemMenuAboutService.cs
#if WINDOWS
using System;
using System.Runtime.InteropServices;

namespace Moto.Editor.Platforms.Windows;

/// <summary>
/// Ajoute « À propos de MOTO Editor… » au menu système de la fenêtre WinUI 3
/// (via GetSystemMenu + subclassing pour intercepter WM_SYSCOMMAND).
/// </summary>
public sealed class SystemMenuAboutService
{
    public event Action? AboutRequested;

    private const uint WM_SYSCOMMAND = 0x0112;
    private const uint MF_SEPARATOR = 0x0800;
    private const uint MF_STRING    = 0x0000;
    private const uint SC_ABOUT     = 0x1000; // id custom (hors plage réservée 0xF000+)

    private delegate IntPtr SubclassDelegate(IntPtr hWnd, uint uMsg, UIntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);
    private static SubclassDelegate? _hook;      // racine anti-GC
    private static SystemMenuAboutService? _instance;

    [DllImport("user32.dll")] private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassDelegate pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, UIntPtr wParam, IntPtr lParam);

    public void Attach(IntPtr hwnd)
    {
        _instance = this;
        var menu = GetSystemMenu(hwnd, false);
        if (menu == IntPtr.Zero) return;

        AppendMenu(menu, MF_SEPARATOR, UIntPtr.Zero, null);
        AppendMenu(menu, MF_STRING, new UIntPtr(SC_ABOUT), "À propos de MOTO Editor…");

        _hook = HookProc;
        SetWindowSubclass(hwnd, _hook, new UIntPtr(0xADA1), IntPtr.Zero);
    }

    private static IntPtr HookProc(IntPtr hWnd, uint uMsg, UIntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (uMsg == WM_SYSCOMMAND && (wParam.ToUInt32() & 0xFFFF) == SC_ABOUT)
        {
            _instance?.AboutRequested?.Invoke();
            return IntPtr.Zero;
        }
        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }
}
#endif
