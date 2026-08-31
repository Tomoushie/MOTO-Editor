// Moto.Editor/Views/GearMenuView.xaml.cs
using System;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Views
{
    /// <summary>
    /// Menu déroulant partagé par l'engrenage ⚙ et l'avatar 🙂 de la barre de
    /// titre (voir le commentaire du .xaml). Affiché en overlay par MainPage.
    /// </summary>
    public partial class GearMenuView : ContentView
    {
        /// <summary>Id choisi : "user","org","settings","keymap","theme","icontheme","extensions","panellayout","signout".</summary>
        public event Action<string>? ItemSelected;

        public GearMenuView()
        {
            InitializeComponent();
        }

        private void OnRowTapped(object sender, EventArgs e)
        {
            var id = sender switch
            {
                var s when s == RowUser => "user",
                var s when s == RowOrg => "org",
                var s when s == RowSettings => "settings",
                var s when s == RowKeymap => "keymap",
                var s when s == RowTheme => "theme",
                var s when s == RowIconTheme => "icontheme",
                var s when s == RowExtensions => "extensions",
                var s when s == RowPanelLayout => "panellayout",
                var s when s == RowSignOut => "signout",
                _ => null
            };

            if (id != null)
                ItemSelected?.Invoke(id);
        }
    }
}
