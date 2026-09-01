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

            // ★ AJOUT (01/09, direction "Hybride Claude") : les 5 lignes n'avaient
            // aucun retour visuel au survol (même défaut que GearMenuView, voir
            // l'audit visuel) — même helper déjà utilisé ailleurs dans le projet.
            foreach (var row in new[] { RowLocal, RowCloud, RowRemote, RowWsl, RowSsh })
                HoverEffects.Attach(row);
        }

        // ★ CORRECTION (01/09, vraie cause trouvée) : IsVisible=false remplacé par
        // Opacity=0 + InputTransparent=true partout dans ce fichier. Un contrôle qui
        // démarre avec IsVisible=false (voir HomeView.xaml) ne se remesure jamais
        // correctement même en repassant IsVisible à true — défaut documenté de
        // .NET MAUI (dotnet/maui#9850, #28677, #8185). En ne touchant plus jamais
        // IsVisible (jamais false, même en interne ici), le contrôle reste
        // "monté"/mesuré normalement en permanence.
        private void OnCloseClicked(object sender, EventArgs e)
        {
            Opacity = 0;
            InputTransparent = true;
        }

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
                Opacity = 0;
                InputTransparent = true;
                LocationSelected?.Invoke(id);
            }
        }
    }
}
