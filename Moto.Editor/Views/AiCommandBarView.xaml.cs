// Moto.Editor/Views/AiCommandBarView.xaml.cs
using System;
using Microsoft.Maui.Controls;

namespace Moto.Editor.Views
{
    /// <summary>
    /// Barre de commande IA flottante.
    /// S'ouvre avec CTRL+SHIFT+I, un bouton, ou l'activation de la fenêtre.
    /// </summary>
    public partial class AiCommandBarView : ContentView
    {
        /// <summary>Déclenché quand l'utilisateur envoie une demande.</summary>
        public event Action<string> Submitted;

        public AiCommandBarView()
        {
            InitializeComponent();
        }

        /// <summary>Affiche la barre et donne le focus au champ.</summary>
        public void Show()
        {
            IsVisible = true;
            InputEntry.Focus();
        }

        /// <summary>Masque la barre.</summary>
        public void Hide()
        {
            IsVisible = false;
        }

        /// <summary>Bascule affichage.</summary>
        public void Toggle()
        {
            if (IsVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        /// <summary>État occupé pendant le traitement IA.</summary>
        public void SetBusy(bool busy)
        {
            InputEntry.IsEnabled = !busy;
            InputEntry.Placeholder = busy
                ? "MOTO AI réfléchit..."
                : "Comment puis-je vous aider aujourd'hui ?";
        }

        private void OnSubmitted(object sender, EventArgs e)
        {
            var text = InputEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            InputEntry.Text = string.Empty;
            Submitted?.Invoke(text);
        }
    }
}
