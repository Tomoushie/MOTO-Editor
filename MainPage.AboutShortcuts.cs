using Microsoft.Maui.Controls;
using Moto.Editor.Services;
using Moto.Editor.Views;

namespace Moto.Editor;

public partial class MainPage
{
    /// <summary>À appeler une fois dans le constructeur (après HookAboutCommand).</summary>
    private void HookAboutShortcuts()
    {
        AboutLauncher.ShowRequested += () =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var about = Handler?.MauiContext?.Services.GetService(typeof(AboutView)) as AboutView;
                if (about != null) ShowInOverlay(about); // méthode d'overlay existante
            });
    }
}
