// Moto.Editor/Controls/InlayHintOverlay.cs
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Moto.Core.LSP.InlayHints;
using System.Collections.Generic;

namespace Moto.Editor.Controls
{
    /// <summary>
    /// Overlay qui rend les inlay hints par-dessus l'éditeur.
    /// Couleurs cohérentes avec MotoTheme.xaml.
    /// </summary>
    public sealed class InlayHintOverlay : Grid
    {
        public void RenderHints(IReadOnlyList<InlayHint> hints, double charWidth, double lineHeight)
        {
            Children.Clear();
            if (hints == null) return;

            foreach (var hint in hints)
            {
                var color = hint.Kind switch
                {
                    InlayHintKind.Type => Color.FromArgb("#8B949E"),
                    InlayHintKind.Parameter => Color.FromArgb("#6E7681"),
                    InlayHintKind.ReturnValue => Color.FromArgb("#7D8590"),
                    _ => Color.FromArgb("#8B949E")
                };

                var label = new Label
                {
                    Text = hint.Label,
                    FontSize = 10,
                    FontAttributes = FontAttributes.Italic,
                    TextColor = color,
                    Opacity = 0.85
                };

                var border = new Border
                {
                    BackgroundColor = Color.FromArgb("#33232428"),
                    Stroke = Colors.Transparent,
                    StrokeThickness = 0,
                    Padding = new Thickness(2, 1),
                    StrokeShape = new RoundRectangle { CornerRadius = 3 },
                    Content = label,
                    Margin = new Thickness(hint.Column * charWidth, hint.Line * lineHeight, 0, 0),
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start
                };

                Children.Add(border);
            }
        }
    }
}
