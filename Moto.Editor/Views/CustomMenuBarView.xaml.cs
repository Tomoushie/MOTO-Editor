// Moto.Editor/Views/CustomMenuBarView.xaml.cs
// Barre de titre custom (utilisée par MainPage : x:Name="MenuBar").
// Combine deux morceaux qui existaient séparément avant correction :
// - la gestion de la mise à jour (SetUpdateManager/UpdateRequested), déjà écrite ici,
// - les boutons fenêtre (min/max/close), copiés depuis Controls/CustomMenuBarView.xaml.cs
//   (variante non utilisée, conservée telle quelle).
using System;
using Microsoft.Maui.Controls;
using Moto.Core.Updates;

namespace Moto.Editor.Views
{
    public partial class CustomMenuBarView : ContentView
    {
        private UpdateManager? _updateManager;
        private UpdateInfo? _pendingUpdate;

        public event Action? UpdateRequested;

        /// <summary>
        /// Déclenché par une commande de menu (ex: "file.save", "view.terminal").
        /// Aucun menu déroulant (Fichier/Édition/...) n'existe encore dans cette
        /// barre de titre : l'évènement est câblé par MainPage (MenuBar.MenuCommanded += ...)
        /// mais rien ne l'émet pour l'instant — point d'extension pour une future
        /// vraie barre de menus.
        /// </summary>
        public event Action<string>? MenuCommanded;

        private Microsoft.UI.Xaml.Window? NativeWindow =>
#if WINDOWS
            Application.Current?.Windows.Count > 0
                ? Application.Current.Windows[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window
                : null;
#else
            null;
#endif

        public CustomMenuBarView()
        {
            InitializeComponent();
        }

        public void SetUpdateManager(UpdateManager manager)
        {
            _updateManager = manager;
            _updateManager.UpdateAvailable += OnUpdateAvailable;
        }

        private void OnUpdateAvailable(UpdateInfo update)
        {
            _pendingUpdate = update;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateBadge.IsVisible = true;
                UpdateLabel.Text = $"v{update.Version}";
            });
        }

        private async void OnUpdateClicked(object? sender, EventArgs e)
        {
            if (_pendingUpdate == null) return;

            var confirmed = await Application.Current!.MainPage!.DisplayAlert(
                "Mise à jour disponible",
                $"Version {_pendingUpdate.Version} est disponible.\n\n" +
                $"{_pendingUpdate.Changelog}\n\n" +
                "Un redémarrage sera nécessaire.",
                "Télécharger",
                "Plus tard");

            if (confirmed)
            {
                UpdateRequested?.Invoke();
            }
        }

        private void OnMinClicked(object? sender, EventArgs e)
        {
#if WINDOWS
            if (NativeWindow?.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
                p.Minimize();
#endif
        }

        private void OnMaxClicked(object? sender, EventArgs e)
        {
#if WINDOWS
            if (NativeWindow?.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
            {
                if (p.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized) p.Restore();
                else p.Maximize();
            }
#endif
        }

        private void OnCloseClicked(object? sender, EventArgs e)
        {
#if WINDOWS
            NativeWindow?.Close();
#endif
        }
    }
}
