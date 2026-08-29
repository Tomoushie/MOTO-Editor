// Moto.Editor/Controls/InlayHintsOverlay.cs
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.LSP;

namespace Moto.Editor.Controls
{
    /// <summary>
    /// Overlay qui rend les inlay hints par-dessus l'éditeur.
    /// Positionnés via Grid.Row/Column selon la ligne/colonne.
    /// </summary>
    public sealed class InlayHintsOverlay : Grid
    {
        public void RenderHints(IReadOnlyList<InlayHint> hints, double charWidth, double lineHeight)
        {
            Children.Clear();

            foreach (var hint in hints)
            {
                var label = new Label
                {
                    Text = hint.Label,
                    FontSize = 10,
                    FontAttributes = FontAttributes.Italic,
                    TextColor = hint.Kind switch
                    {
                        InlayHintKind.Type => Color.FromArgb("#8B949E"),
                        InlayHintKind.Parameter => Color.FromArgb("#6E7681"),
                        InlayHintKind.ReturnValue => Color.FromArgb("#7D8590"),
                        _ => Color.FromArgb("#8B949E")
                    },
                    Opacity = 0.8
                };

                var border = new Border
                {
                    BackgroundColor = Color.FromArgb("#1E1F24"),
                    Stroke = Colors.Transparent,
                    StrokeThickness = 0,
                    Padding = new Thickness(2, 1),
                    StrokeShape = new RoundRectangle { CornerRadius = 3 },
                    Content = label
                };

                border.Margin = new Thickness(hint.Column * charWidth, hint.Line * lineHeight, 0, 0);
                border.HorizontalOptions = LayoutOptions.Start;
                border.VerticalOptions = LayoutOptions.Start;

                Children.Add(border);
            }
        }
    }
}
