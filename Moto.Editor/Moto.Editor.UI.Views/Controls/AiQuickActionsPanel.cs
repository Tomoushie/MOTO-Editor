// Moto.Editor/UI/Controls/AiQuickActionsPanel.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Moto.Editor.UI.Controls
{
    /// <summary>
    /// Action rapide proposée par MOTO AI.
    /// </summary>
    public class AiQuickAction
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Action Execute { get; set; }
    }

    /// <summary>
    /// Panneau contextuel d'actions rapides.
    /// Apparaît à côté du curseur et propose :
    /// - Ajouter une méthode
    /// - Ajouter une classe
    /// - Ajouter un système
    /// - Ajouter un commentaire
    /// - Ajouter un test
    /// - Ajouter un namespace
    ///
    /// Les actions disponibles dépendent du type de fichier actif.
    /// </summary>
    public class AiQuickActionsPanel : Panel
    {
        private readonly FlowLayoutPanel _list = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Dock = DockStyle.Fill,
            Padding = new Padding(6)
        };

        private readonly Label _header = new Label
        {
            Text = "MOTO — Actions rapides",
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Padding = new Padding(8, 0, 0, 0)
        };

        private readonly List<AiQuickAction> _allActions = new List<AiQuickAction>();

        public AiQuickActionsPanel()
        {
            Width = 260;
            BackColor = Color.FromArgb(30, 31, 35);
            ForeColor = Color.FromArgb(230, 232, 236);
            BorderStyle = BorderStyle.FixedSingle;
            Visible = false;

            Controls.Add(_list);
            Controls.Add(_header);

            RegisterDefaultActions();
        }

        /// <summary>
        /// Ajoute une action personnalisée.
        /// </summary>
        public void RegisterAction(AiQuickAction action)
        {
            if (action != null)
            {
                _allActions.Add(action);
            }
        }

        /// <summary>
        /// Affiche les actions pertinentes selon le fichier actif.
        /// </summary>
        public void ShowFor(string filePath, Point screenLocation)
        {
            _list.Controls.Clear();

            var extension = System.IO.Path.GetExtension(filePath ?? string.Empty).ToLowerInvariant();

            // Filtrage : certaines actions n'ont de sens que pour certains langages.
            var filtered = _allActions.Where(a => IsRelevant(a.Id, extension)).ToList();

            if (filtered.Count == 0)
            {
                Hide();
                return;
            }

            foreach (var action in filtered)
            {
                var button = CreateActionButton(action);
                _list.Controls.Add(button);
            }

            Location = screenLocation;
            Height = Math.Min(360, 28 + filtered.Count * 44 + 12);
            Visible = true;
            BringToFront();
        }

        private Button CreateActionButton(AiQuickAction action)
        {
            var button = new Button
            {
                Text = $"{action.Title}\n{action.Description}",
                Width = 230,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 2, 0, 2),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(40, 41, 46),
                ForeColor = Color.FromArgb(230, 232, 236),
                Margin = new Padding(0, 2, 0, 2),
                Font = new Font("Segoe UI", 8.75F)
            };

            button.FlatAppearance.BorderSize = 0;
            button.Click += (s, e) =>
            {
                action.Execute?.Invoke();
                Hide();
            };

            return button;
        }

        private bool IsRelevant(string actionId, string extension)
        {
            // Règles simples : une action n'est proposée que si elle a du sens.
            switch (actionId)
            {
                case "add-method":
                case "add-class":
                case "add-interface":
                case "add-namespace":
                case "add-system":
                    return extension == ".cs" || extension == ".java" || extension == ".ts" || extension == ".py";

                case "add-test":
                    return extension == ".cs" || extension == ".java" || extension == ".ts" || extension == ".py";

                case "add-comment":
                case "explain-selection":
                    return !string.IsNullOrWhiteSpace(extension);

                default:
                    return true;
            }
        }

        private void RegisterDefaultActions()
        {
            // Les Execute sont branchés par MainWindow lors de l'initialisation.
            RegisterAction(new AiQuickAction { Id = "add-method", Title = "Ajouter une méthode", Description = "Crée une méthode vide ici." });
            RegisterAction(new AiQuickAction { Id = "add-class", Title = "Ajouter une classe", Description = "Crée une nouvelle classe." });
            RegisterAction(new AiQuickAction { Id = "add-interface", Title = "Ajouter une interface", Description = "Crée un contrat d'interface." });
            RegisterAction(new AiQuickAction { Id = "add-system", Title = "Ajouter un système", Description = "Crée un module système (ECS)." });
            RegisterAction(new AiQuickAction { Id = "add-namespace", Title = "Ajouter un namespace", Description = "Encapsule le code dans un namespace." });
            RegisterAction(new AiQuickAction { Id = "add-comment", Title = "Ajouter un commentaire", Description = "MOTO explique le code sélectionné." });
            RegisterAction(new AiQuickAction { Id = "add-test", Title = "Ajouter un test", Description = "Génère un test unitaire." });
            RegisterAction(new AiQuickAction { Id = "explain-selection", Title = "Expliquer la sélection", Description = "MOTO explique ce que fait le code." });
        }

        /// <summary>
        /// Récupère une action par son ID pour brancher un handler externe.
        /// </summary>
        public AiQuickAction FindAction(string id)
        {
            return _allActions.FirstOrDefault(a =>
                string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }
}
