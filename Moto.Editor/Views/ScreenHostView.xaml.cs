// Moto.Editor/Views/ScreenHostView.xaml.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Views
{
    /// <summary>
    /// ★ AJOUT (27/09, option C choisie par Tom) : affiche un écran DANS la fenêtre (voir ScreenHostView.xaml).
    /// Un seul écran à la fois ; chaque ouverture reçoit une instance neuve, relâchée à la fermeture (comme l'ancienne page).
    /// </summary>
    public partial class ScreenHostView : ContentView
    {
        public ScreenHostView()
        {
            InitializeComponent();
        }

        /// <summary>Affiche <paramref name="screen"/> sous ce titre (remplace l'écran déjà ouvert, s'il y en a un).</summary>
        public void Show(string title, View screen)
        {
            TitleLabel.Text = title;
            ContentSlot.Content = screen;
            IsVisible = true;
            App.Breadcrumb($"Écran dans la fenêtre : « {title} » ouvert");
        }

        /// <summary>Ferme l'écran affiché (croix de la barre de titre).</summary>
        public void Close()
        {
            if (!IsVisible) return;
            IsVisible = false;
            ContentSlot.Content = null;
            App.Breadcrumb($"Écran dans la fenêtre : « {TitleLabel.Text} » fermé");
        }

        private void OnCloseClicked(object sender, EventArgs e) => Close();

        // Une ContentView n'a pas de DisplayAlert : les écrans affichés ici passent par la page de leur fenêtre.
        public static Task AlertAsync(VisualElement from, string title, string message, string cancel) =>
            PageFor(from)?.DisplayAlert(title, message, cancel) ?? Task.CompletedTask;

        public static Task<bool> ConfirmAsync(VisualElement from, string title, string message, string accept, string cancel) =>
            PageFor(from)?.DisplayAlert(title, message, accept, cancel) ?? Task.FromResult(false);

        private static Page? PageFor(VisualElement from) =>
            from.Window?.Page ?? Application.Current?.Windows.FirstOrDefault()?.Page;
    }
}
