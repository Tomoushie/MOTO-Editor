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

            // ★ CORRECTION (31/08) : survol/clic façon Windows par défaut — gris
            // neutre pour Réduire/Plein écran, rouge pour Fermer. Demandé par Tom ;
            // une première tentative avait été écrite dans Controls/CustomMenuBarView
            // (fichier homonyme jamais instancié) et n'avait donc jamais été visible.
            AttachButtonFeedback(BtnMin, hoverColor: Color.FromArgb("#2A2C31"), pressColor: Color.FromArgb("#34363C"));
            AttachButtonFeedback(BtnMax, hoverColor: Color.FromArgb("#2A2C31"), pressColor: Color.FromArgb("#34363C"));
            AttachButtonFeedback(BtnClose, hoverColor: Color.FromArgb("#E81123"), pressColor: Color.FromArgb("#C50E1F"));
        }

        /// <summary>
        /// Colore le fond du bouton au survol/clic. Nos boutons sont des Border/Label
        /// dessinés par MAUI (pas des boutons système), donc Windows ne les recolore
        /// jamais tout seul — il faut le faire nous-mêmes (même patron que
        /// HomeView.AttachChipHover).
        /// </summary>
        private static void AttachButtonFeedback(Border button, Color hoverColor, Color pressColor)
        {
            var pointer = new PointerGestureRecognizer();
            pointer.PointerEntered += (_, _) => button.BackgroundColor = hoverColor;
            pointer.PointerExited += (_, _) => button.BackgroundColor = Colors.Transparent;
            pointer.PointerPressed += (_, _) => button.BackgroundColor = pressColor;
            pointer.PointerReleased += (_, _) => button.BackgroundColor = hoverColor;
            button.GestureRecognizers.Add(pointer);
        }

        /// <summary>
        /// ★ AJOUT (31/08) : engrenage (point 1) et avatar (point 11) ouvrent le même
        /// petit menu déroulant — un seul id d'événement, MainPage bascule
        /// GearMenu.IsVisible (voir MainPage.Routing.cs, OnMenuCommanded).
        /// </summary>
        private void OnGearOrAvatarTapped(object? sender, TappedEventArgs e) => MenuCommanded?.Invoke("gear.toggle");

        /// <summary>
        /// ★ AJOUT (31/08) : Fichiers/Recherche/IA/Cortex/Collab, rapatriés ici depuis
        /// ActivityBarView (retirée de MainPage.xaml — Tom veut tout sur une seule
        /// ligne). Même id qu'avant ("explorer","search","ai","cortex","collab") ;
        /// MainPage.Routing.cs (OnMenuCommanded) les route vers OnActivitySelected,
        /// code de panneau inchangé.
        /// </summary>
        private void OnNavTapped(object? sender, TappedEventArgs e)
        {
            if (e.Parameter is string id)
                MenuCommanded?.Invoke(id);
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
