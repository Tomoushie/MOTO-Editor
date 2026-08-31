// Moto.Editor/Controls/ExecutionLocationMenu.xaml.cs
using System;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Controls
{
    /// <summary>
    /// Menu custom "Emplacement d'exécution" (Local/Cloud/Contrôle à distance/WSL/SSH),
    /// affiché en overlay centré par MainPage. Voir le commentaire du .xaml.
    /// </summary>
    public partial class ExecutionLocationMenu : ContentView
    {
        /// <summary>Choix effectué : "local", "cloud", "remote", "wsl" ou "ssh".</summary>
        public event Action<string>? LocationSelected;

        public ExecutionLocationMenu()
        {
            InitializeComponent();
        }

        private void OnCloseClicked(object sender, EventArgs e) => IsVisible = false;

        private void OnRowTapped(object sender, EventArgs e)
        {
            var id = sender switch
            {
                var s when s == RowLocal => "local",
                var s when s == RowCloud => "cloud",
                var s when s == RowRemote => "remote",
                var s when s == RowWsl => "wsl",
                var s when s == RowSsh => "ssh",
                _ => null
            };

            if (id != null)
            {
                // ★ AJOUT (31/08) : le menu se ferme lui-même désormais — auparavant
                // c'était MainPage qui le fermait après coup ; depuis que ce menu vit
                // dans HomeView (point 1 de Tom), c'est plus simple qu'il gère sa
                // propre fermeture, quel que soit le futur écouteur.
                IsVisible = false;
                LocationSelected?.Invoke(id);
            }
        }
    }
}
