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

        public ProactiveActionsView()
        {
            InitializeComponent();
        }

        public void UpdateActions(IReadOnlyList<ContextualAction> actions)
        {
            ActionsList.Children.Clear();

            if (actions == null || actions.Count == 0)
            {
                IsVisible = false;
                return;
            }

            foreach (var action in actions.Take(4))
            {
                var button = new Button
                {
                    Text = action.Title,
                    BackgroundColor = (Color)Application.Current.Resources["BgSide"],
                    TextColor = (Color)Application.Current.Resources["Txt1"],
                    HorizontalOptions = LayoutOptions.Fill,
                    HorizontalTextAlignment = TextAlignment.Start,
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

            IsVisible = true;
        }
    }
}
