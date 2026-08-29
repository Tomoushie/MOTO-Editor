// UI/Editor/SyntaxRichTextBox.cs
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Moto.Editor.UI.Editor
{
    /// <summary>
    /// Éditeur texte amélioré avec coloration syntaxique différée.
    /// Le timer évite de relancer la coloration à chaque frappe.
    /// </summary>
    public class SyntaxRichTextBox : RichTextBox
    {
        private readonly Timer _highlightTimer = new Timer();
        private readonly SyntaxHighlighter _highlighter = new SyntaxHighlighter();
        private bool _applyingHighlight;

        /// <summary>
        /// Chemin du fichier ouvert dans cet éditeur.
        /// </summary>
        public string FilePath { get; set; }

        public SyntaxRichTextBox()
        {
            Font = new Font("Consolas", 10F);
            AcceptsTab = true;
            WordWrap = false;
            ScrollBars = RichTextBoxScrollBars.ForcedBoth;

            _highlightTimer.Interval = 300;
            _highlightTimer.Tick += (s, e) =>
            {
                _highlightTimer.Stop();

                if (!_applyingHighlight)
                {
                    ApplyHighlight();
                }
            };
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);

            if (!_applyingHighlight)
            {
                _highlightTimer.Start();
            }
        }

        public void ApplyHighlight()
        {
            _applyingHighlight = true;

            try
            {
                _highlighter.Apply(this, FilePath);
            }
            finally
            {
                _applyingHighlight = false;
            }
        }
    }
}
