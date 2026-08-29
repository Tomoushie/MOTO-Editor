// Moto.Editor/Platforms/Mac/MacShellAdapter.cs
#if MACOS || MACCATALYST
using System;
using Moto.Core.Platform;

namespace Moto.Editor.Platforms.Mac;

/// <summary>Adapter macOS : implémentations natives ou fallbacks propres.</summary>
public sealed class MacShellAdapter : IPlatformShell
{
    public void ShowToast(string title, string message)
    {
        // Fallback : notification via NSUserNotification (à brancher) ou log
        Console.WriteLine($"[MOTO] {title} — {message}");
    }

    public void SetWindowIcon(string iconPath)
    {
        // macOS gère l'icône via le bundle (.icns) — rien à faire au runtime
    }

    public bool TryRegisterGlobalHotkey(string combo, Action onTriggered)
    {
        // Les hotkeys globales macOS nécessitent l'accessibilité ; on désactive proprement
        return false;
    }

    public void AddSystemMenuAbout(Action onAbout)
    {
        // macOS a déjà un menu "À propos" standard dans la barre de menu
    }
}
#endif
