// UI/Panels/DiagnosticsPanel.cs
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Moto.Editor.UI.Panels
{
    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public class DiagnosticItem
    {
        public DiagnosticSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public int Line { get; set; }
    }

    /// <summary>
    /// Panneau de diagnostics.
    /// Destiné à recevoir les erreurs, warnings et suggestions produits par XENO-SSS∞.
    /// </summary>
    public class DiagnosticsPanel : Panel
    {
        private readonly ListView _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9F)
        };

        public DiagnosticsPanel()
        {
            Height = 190;
            Dock = DockStyle.Bottom;

            _list.Columns.Add("Severity", 90);
            _list.Columns.Add("Message", 480);
            _list.Columns.Add("File", 260);
            _list.Columns.Add("Line", 60);

            Controls.Add(_list);
        }

        public void SetDiagnostics(IEnumerable<DiagnosticItem> diagnostics)
        {
            _list.Items.Clear();

            foreach (var diagnostic in diagnostics)
            {
                var item = new ListViewItem(diagnostic.Severity.ToString());
                item.SubItems.Add(diagnostic.Message);
                item.SubItems.Add(diagnostic.File);
                item.SubItems.Add(diagnostic.Line.ToString());

                item.ForeColor = diagnostic.Severity switch
                {
                    DiagnosticSeverity.Error => Color.OrangeRed,
                    DiagnosticSeverity.Warning => Color.Goldenrod,
                    _ => Color.Gray
                };

                _list.Items.Add(item);
            }
        }
    }
}
