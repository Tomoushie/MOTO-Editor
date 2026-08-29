// UI/ModernSidebar.cs
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Moto.Editor.UI
{
    /// <summary>
    /// Barre latérale moderne avec animation légère.
    /// </summary>
    public class ModernSidebar : Panel
    {
        private readonly Timer _animationTimer = new Timer
        {
            Interval = 16
        };

        private int _targetWidth;

        public int ExpandedWidth { get; set; } = 240;
        public int CollapsedWidth { get; set; } = 56;
        public bool IsExpanded { get; private set; } = true;

        public ModernSidebar()
        {
            Dock = DockStyle.Left;
            Width = ExpandedWidth;
            BackColor = Color.FromArgb(23, 24, 28);

            _animationTimer.Tick += AnimationTimer_Tick;
        }

        /// <summary>
        /// Bascule la sidebar entre mode étendu et mode compact.
        /// </summary>
        public void Toggle()
        {
            IsExpanded = !IsExpanded;
            _targetWidth = IsExpanded ? ExpandedWidth : CollapsedWidth;
            _animationTimer.Start();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            int delta = (_targetWidth - Width) / 4;

            if (Math.Abs(delta) < 1)
            {
                Width = _targetWidth;
                _animationTimer.Stop();
                return;
            }

            Width += delta;
        }
    }
}
