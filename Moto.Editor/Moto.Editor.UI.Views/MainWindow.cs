// UI/MainWindow.cs
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Moto.Editor.AI;
using Moto.Editor.Core;
using Moto.Editor.Integration;
using Moto.Editor.Terminal;

namespace Moto.Editor.UI
{
    /// <summary>
    /// Fenêtre principale de MOTO Editor.
    /// Interface volontairement épurée :
    /// - explorateur de fichiers à gauche ;
    /// - onglets au centre ;
    /// - terminal en bas ;
    /// - commandes via Ctrl+Shift+P.
    /// </summary>
    public class MainWindow : Form
    {
        private readonly Workspace _workspace = new Workspace();
        private readonly CommandSystem _commands = new CommandSystem();
        private readonly OllamaClient _ollama = new OllamaClient();
        private readonly HttpXenoClient _xeno = new HttpXenoClient();

        private MotoAi _ai;
        private EditorTabSystem _tabs;
        private TerminalHost _terminal;

        private SplitContainer _editorTerminalSplit;
        private TreeView _fileTree;
        private TabControl _tabControl;
        private TextBox _terminalOutput;
        private TextBox _terminalInput;
        private ToolStripStatusLabel _statusLabel;

        public MainWindow()
        {
            Text = "MOTO Editor";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            Font = new Font("Segoe UI", 9F);

            InitializeLayout();

            _tabs = new EditorTabSystem(_tabControl);
            _terminal = new TerminalHost(AppendTerminal);
            _ai = new MotoAi(_ollama, _xeno);

            _ai.SuggestionReady += suggestion => SetStatus(suggestion);

            InitializeCommands();

            Load += (s, e) =>
            {
                try
                {
                    _terminal.Start();
                }
                catch (Exception ex)
                {
                    AppendTerminal($"[terminal] start error: {ex.Message}");
                }
            };

            FormClosed += (s, e) =>
            {
                _terminal.Stop();
            };
        }

        private void InitializeLayout()
        {
            var menu = CreateMenu();

            var rootSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 260,
                BorderStyle = BorderStyle.None
            };

            _fileTree = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                ShowLines = true
            };

            _fileTree.NodeMouseDoubleClick += FileTree_NodeMouseDoubleClick;

            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };

            _editorTerminalSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 420,
                BorderStyle = BorderStyle.None
            };

            _editorTerminalSplit.Panel1.Controls.Add(_tabControl);

            var terminalLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                BackColor = Color.Black
            };

            terminalLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 75F));
            terminalLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));

            _terminalOutput = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                Font = new Font("Consolas", 9.75F),
                WordWrap = false
            };

            _terminalInput = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.White,
                Font = new Font("Consolas", 9.75F)
            };

            _terminalInput.KeyDown += TerminalInput_KeyDown;

            terminalLayout.Controls.Add(_terminalOutput, 0, 0);
            terminalLayout.Controls.Add(_terminalInput, 0, 1);

            _editorTerminalSplit.Panel2.Controls.Add(terminalLayout);

            rootSplit.Panel1.Controls.Add(_fileTree);
            rootSplit.Panel2.Controls.Add(_editorTerminalSplit);

            var status = new StatusStrip();

            _statusLabel = new ToolStripStatusLabel("MOTO prêt. Ctrl+Shift+P = commandes.");
            status.Items.Add(_statusLabel);

            Controls.Add(rootSplit);
            Controls.Add(status);
            Controls.Add(menu);

            MainMenuStrip = menu;
        }

        private MenuStrip CreateMenu()
        {
            var menu = new MenuStrip();

            var file = new ToolStripMenuItem("&File");
            file.DropDownItems.Add("Open Folder...", null, (s, e) => OpenFolderDialog());
            file.DropDownItems.Add("Save", null, (s, e) => _tabs?.SaveActive());

            var ai = new ToolStripMenuItem("&AI");
            ai.DropDownItems.Add("Complete active document", null, async (s, e) => await CompleteWithOllamaAsync());
            ai.DropDownItems.Add("Run XENO pipeline", null, async (s, e) => await RunXenoAsync());

            var terminal = new ToolStripMenuItem("&Terminal");
            terminal.DropDownItems.Add("Toggle terminal", null, (s, e) => ToggleTerminal());

            menu.Items.Add(file);
            menu.Items.Add(ai);
            menu.Items.Add(terminal);

            return menu;
        }

        private void InitializeCommands()
        {
            _commands.Register("File: Open Folder", OpenFolderDialog);
            _commands.Register("File: Save", () => _tabs.SaveActive());
            _commands.Register("Terminal: Toggle", ToggleTerminal);
            _commands.Register("Ollama: Test connection", async () => await TestOllamaAsync());
            _commands.Register("XENO: Test connection", async () => await TestXenoAsync());
            _commands.Register("XENO: Run full pipeline", async () => await RunXenoAsync());
            _commands.Register("MOTO: Complete active document", async () => await CompleteWithOllamaAsync());
            _commands.Register("MOTO: Show command palette", ShowCommandPalette);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Ctrl+O : ouvrir un workspace
            if (keyData == (Keys.Control | Keys.O))
            {
                OpenFolderDialog();
                return true;
            }

            // Ctrl+S : sauvegarder le fichier actif
            if (keyData == (Keys.Control | Keys.S))
            {
                _tabs.SaveActive();
                return true;
            }

            // Ctrl+Shift+P : palette de commandes
            if (keyData == (Keys.Control | Keys.Shift | Keys.P))
            {
                ShowCommandPalette();
                return true;
            }

            // Ctrl+` : terminal
            if (keyData == (Keys.Control | Keys.Oemtilde))
            {
                ToggleTerminal();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OpenFolderDialog()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Ouvre un workspace (dossier projet).",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _workspace.OpenFolder(dialog.SelectedPath);
                LoadFileTree(dialog.SelectedPath);
                SetStatus($"Workspace ouvert: {dialog.SelectedPath}");
            }
        }

        private void LoadFileTree(string rootPath)
        {
            _fileTree.Nodes.Clear();

            var rootInfo = new DirectoryInfo(rootPath);
            var rootNode = new TreeNode(rootInfo.Name)
            {
                Tag = rootInfo.FullName
            };

            _fileTree.Nodes.Add(rootNode);
            AddDirectoryNodes(rootNode, rootInfo, 0);
            rootNode.Expand();
        }

        private void AddDirectoryNodes(TreeNode parent, DirectoryInfo dir, int depth)
        {
            const int maxDepth = 6;

            if (depth >= maxDepth)
            {
                return;
            }

            try
            {
                foreach (var subDir in dir.GetDirectories())
                {
                    if (subDir.Name.StartsWith(".") ||
                        subDir.Name == "bin" ||
                        subDir.Name == "obj" ||
                        subDir.Name == "node_modules")
                    {
                        continue;
                    }

                    var dirNode = new TreeNode(subDir.Name)
                    {
                        Tag = subDir.FullName
                    };

                    parent.Nodes.Add(dirNode);
                    AddDirectoryNodes(dirNode, subDir, depth + 1);
                }

                foreach (var file in dir.GetFiles())
                {
                    if (file.Name.StartsWith("."))
                    {
                        continue;
                    }

                    parent.Nodes.Add(new TreeNode(file.Name)
                    {
                        Tag = file.FullName
                    });
                }
            }
            catch (Exception ex)
            {
                SetStatus($"File tree error: {ex.Message}");
            }
        }

        private void FileTree_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            var path = e.Node.Tag as string;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            _tabs.OpenFile(path);
            _ai.RecordFile(path);
            SetStatus($"Ouvert: {path}");
        }

        private void TerminalInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            var command = _terminalInput.Text.Trim();

            if (!string.IsNullOrWhiteSpace(command))
            {
                AppendTerminal($"> {command}");
                _terminal.SendInput(command);
                _ai.RecordCommand(command);
            }

            _terminalInput.Clear();
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        private void ToggleTerminal()
        {
            _editorTerminalSplit.Panel2Collapsed = !_editorTerminalSplit.Panel2Collapsed;
        }

        private void ShowCommandPalette()
        {
            using var palette = new Form
            {
                Text = "MOTO Command Palette",
                Width = 540,
                Height = 320,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var input = new TextBox
            {
                Dock = DockStyle.Top,
                Font = new Font("Segoe UI", 10F),
                PlaceholderText = "Tape une commande puis Entrée..."
            };

            var list = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.75F)
            };

            foreach (var command in _commands.Names)
            {
                list.Items.Add(command);
            }

            input.TextChanged += (s, e) =>
            {
                list.Items.Clear();

                foreach (var command in _commands.Names)
                {
                    if (command.Contains(input.Text, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Items.Add(command);
                    }
                }
            };

            input.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && list.SelectedItem != null)
                {
                    var command = list.SelectedItem.ToString();
                    palette.Close();
                    _commands.TryExecute(command);
                }
            };

            list.MouseDoubleClick += (s, e) =>
            {
                if (list.SelectedItem != null)
                {
                    var command = list.SelectedItem.ToString();
                    palette.Close();
                    _commands.TryExecute(command);
                }
            };

            palette.Controls.Add(list);
            palette.Controls.Add(input);

            palette.ShowDialog(this);
        }

        private async Task TestOllamaAsync()
        {
            try
            {
                SetStatus("Ollama: test en cours...");
                await _ollama.GenerateAsync("Réponds uniquement: pong.");
                SetStatus("Ollama: connexion OK.");
            }
            catch (Exception ex)
            {
                SetStatus($"Ollama erreur: {ex.Message}");
            }
        }

        private async Task TestXenoAsync()
        {
            try
            {
                SetStatus("XENO: test en cours...");
                await _xeno.PingAsync();
                SetStatus("XENO: connexion OK.");
            }
            catch (Exception ex)
            {
                SetStatus($"XENO erreur: {ex.Message}");
            }
        }

        private async Task RunXenoAsync()
        {
            if (string.IsNullOrWhiteSpace(_workspace.RootPath))
            {
                SetStatus("Ouvre d'abord un workspace.");
                return;
            }

            try
            {
                SetStatus("XENO-SSS∞: exécution du pipeline...");

                var response = await _ai.RunProjectOperationAsync(
                    _workspace.RootPath,
                    "run-full-pipeline"
                );

                if (response == null)
                {
                    SetStatus("XENO: réponse vide.");
                    return;
                }

                SetStatus(response.Success
                    ? $"XENO OK: {response.Summary}"
                    : $"XENO warning: {response.Summary}");

                foreach (var detail in response.Details)
                {
                    AppendTerminal($"[XENO] {detail}");
                }
            }
            catch (Exception ex)
            {
                SetStatus($"XENO erreur: {ex.Message}");
            }
        }

        private async Task CompleteWithOllamaAsync()
        {
            var path = _tabs.GetActivePath();
            var code = _tabs.GetActiveText();

            if (string.IsNullOrWhiteSpace(code))
            {
                SetStatus("Aucun code actif à compléter.");
                return;
            }

            try
            {
                SetStatus("MOTO AI: complétion en cours...");

                var suggestion = await _ai.CompleteCodeAsync(path ?? "untitled", code);

                if (string.IsNullOrWhiteSpace(suggestion))
                {
                    SetStatus("MOTO AI: aucune suggestion.");
                    return;
                }

                _tabs.AppendToActive(Environment.NewLine + suggestion);
                SetStatus("MOTO AI: suggestion insérée.");
            }
            catch (Exception ex)
            {
                SetStatus($"MOTO AI erreur: {ex.Message}");
            }
        }

        private void AppendTerminal(string line)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendTerminal), line);
                return;
            }

            _terminalOutput.AppendText(line + Environment.NewLine);
        }

        private void SetStatus(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(SetStatus), message);
                return;
            }

            _statusLabel.Text = message;
        }
    }
}
