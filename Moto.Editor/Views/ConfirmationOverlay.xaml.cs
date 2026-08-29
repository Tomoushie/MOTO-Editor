// Moto.Editor/Views/ConfirmationOverlay.xaml.cs
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Moto.Core.Settings;

namespace Moto.Editor.Views
{
    /// <summary>
    /// Overlay modal de confirmation pour les actions sensibles de l'IA.
    /// </summary>
    public partial class ConfirmationOverlay : ContentView
    {
        private TaskCompletionSource<bool>? _tcs;

        public ConfirmationOverlay()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Affiche la demande de confirmation et attend la réponse.
        /// </summary>
        public async Task<bool> ShowAsync(ConfirmationRequest request)
        {
            _tcs = new TaskCompletionSource<bool>();

            TitleLabel.Text = request.Title;
            MessageLabel.Text = request.Message;
            DetailsLabel.Text = request.Details;
            ConfirmBtn.Text = request.ConfirmText;
            CancelBtn.Text = request.CancelText;

            // Couleur destructive (rouge) pour les actions irréversibles
            if (request.IsDestructive)
            {
                ConfirmBtn.BackgroundColor = Microsoft.Maui.Graphics.Color.FromArgb("#DC2626");
            }
            else
            {
                ConfirmBtn.BackgroundColor = (Microsoft.Maui.Graphics.Color)Application.Current.Resources["Accent"];
            }

            IsVisible = true;

            return await _tcs.Task;
        }

        private void OnConfirmClicked(object? sender, EventArgs e)
        {
            IsVisible = false;
            _tcs?.TrySetResult(true);
        }

        private void OnCancelClicked(object? sender, EventArgs e)
        {
            IsVisible = false;
            _tcs?.TrySetResult(false);
        }
    }
}
