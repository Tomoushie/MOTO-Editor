// UI/RoundedTabControl.cs
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Moto.Editor.UI
{
    /// <summary>
    /// Onglets modernisés avec coins arrondis.
    /// </summary>
    public class RoundedTabControl : TabControl
    {
        public RoundedTabControl()
        {
            DrawMode = TabDrawMode.OwnerDrawFixed;
            SizeMode = TabSizeMode.Fixed;
            ItemSize = new Size(150, 30);
            Padding = new Point(10, 6);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                base.OnDrawItem(e);
                return;
            }

            var rect = GetTabRect(e.Index);
            rect.Inflate(-2, -2);

            bool selected = e.Index == SelectedIndex;

            using var path = CreateRoundedRectangle(rect, 10);
            using var brush = new SolidBrush(
                selected
                    ? Color.FromArgb(0, 122, 204)
                    : Color.FromArgb(35, 36, 41)
            );

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);

            var textColor = selected
                ? Color.White
                : Color.FromArgb(220, 222, 226);

            TextRenderer.DrawText(
                e.Graphics,
                TabPages[e.Index].Text,
                Font,
                rect,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            );
        }

        private GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;

            var arc = new Rectangle(rect.X, rect.Y, diameter, diameter);
            path.AddArc(arc, 180, 90);

            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();

            return path;
        }
    }
}
