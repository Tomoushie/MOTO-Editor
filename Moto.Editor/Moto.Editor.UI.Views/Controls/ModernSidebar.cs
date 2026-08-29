// UI/Controls/ModernSidebar.cs
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Moto.Editor.UI.Controls
{
    /// <summary>
    /// Barre latérale moderne, simple et épurée.
    /// </summary>
    public class ModernSidebar : Panel
    {
        private readonly Label _title = new Label
        {
            Text = "MOTO",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Padding = new Padding(8, 0, 0, 0)
        };

        private readonly FlowLayoutPanel _actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(6)
        };

        private readonly TableLayoutPanel _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1
        };

        public ModernSidebar()
        {
            Width = 250;
            Dock = DockStyle.Left;
            Padding = new Padding(8);

            _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _layout.Controls.Add(_title, 0, 0);
            _layout.Controls.Add(_actions, 0, 1);

            Controls.Add(_layout);
        }

        public void SetTitle(string title)
        {
            _title.Text = title;
        }

        /// <summary>
        /// Ajoute une action dans la sidebar.
        /// Exemple : ouvrir un dossier, lancer XENO, valider le projet.
        /// </summary>
        public void AddAction(string text, Action onClick)
        {
            var button = new Button
            {
                Text = text,
                Width = 220,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Margin = new Padding(0, 2, 0, 2)
            };

            button.FlatAppearance.BorderSize = 0;
            button.Click += (s, e) => onClick?.Invoke();

            _actions.Controls.Add(button);
        }
    }
}
