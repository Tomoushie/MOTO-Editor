// Moto.Editor/Views/CollabPanelView.xaml.cs
using System;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Views
{
    public partial class CollabPanelView : ContentView
    {
        /// <summary>Déclenché quand l'utilisateur envoie un message depuis ce panneau.</summary>
        public event Action<string>? ChatSubmitted;

        public CollabPanelView()
        {
            InitializeComponent();
        }

        /// <summary>Ajoute une ligne au journal de chat et défile vers le bas.</summary>
        public void AddChat(string message)
        {
            ChatLog.Children.Add(new Label
            {
                Text = message,
                FontSize = 12,
                TextColor = (Color)Application.Current!.Resources["Txt1"]
            });
            _ = ChatScroll.ScrollToAsync(0, ChatLog.Height, true);
        }

        /// <summary>Met à jour le libellé de présence (ex: "👥 3 en ligne : Tom, ...").</summary>
        public void SetPeers(string summary) => PeersLabel.Text = summary;

        private void OnSendClicked(object? sender, EventArgs e)
        {
            var text = ChatEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            ChatEntry.Text = string.Empty;
            ChatSubmitted?.Invoke(text);
        }
    }
}
