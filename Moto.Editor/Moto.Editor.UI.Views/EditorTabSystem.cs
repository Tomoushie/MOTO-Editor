// UI/EditorTabSystem.cs
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Moto.Editor.UI
{
    /// <summary>
    /// Système d'onglets de MOTO Editor.
    /// Chaque fichier ouvert devient un onglet contenant un éditeur texte.
    /// </summary>
    public class EditorTabSystem
    {
        private readonly TabControl _tabs;

        public EditorTabSystem(TabControl tabs)
        {
            _tabs = tabs;
        }

        /// <summary>
        /// Ouvre un fichier dans un onglet.
        /// Si le fichier est déjà ouvert, l'onglet existant est sélectionné.
        /// </summary>
        public void OpenFile(string path)
        {
            foreach (TabPage page in _tabs.TabPages)
            {
                if (page.Tag as string == path)
                {
                    _tabs.SelectedTab = page;
                    return;
                }
            }

            var content = File.ReadAllText(path);

            var editor = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Text = content,
                Font = new Font("Consolas", 10F),
                AcceptsTab = true,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.ForcedBoth
            };

            var tabPage = new TabPage(Path.GetFileName(path))
            {
                Tag = path
            };

            tabPage.Controls.Add(editor);
            _tabs.TabPages.Add(tabPage);
            _tabs.SelectedTab = tabPage;
        }

        /// <summary>
        /// Sauvegarde le fichier actif.
        /// </summary>
        public void SaveActive()
        {
            var page = _tabs.SelectedTab;
            if (page == null)
            {
                return;
            }

            var path = page.Tag as string;
            var editor = GetActiveEditor();

            if (string.IsNullOrWhiteSpace(path) || editor == null)
            {
                return;
            }

            File.WriteAllText(path, editor.Text);
            page.Text = Path.GetFileName(path);
        }

        /// <summary>
        /// Retourne le chemin du fichier actif.
        /// </summary>
        public string GetActivePath()
        {
            return _tabs.SelectedTab?.Tag as string;
        }

        /// <summary>
        /// Retourne le texte du fichier actif.
        /// </summary>
        public string GetActiveText()
        {
            return GetActiveEditor()?.Text ?? string.Empty;
        }

        /// <summary>
        /// Ajoute du texte à la fin du document actif.
        /// Utilisé pour les suggestions MOTO AI.
        /// </summary>
        public void AppendToActive(string text)
        {
            var editor = GetActiveEditor();
            if (editor == null)
            {
                return;
            }

            editor.AppendText(text);
        }

        private RichTextBox GetActiveEditor()
        {
            var page = _tabs.SelectedTab;
            if (page == null || page.Controls.Count == 0)
            {
                return null;
            }

            return page.Controls[0] as RichTextBox;
        }
    }
}
