// UI/Modern/AnimatedSidebar.cs
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Moto.Editor.UI.Modern
{
    /// <summary>
    /// Barre latérale animée.
    /// Animation légère via Timer, sans dépendance externe.
    /// </summary>
    public class AnimatedSidebar : Panel
    {
        private readonly Timer _animationTimer = new Timer
        {
            Interval = 16
        };

        private int _targetWidth;

        /// <summary>
        /// Largeur lorsque la sidebar est ouverte.
        /// </summary>
        public int ExpandedWidth { get; set; } = 240;

        /// <summary>
        /// Largeur lorsque la sidebar est réduite.
        /// </summary>
        public int CollapsedWidth { get; set; } = 52;

        /// <summary>
        /// État courant de la sidebar.
        /// </summary>
        public bool IsExpanded { get; private set; } = true;

        public AnimatedSidebar()
        {
            Width = ExpandedWidth;
            Dock = DockStyle.Left;
            BackColor = Color.FromArgb(23, 24, 28);

            _animationTimer.Tick += AnimationTimer_Tick;
        }

        /// <summary>
        /// Bascule l'état ouvert / réduit.
        /// </summary>
        public void Toggle()
        {
            IsExpanded = !IsExpanded;
            _targetWidth = IsExpanded ? ExpandedWidth : CollapsedWidth;
            _animationTimer.Start();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            // Interpolation simple pour une animation fluide mais légère.
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
