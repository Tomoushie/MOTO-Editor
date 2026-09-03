// Moto.Editor/Views/ProactiveActionsView.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.AI.Actions;

namespace Moto.Editor.Views
{
    /// <summary>
    /// Panneau flottant branché sur ContextualActionsEngine.
    /// </summary>
    public partial class ProactiveActionsView : ContentView
    {
        public event Action<string>? ActionSelected;

        // ★ AJOUT (03/09) : fermeture demandée par Tom — voir OnCloseClicked/UpdateActions.
        private bool _dismissed;
        private string _lastShownKey = string.Empty;

        // ★ AJOUT (03/09) : déplacement libre — origine de la translation au début
        // de chaque geste (le pattern standard MAUI : TranslationX/Y cumulés à
        // partir d'un point de départ mémorisé, pas recalculés depuis 0 à chaque
        // frame, sinon le panneau "saute" au début de chaque nouveau glissé).
        private double _dragStartX;
        private double _dragStartY;

        public ProactiveActionsView()
        {
            InitializeComponent();
        }

        private void OnCloseClicked(object? sender, EventArgs e)
        {
            _dismissed = true;
            IsVisible = false;
        }

        private void OnHeaderPanUpdated(object? sender, PanUpdatedEventArgs e)
        {
            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _dragStartX = TranslationX;
                    _dragStartY = TranslationY;
                    break;
                case GestureStatus.Running:
                    TranslationX = _dragStartX + e.TotalX;
                    TranslationY = _dragStartY + e.TotalY;
                    break;
            }
        }

        public void UpdateActions(IReadOnlyList<ContextualAction> actions)
        {
            ActionsList.Children.Clear();

            if (actions == null || actions.Count == 0)
            {
                IsVisible = false;
                return;
            }

            // ★ AJOUT (03/09) : une fermeture manuelle (✕) ne doit pas être aussitôt
            // annulée par le rafraîchissement automatique (RefreshProactiveSuggestions,
            // toutes les 30s, MainPage.Extensions.cs) tant que ce sont EXACTEMENT les
            // mêmes suggestions qu'au moment de la fermeture. Dès que le contexte change
            // (nouvelles suggestions), le panneau redevient visible normalement — Tom
            // n'a besoin de rien faire de spécial pour ça.
            var key = string.Join("|", actions.Select(a => a.Command));
            if (_dismissed && key == _lastShownKey)
            {
                foreach (var action in actions.Take(4))
                    BuildActionRow(action);
                return;
            }

            _dismissed = false;
            _lastShownKey = key;

            foreach (var action in actions.Take(4))
                BuildActionRow(action);

            IsVisible = true;
        }

        private void BuildActionRow(ContextualAction action)
        {
            var button = new Button
            {
                Text = action.Title,
                BackgroundColor = (Color)Application.Current.Resources["BgSide"],
                TextColor = (Color)Application.Current.Resources["Txt1"],
                HorizontalOptions = LayoutOptions.Fill,
                CornerRadius = 8
            };

            button.Clicked += (s, e) => ActionSelected?.Invoke(action.Command);
            ActionsList.Children.Add(button);

            ActionsList.Children.Add(new Label
            {
                Text = action.Description,
                FontSize = 11,
                TextColor = (Color)Application.Current.Resources["Txt2"],
                Margin = new Thickness(8, 0, 0, 4)
            });
        }
    }
}
