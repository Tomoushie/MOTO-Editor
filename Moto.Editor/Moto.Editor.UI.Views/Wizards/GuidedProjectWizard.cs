// Moto.Editor/UI/Wizards/GuidedProjectWizard.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Moto.Editor.UI.Wizards
{
    /// <summary>
    /// Type de projet proposé par l'assistant.
    /// </summary>
    public enum ProjectTemplate
    {
        Empty,
        ConsoleApp,
        GameModule,
        WebApi,
        Library
    }

    /// <summary>
    /// Options saisies par l'utilisateur débutant.
    /// </summary>
    public class GuidedProjectOptions
    {
        public string ProjectName { get; set; } = string.Empty;
        public string TargetFolder { get; set; } = string.Empty;
        public ProjectTemplate Template { get; set; } = ProjectTemplate.ConsoleApp;
        public string Description { get; set; } = string.Empty;
        public bool IncludeTests { get; set; } = true;
        public bool IncludeDocumentation { get; set; } = true;
    }

    /// <summary>
    /// Contrat pour appeler XENO-SSS∞ depuis l'assistant.
    /// XENO génère la structure, MOTO Editor ne crée rien directement.
    /// </summary>
    public interface IProjectScaffolder
    {
        Task<ScaffoldResult> ScaffoldAsync(GuidedProjectOptions options);
    }

    /// <summary>
    /// Résultat de la génération de projet.
    /// </summary>
    public class ScaffoldResult
    {
        public bool Success { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<string> CreatedFiles { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
    }

    /// <summary>
    /// Assistant de création de projet guidée.
    /// Le débutant n'a qu'à répondre à quelques questions.
    /// XENO-SSS∞ fait le reste : structure, fichiers, classes, systèmes, exemples.
    /// </summary>
    public class GuidedProjectWizard : Form
    {
        private readonly IProjectScaffolder _scaffolder;

        private TextBox _nameBox;
        private TextBox _descriptionBox;
        private ComboBox _templateBox;
        private CheckBox _includeTests;
        private CheckBox _includeDocumentation;
        private TextBox _folderBox;
        private Button _browseButton;
        private Button _createButton;
        private Button _cancelButton;
        private RichTextBox _previewBox;

        public GuidedProjectOptions ResultOptions { get; private set; }

        /// <summary>
        /// Déclenché si l'utilisateur valide et que la génération réussit.
        /// </summary>
        public event Action<ScaffoldResult> ProjectCreated;

        public GuidedProjectWizard(IProjectScaffolder scaffolder)
        {
            _scaffolder = scaffolder ?? throw new ArgumentNullException(nameof(scaffolder));

            Text = "Créer un nouveau projet (assisté)";
            Width = 720;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            InitializeLayout();
        }

        private void InitializeLayout()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(14)
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Nom du projet
            layout.Controls.Add(CreateLabel("Nom du projet :"), 0, 0);
            _nameBox = new TextBox { Dock = DockStyle.Fill, Text = "MonProjet" };
            layout.Controls.Add(_nameBox, 1, 0);

            // Description (langage naturel)
            layout.Controls.Add(CreateLabel("Description :"), 0, 1);
            _descriptionBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                Height = 60,
                Text = "Un petit projet pour apprendre."
            };
            layout.Controls.Add(_descriptionBox, 1, 1);

            // Template
            layout.Controls.Add(CreateLabel("Type de projet :"), 0, 2);
            _templateBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _templateBox.Items.AddRange(new object[]
            {
                "Application console (simple)",
                "Module de jeu (Snake2000)",
                "API web",
                "Bibliothèque",
                "Projet vide"
            });
            _templateBox.SelectedIndex = 0;
            layout.Controls.Add(_templateBox, 1, 2);

            // Options
            layout.Controls.Add(CreateLabel("Options :"), 0, 3);
            var optionsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            _includeTests = new CheckBox { Text = "Inclure des tests", Checked = true, AutoSize = true };
            _includeDocumentation = new CheckBox { Text = "Inclure une documentation", Checked = true, AutoSize = true };
            optionsPanel.Controls.Add(_includeTests);
            optionsPanel.Controls.Add(_includeDocumentation);
            layout.Controls.Add(optionsPanel, 1, 3);

            // Dossier cible
            layout.Controls.Add(CreateLabel("Dossier :"), 0, 4);
            var folderPanel = new Panel { Dock = DockStyle.Fill };
            _folderBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "MotoProjects"
                )
            };
            _browseButton = new Button { Text = "...", Dock = DockStyle.Right, Width = 40 };
            _browseButton.Click += BrowseButton_Click;
            folderPanel.Controls.Add(_folderBox);
            folderPanel.Controls.Add(_browseButton);
            layout.Controls.Add(folderPanel, 1, 4);

            // Preview
            layout.Controls.Add(CreateLabel("Aperçu :"), 0, 5);
            _previewBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(27, 28, 31),
                ForeColor = Color.FromArgb(230, 232, 236),
                Font = new Font("Consolas", 9F)
            };
            layout.Controls.Add(_previewBox, 1, 5);

            // Boutons
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            _createButton = new Button { Text = "Créer le projet", Width = 140, Height = 32 };
            _cancelButton = new Button { Text = "Annuler", Width = 100, Height = 32 };
            _createButton.Click += CreateButton_Click;
            _cancelButton.Click += (s, e) => DialogResult = DialogResult.Cancel;
            buttonPanel.Controls.Add(_createButton);
            buttonPanel.Controls.Add(_cancelButton);

            layout.Controls.Add(new Panel(), 0, 6);
            layout.Controls.Add(buttonPanel, 1, 6);

            // Mise à jour de l'aperçu à chaque changement
            _nameBox.TextChanged += (s, e) => UpdatePreview();
            _templateBox.SelectedIndexChanged += (s, e) => UpdatePreview();
            _descriptionBox.TextChanged += (s, e) => UpdatePreview();

            Controls.Add(layout);
            UpdatePreview();
        }

        private Label CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false,
                Height = 28
            };
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _folderBox.Text = dialog.SelectedPath;
            }
        }

        private void UpdatePreview()
        {
            var templateName = _templateBox.SelectedItem?.ToString() ?? "inconnu";

            _previewBox.Text =
                "Ce que MOTO va créer pour toi :\n\n" +
                $"• Nom : {_nameBox.Text}\n" +
                $"• Type : {templateName}\n" +
                $"• Description : {_descriptionBox.Text}\n" +
                $"• Dossier : {_folderBox.Text}\n" +
                $"• Tests : {(_includeTests.Checked ? "oui" : "non")}\n" +
                $"• Documentation : {(_includeDocumentation.Checked ? "oui" : "non")}\n\n" +
                "MOTO va générer automatiquement :\n" +
                "  - la structure des dossiers\n" +
                "  - les fichiers de base\n" +
                "  - des classes et systèmes d'exemple\n" +
                "  - des commentaires pédagogiques\n\n" +
                "Tu n'as rien à écrire toi-même.";
        }

        private async void CreateButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show("Donne un nom à ton projet.", "MOTO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_folderBox.Text) || !Directory.Exists(_folderBox.Text))
            {
                MessageBox.Show("Choisis un dossier valide.", "MOTO", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var options = new GuidedProjectOptions
            {
                ProjectName = _nameBox.Text.Trim(),
                TargetFolder = _folderBox.Text.Trim(),
                Template = MapTemplate(_templateBox.SelectedIndex),
                Description = _descriptionBox.Text.Trim(),
                IncludeTests = _includeTests.Checked,
                IncludeDocumentation = _includeDocumentation.Checked
            };

            _createButton.Enabled = false;
            _createButton.Text = "Création en cours...";

            try
            {
                var result = await _scaffolder.ScaffoldAsync(options);

                if (!result.Success)
                {
                    MessageBox.Show(
                        $"La création a échoué :\n{result.Summary}",
                        "MOTO",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    return;
                }

                ResultOptions = options;
                ProjectCreated?.Invoke(result);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur : {ex.Message}", "MOTO", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _createButton.Enabled = true;
                _createButton.Text = "Créer le projet";
            }
        }

        private ProjectTemplate MapTemplate(int index)
        {
            return index switch
            {
                0 => ProjectTemplate.ConsoleApp,
                1 => ProjectTemplate.GameModule,
                2 => ProjectTemplate.WebApi,
                3 => ProjectTemplate.Library,
                4 => ProjectTemplate.Empty,
                _ => ProjectTemplate.ConsoleApp
            };
        }
    }
}
