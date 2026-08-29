// Moto.Editor/Controls/BreakpointGutterOverlay.cs
// Overlay qui rend les breakpoints dans le gutter de l'éditeur.
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.Debug;

namespace Moto.Editor.Controls
{
    /// <summary>
    /// Overlay qui affiche les icônes de breakpoints dans le gutter.
    /// </summary>
    public sealed class BreakpointGutterOverlay : Grid
    {
        private const double LineHeight = 18.0;
        private const double GutterWidth = 30.0;
        private const double IconSize = 12.0;

        /// <summary>Déclenché quand l'utilisateur clique sur une ligne du gutter.</summary>
        public event Action<int>? BreakpointToggleRequested;

        /// <summary>
        /// Rend les breakpoints pour le document actuel.
        /// </summary>
        public void RenderBreakpoints(IReadOnlyList<BreakpointInfo> breakpoints, int totalLines)
        {
            Children.Clear();

            // Zone cliquable pour chaque ligne
            for (int line = 0; line < totalLines; line++)
            {
                var clickZone = new BoxView
                {
                    Color = Colors.Transparent,
                    WidthRequest = GutterWidth,
                    HeightRequest = LineHeight,
                    Margin = new Thickness(0, line * LineHeight, 0, 0),
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start
                };

                var tapGesture = new TapGestureRecognizer();
                var lineNumber = line; // Capture pour le closure
                tapGesture.Tapped += (s, e) => BreakpointToggleRequested?.Invoke(lineNumber);
                clickZone.GestureRecognizers.Add(tapGesture);

                Children.Add(clickZone);
            }

            // Icônes de breakpoints
            foreach (var bp in breakpoints)
            {
                var icon = new Border
                {
                    WidthRequest = IconSize,
                    HeightRequest = IconSize,
                    StrokeShape = new Ellipse(),
                    Stroke = Colors.Transparent,
                    StrokeThickness = 0,
                    Margin = new Thickness(
                        (GutterWidth - IconSize) / 2,
                        bp.Line * LineHeight + (LineHeight - IconSize) / 2,
                        0, 0),
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start
                };

                // Couleur selon l'état
                icon.BackgroundColor = bp.Enabled
                    ? (bp.Verified ? Color.FromArgb("#DC2626") : Color.FromArgb("#9CA3AF"))
                    : Color.FromArgb("#6B7280");

                // Tooltip avec la condition si présente
                if (!string.IsNullOrWhiteSpace(bp.Condition))
                {
                    icon.ToolTip = $"Condition : {bp.Condition}";
                }

                Children.Add(icon);

                // Badge du compteur de hits
                if (bp.HitCount > 0)
                {
                    var badge = new Border
                    {
                        BackgroundColor = Color.FromArgb("#F59E0B"),
                        Stroke = Colors.Transparent,
                        StrokeThickness = 0,
                        Padding = new Thickness(2, 1),
                        StrokeShape = new RoundRectangle { CornerRadius = 6 },
                        Margin = new Thickness(
                            GutterWidth - 16,
                            bp.Line * LineHeight + 2,
                            0, 0),
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.Start
                    };

                    badge.Content = new Label
                    {
                        Text = bp.HitCount.ToString(),
                        FontSize = 8,
                        TextColor = Colors.White,
                        FontAttributes = FontAttributes.Bold
                    };

                    Children.Add(badge);
                }
            }
        }
    }
}
