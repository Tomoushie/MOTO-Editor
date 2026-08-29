// UI/Controls/AutocompletePopup.cs
using System.Drawing;
using System.Windows.Forms;

namespace Moto.Editor.UI.Controls
{
    /// <summary>
    /// Popup d'autocomplétion temps réel.
    /// </summary>
    public class AutocompletePopup : ListBox
    {
        public AutocompletePopup()
        {
            Visible = false;
            BorderStyle = BorderStyle.FixedSingle;
            Font = new Font("Consolas", 9.5F);
            BackColor = Color.FromArgb(30, 31, 35);
            ForeColor = Color.FromArgb(230, 232, 236);
        }

        /// <summary>
        /// Affiche la popup près du curseur.
        /// </summary>
        public void ShowNearCaret(RichTextBox editor, int wordStart, System.Collections.Generic.IReadOnlyList<AI.AutocompleteItem> items)
        {
            Items.Clear();

            if (items == null || items.Count == 0)
            {
                Hide();
                return;
            }

            foreach (var item in items)
            {
                Items.Add(item.DisplayText);
            }

            var position = editor.GetPositionFromCharIndex(editor.SelectionStart);

            Location = new Point(position.X + 2, position.Y + 22);
            Width = 300;
            Height = System.Math.Min(200, Items.Count * 20);

            Tag = wordStart;
            Show();
        }

        /// <summary>
        /// Applique la complétion sélectionnée.
        /// </summary>
        public bool TryApply(RichTextBox editor)
        {
            if (!Visible || SelectedIndex < 0 || Tag is not int wordStart)
            {
                return false;
            }

            var insertText = SelectedItem.ToString();

            editor.Select(wordStart, editor.SelectionStart - wordStart);
            editor.SelectedText = insertText;

            Hide();
            return true;
        }
    }
}
