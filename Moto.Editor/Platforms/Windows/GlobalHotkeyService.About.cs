// Moto.Editor/Platforms/Windows/GlobalHotkeyService.About.cs
#if WINDOWS
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Moto.Editor.Platforms.Windows;

/// <summary>
/// Extension additive : raccourci global Ctrl+Shift+A → ouvre « À propos ».
/// NOTE : si GlobalHotkeyService n'est pas déclarée `partial`, ajoutez le mot-clé.
/// </summary>
public partial class GlobalHotkeyService
{
    public const int AboutHotkeyId = 0xADA0;
    private Thread? _aboutHotkeyThread;

    /// <summary>Déclenché quand Ctrl+Shift+A est pressé.</summary>
    public event Action? AboutHotkeyPressed;

    // P/Invoke nommés différemment pour éviter tout conflit avec la classe de base.
    // ★ CORRECTION (30/08) : il manquait `EntryPoint = "..."` — sans lui, P/Invoke
    // cherchait une fonction appelée littéralement "NativeRegisterHotKey" dans
    // user32.dll (qui n'existe pas) au lieu de la vraie "RegisterHotKey" ⇒
    // EntryPointNotFoundException au premier déclenchement du thread du raccourci.
    [DllImport("user32.dll", EntryPoint = "RegisterHotKey")] private static extern bool NativeRegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", EntryPoint = "UnregisterHotKey")] private static extern bool NativeUnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", EntryPoint = "GetMessageW")] private static extern int NativeGetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public IntPtr pt; }

    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT   = 0x0004;
    private const uint VK_A        = 0x41;
    private const uint WM_HOTKEY   = 0x0312;

    /// <summary>Enregistre Ctrl+Shift+A sur un thread de messages dédié.</summary>
    public void RegisterAboutHotkey()
    {
        if (_aboutHotkeyThread != null) return;

        _aboutHotkeyThread = new Thread(() =>
        {
            NativeRegisterHotKey(IntPtr.Zero, AboutHotkeyId, MOD_CONTROL | MOD_SHIFT, VK_A);
            while (NativeGetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == WM_HOTKEY && msg.wParam.ToInt32() == AboutHotkeyId)
                    AboutHotkeyPressed?.Invoke();
            }
            NativeUnregisterHotKey(IntPtr.Zero, AboutHotkeyId);
        })
        { IsBackground = true };

        _aboutHotkeyThread.Start();
    }
}
#endif
