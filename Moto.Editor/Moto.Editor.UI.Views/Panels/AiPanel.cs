// UI/Panels/AiPanel.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Moto.Editor.UI.Panels
{
    public class AiSuggestion
    {
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public Action Action { get; set; }
    }

    /// <summary>
    /// Panneau de suggestions IA.
    /// Affiche les propositions de MOTO AI ou de XENO-SSS∞.
    /// </summary>
    public class AiPanel : Panel
    {
        private readonly ListBox _list = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5F),
            BorderStyle = BorderStyle.None
        };

        private readonly Button _applyButton = new Button
        {
            Text = "Apply",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Height = 34
        };

        private readonly Button _dismissButton = new Button
        {
            Text = "Dismiss",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Height = 34
        };

        private readonly TableLayoutPanel _layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };

        public event Action<AiSuggestion> SuggestionApplied;

        public AiPanel()
        {
            Width = 330;
            Dock = DockStyle.Right;
            Padding = new Padding(6);

            _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            _layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));

            _layout.Controls.Add(_list, 0, 0);
            _layout.Controls.Add(_applyButton, 0, 1);
            _layout.Controls.Add(_dismissButton, 0, 2);

            Controls.Add(_layout);

            _applyButton.Click += (s, e) =>
            {
                if (_list.SelectedItem is AiSuggestion suggestion)
                {
                    SuggestionApplied?.Invoke(suggestion);
                    suggestion.Action?.Invoke();
                }
            };

            _dismissButton.Click += (s, e) =>
            {
                _list.Items.Clear();
            };
        }

        public void ShowSuggestions(IEnumerable<AiSuggestion> suggestions)
        {
            _list.Items.Clear();

            foreach (var suggestion in suggestions)
            {
                _list.Items.Add(suggestion);
            }

            _list.DisplayMember = "Title";
        }
    }
}
