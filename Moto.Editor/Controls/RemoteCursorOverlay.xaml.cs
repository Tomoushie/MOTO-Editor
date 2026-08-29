using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Moto.Core.Collab;

namespace Moto.Editor.Controls;

public sealed partial class RemoteCursorOverlay : UserControl
{
    public RemoteCursorOverlay()
    {
        this.InitializeComponent();
    }

    public void UpdateCursors(IEnumerable<RemoteCursor> cursors)
    {
        CursorCanvas.Children.Clear();
        foreach (var cursor in cursors)
        {
            var line = new Rectangle
            {
                Width = 2,
                Height = 18,
                Fill = new SolidColorBrush(cursor.Color),
                Opacity = 0.8
            };
            Canvas.SetLeft(line, cursor.X);
            Canvas.SetTop(line, cursor.Y);
            CursorCanvas.Children.Add(line);

            var label = new TextBlock
            {
                Text = cursor.UserName,
                FontSize = 10,
                Foreground = new SolidColorBrush(cursor.Color),
                Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                Padding = new Microsoft.UI.Xaml.Thickness(2)
            };
            Canvas.SetLeft(label, cursor.X + 4);
            Canvas.SetTop(label, cursor.Y - 14);
            CursorCanvas.Children.Add(label);
        }
    }
}
