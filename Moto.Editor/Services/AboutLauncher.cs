using System;

namespace Moto.Editor.Services;

/// <summary>Pont statique : hotkey / menu système / palette → MainPage affiche la vue.</summary>
public static class AboutLauncher
{
    public static event Action? ShowRequested;
    public static void RequestShow() => ShowRequested?.Invoke();
}
