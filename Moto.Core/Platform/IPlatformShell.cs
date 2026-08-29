// Moto.Core/Platform/IPlatformShell.cs
using System;

namespace Moto.Core.Platform;

/// <summary>
/// Abstraction des capacités natives par OS.
/// Permet d'ajouter macOS/Linux SANS réécrire le code partagé.
/// </summary>
public interface IPlatformShell
{
    void ShowToast(string title, string message);
    void SetWindowIcon(string iconPath);
    bool TryRegisterGlobalHotkey(string combo, Action onTriggered);
    void AddSystemMenuAbout(Action onAbout);
}
