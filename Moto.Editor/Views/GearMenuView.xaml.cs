// Moto.Editor/Views/GearMenuView.xaml.cs
using System;
using Microsoft.Maui.Controls;
using Moto.Editor.Controls;

namespace Moto.Editor.Views
{
    /// <summary>
    /// Menu déroulant partagé par l'engrenage ⚙ et l'avatar 🙂 de la barre de
    /// titre (voir le commentaire du .xaml). Affiché en overlay par MainPage.
    /// </summary>
    public partial class GearMenuView : ContentView
    {
        /// <summary>
        /// Id choisi : "user","org","settings","keymap","theme","extensions","panellayout","signout".
        /// ★ RETRAIT (31/08, point 14) : "icontheme" fusionné dans "theme" (doublon
        /// repéré par Tom).
        /// </summary>
        public event Action<string>? ItemSelected;

        public GearMenuView()
        {
            InitializeComponent();

            // ★ AJOUT (01/09, direction "Hybride Claude") : aucune des 8 lignes ne
            // réagissait au survol (juste un TapGestureRecognizer sur fond
            // Transparent fixe) — le menu paraissait statique/inerte à l'usage,
            // repéré dans l'audit visuel. Même helper déjà utilisé ailleurs
            // (HomeView, FileExplorerView, AiComposerBarView) plutôt qu'un
            // nouveau mécanisme.
            foreach (var row in new[] { RowUser, RowOrg, RowSettings, RowKeymap, RowTheme, RowExtensions, RowPanelLayout, RowSignOut })
                HoverEffects.Attach(row);
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
