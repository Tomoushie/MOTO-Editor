// Moto.Editor/Views/Sidebar/SidebarView.xaml.cs
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Moto.Editor.Views
{
    // Corrigé : ce fichier déclarait "Moto.Editor.Views.Sidebar" alors que
    // SidebarView.xaml déclare x:Class="Moto.Editor.Views.SidebarView" —
    // le code-behind ne correspondait donc jamais réellement au XAML.
    public partial class SidebarView : ContentView
    {
        public event Action<string> ThreadSelected;

        /// <summary>
        /// Déclenché après un drag & drop réussi d'une session entre sections.
        /// Paramètres : (sessionId, targetSectionKey) où targetSectionKey ∈ {"pinned","projects","recent"}.
        /// </summary>
        public event Action<string, string> SessionMoved;

        /// <summary>Déclenché quand l'utilisateur clique "＋ Nouveau chat".</summary>
        public event Action? NewChatRequested;

        public SidebarView()
        {
            InitializeComponent();
        }

        private void OnNewChat(object sender, EventArgs e) => NewChatRequested?.Invoke();

        /// <summary>Remplit les listes de la sidebar et attache les gestionnaires de drag/drop.</summary>
        public void Refresh(List<string> pinned, List<string> projects, List<string> recents)
        {
            Fill(PinnedList, pinned, "pinned");
            Fill(ProjectsList, projects, "projects");
            Fill(RecentsList, recents, "recent");
        }

        private void Fill(VerticalStackLayout host, List<string> items, string sectionName)
        {
            host.Children.Clear();

            // ── Cible de drop sur la section ──
            // (AllowDrop n'existe pas côté MAUI : ajouter un DropGestureRecognizer suffit.)
            var drop = new DropGestureRecognizer();
            drop.Drop += async (s, e) =>
            {
                var name = e.Data != null ? await e.Data.GetTextAsync() : null;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    // Invocation du contrat pour remonter l'info au ViewModel/Parent
                    SessionMoved?.Invoke(name, sectionName);
                }
                e.Handled = true;
            };
            host.GestureRecognizers.Add(drop);

            foreach (var item in items)
            {
                var label = new Label
                {
                    Text = "📁 " + item,
                    FontSize = 12,
                    TextColor = (Color)Application.Current.Resources["Txt1"],
                    Padding = new Thickness(4, 4)
                };

                var tap = new TapGestureRecognizer();
                tap.Tapped += (s, e) => ThreadSelected?.Invoke(item);
                label.GestureRecognizers.Add(tap);

                // ── Source de drag : la session se déplace ──
                var drag = new DragGestureRecognizer();
                drag.DragStarting += (s, e) =>
                {
                    e.Data.Text = item;
                    e.Data.Properties["session"] = item;
                };
                label.GestureRecognizers.Add(drag);

                host.Children.Add(label);
            }
        }
    }
}
