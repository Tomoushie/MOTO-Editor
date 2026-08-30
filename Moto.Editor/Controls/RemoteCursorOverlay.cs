// Moto.Editor/Controls/RemoteCursorOverlay.cs
// Overlay MAUI qui rend les curseurs distants par-dessus l'éditeur.
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls.Shapes;
using Moto.Core.Collab;

namespace Moto.Editor.Controls
{
    /// <summary>
    /// Overlay qui affiche les curseurs des utilisateurs distants.
    /// Chaque curseur est une ligne verticale colorée + label avec le nom.
    /// </summary>
    public sealed class RemoteCursorOverlay : Grid
    {
        private const double CharWidth = 7.2;
        private const double LineHeight = 18.0;
        private const double CursorWidth = 2.0;

        /// <summary>
        /// Rend les curseurs distants pour le document actuel.
        /// </summary>
        public void RenderCursors(IReadOnlyList<RemoteCursorView> cursors)
        {
            Children.Clear();
            if (cursors == null) return;

            foreach (var cursor in cursors)
            {
                // Ligne verticale du curseur
                var cursorLine = new BoxView
                {
                    Color = ParseColor(cursor.Color),
                    WidthRequest = CursorWidth,
                    Opacity = 0.8
                };

                var leftMargin = cursor.Column * CharWidth;
                var topMargin = cursor.Line * LineHeight;

                cursorLine.Margin = new Thickness(leftMargin, topMargin, 0, 0);
                cursorLine.HorizontalOptions = LayoutOptions.Start;
                cursorLine.VerticalOptions = LayoutOptions.Start;

                Children.Add(cursorLine);

                // Label avec le nom de l'utilisateur
                var nameLabel = new Border
                {
                    BackgroundColor = ParseColor(cursor.Color),
                    Stroke = Colors.Transparent,
                    StrokeThickness = 0,
                    Padding = new Thickness(4, 2),
                    StrokeShape = new RoundRectangle { CornerRadius = 3 }
                };

                nameLabel.Content = new Label
                {
                    Text = cursor.DisplayName,
                    FontSize = 9,
                    TextColor = Colors.White,
                    FontAttributes = FontAttributes.Bold
                };

                nameLabel.Margin = new Thickness(leftMargin, topMargin - 16, 0, 0);
                nameLabel.HorizontalOptions = LayoutOptions.Start;
                nameLabel.VerticalOptions = LayoutOptions.Start;

                Children.Add(nameLabel);
            }
        }

        private static Color ParseColor(string hex)
        {
            try
            {
                if (hex.StartsWith("#"))
                    hex = hex.Substring(1);

                if (hex.Length == 6)
                {
                    var r = Convert.ToByte(hex.Substring(0, 2), 16);
                    var g = Convert.ToByte(hex.Substring(2, 2), 16);
                    var b = Convert.ToByte(hex.Substring(4, 2), 16);
                    return Color.FromRgb(r, g, b);
                }
            }
            catch { }
            return Colors.Orange;
        }
    }
}
