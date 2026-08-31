// CustomMenuBarView.xaml.cs
// ⚠️ FICHIER MORT CONFIRMÉ (31/08) — NE PAS ÉDITER EN PENSANT CORRIGER LA VRAIE
// BARRE DE TITRE. Il existe DEUX classes "CustomMenuBarView" (piège découvert
// après qu'une correction entière — boutons agrandis + survol/clic — ait été
// écrite ICI par erreur et n'ait donc jamais eu le moindre effet visible chez
// Tom). La vraie barre, réellement instanciée par MainPage.xaml (balise
// <views:CustomMenuBarView>, préfixe "views"), est
// Moto.Editor/Views/CustomMenuBarView.xaml(.cs). Vérifié par grep : aucune
// balise XAML nulle part ne référence "controls:CustomMenuBarView" — ce
// fichier n'est atteint par aucun chemin d'exécution. Suppression tentée
// (git rm) mais bloquée par le classificateur de permissions ; conservé tel
// quel, marqué mort, en attendant une suppression manuelle par Tom si voulu.
using System;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Controls
{
    public partial class CustomMenuBarView : ContentView
    {
        private Microsoft.UI.Xaml.Window? NativeWindow =>
            Application.Current?.Windows.Count > 0
                ? Application.Current.Windows[0].Handler?.PlatformView as Microsoft.UI.Xaml.Window
                : null;

        public CustomMenuBarView()
        {
            InitializeComponent();

            BtnMin.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(OnMinClicked) });
            BtnMax.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(OnMaxClicked) });
            BtnClose.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(OnCloseClicked) });

            // ★ AJOUT (31/08) : survol/clic façon Windows par défaut — gris neutre pour
            // Réduire/Plein écran, rouge pour Fermer (demandé par Tom).
            AttachButtonFeedback(BtnMin, hoverColor: Color.FromArgb("#2A2C31"), pressColor: Color.FromArgb("#34363C"));
            AttachButtonFeedback(BtnMax, hoverColor: Color.FromArgb("#2A2C31"), pressColor: Color.FromArgb("#34363C"));
            AttachButtonFeedback(BtnClose, hoverColor: Color.FromArgb("#E81123"), pressColor: Color.FromArgb("#C50E1F"));
        }

        /// <summary>
        /// Colore le fond du bouton au survol/clic. Nos boutons sont des Border/Label
        /// dessinés par MAUI (pas des boutons système), donc Windows ne les recolore
        /// jamais tout seul — il faut le faire nous-mêmes, comme pour les chips
        /// (voir HomeView.AttachChipHover, même patron).
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

        private void OnMinClicked()
        {
#if WINDOWS
            if (NativeWindow?.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
                p.Minimize();
#endif
        }

        private void OnMaxClicked()
        {
#if WINDOWS
            if (NativeWindow?.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
            {
                if (p.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized) p.Restore();
                else p.Maximize();
            }
#endif
        }

        private void OnCloseClicked()
        {
#if WINDOWS
            NativeWindow?.Close();
#endif
        }
    }
}
