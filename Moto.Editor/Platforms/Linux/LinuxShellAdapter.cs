// Moto.Editor/Platforms/Linux/LinuxShellAdapter.cs
using System;
using Moto.Core.Platform;

namespace Moto.Editor.Platforms.Linux;

/// <summary>Adapter Linux : no-op / fallbacks (MAUI desktop Linux non officiel).</summary>
public sealed class LinuxShellAdapter : IPlatformShell
{
    public void ShowToast(string title, string message)
        => Console.WriteLine($"[MOTO] {title} — {message}"); // ou libnotify via P/Invoke

    public void SetWindowIcon(string iconPath) { }

    public bool TryRegisterGlobalHotkey(string combo, Action onTriggered) => false;

    public void AddSystemMenuAbout(Action onAbout) { }
}
