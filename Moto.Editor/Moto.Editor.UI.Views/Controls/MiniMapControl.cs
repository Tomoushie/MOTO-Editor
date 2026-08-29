// UI/Controls/MiniMapControl.cs
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Moto.Editor.UI.Controls
{
    /// <summary>
    /// Mini-map inspirée de VS Code.
    /// Version légère : dessine une vue réduite du texte source.
    /// </summary>
    public class MiniMapControl : Control
    {
        private RichTextBox _source;
        private readonly Font _miniFont = new Font("Consolas", 4F);

        public MiniMapControl()
        {
            Width = 120;
            Dock = DockStyle.Right;
            BackColor = Color.FromArgb(20, 21, 24);
            ForeColor = Color.FromArgb(180, 184, 190);
        }

        public void Attach(RichTextBox source)
        {
            _source = source;

            if (_source == null)
            {
                return;
            }

            _source.TextChanged += (s, e) => Invalidate();
            _source.VScroll += (s, e) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_source == null)
            {
                return;
            }

            e.Graphics.Clear(BackColor);

            float y = 2F;
            int lineHeight = 6;
            int maxLines = Math.Min(_source.Lines.Length, Height / lineHeight);

            for (int i = 0; i < maxLines; i++)
            {
                var line = _source.Lines[i];

                if (line.Length > 120)
                {
                    line = line.Substring(0, 120);
                }

                using var brush = new SolidBrush(ForeColor);
                e.Graphics.DrawString(line, _miniFont, brush, new PointF(2F, y));

                y += lineHeight;
            }
        }
    }
}
