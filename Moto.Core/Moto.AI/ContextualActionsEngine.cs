// Moto.Core/AI/Actions/ContextualActionsEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Moto.Core.AI.Actions
{
    public enum ContextualActionKind
    {
        Layout,
        Terminal,
        Editor,
        Ai,
        Project
    }

    public sealed class ContextualAction
    {
        public string Id { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public ContextualActionKind Kind { get; init; }
        public string Command { get; init; } = string.Empty;
        public double Relevance { get; init; }
    }

    public sealed class ActionContext
    {
        public bool HasOpenDocument { get; init; }
        public bool IsTerminalVisible { get; init; }
        public bool IsMaximized { get; init; }
        public string? CurrentFilePath { get; init; }
        public bool HasErrors { get; init; }
        public int OpenTabsCount { get; init; }
    }

    /// <summary>
    /// Retourne des actions contextuelles selon l'état courant de l'éditeur.
    /// Reste dans le domaine UI/layout/terminal de MOTO Editor.
    /// </summary>
    public sealed class ContextualActionsEngine
    {
        private static readonly ContextualAction[] Catalog = new[]
        {
            new ContextualAction
            {
                Id = "layout.optimize",
                Title = "Optimiser le layout",
                Description = "Réorganise les panneaux pour l'espace disponible.",
                Kind = ContextualActionKind.Layout,
                Command = "/action layout-optimize",
                Relevance = 0.7
            },
            new ContextualAction
            {
                Id = "layout.maximize",
                Title = "Maximiser l'éditeur",
                Description = "Passe l'éditeur en plein écran.",
                Kind = ContextualActionKind.Layout,
                Command = "/action maximize",
                Relevance = 0.6
            },
            new ContextualAction
            {
                Id = "layout.restore",
                Title = "Restaurer le layout",
                Description = "Revient au layout normal.",
                Kind = ContextualActionKind.Layout,
                Command = "/action layout-restore",
                Relevance = 0.6
            },
            new ContextualAction
            {
                Id = "terminal.open",
                Title = "Ouvrir le terminal",
                Description = "Affiche le terminal intégré.",
                Kind = ContextualActionKind.Terminal,
                Command = "/action terminal-open",
                Relevance = 0.7
            },
            new ContextualAction
            {
                Id = "terminal.test",
                Title = "Tester le terminal",
                Description = "Ouvre le terminal et lance une commande de test.",
                Kind = ContextualActionKind.Terminal,
                Command = "/action terminal-test",
                Relevance = 0.8
            },
            new ContextualAction
            {
                Id = "editor.format",
                Title = "Formater le fichier",
                Description = "Applique le formatage au fichier courant.",
                Kind = ContextualActionKind.Editor,
                Command = "/action format",
                Relevance = 0.9
            },
            new ContextualAction
            {
                Id = "ai.explain",
                Title = "Expliquer le code",
                Description = "Demande à MOTO AI d'expliquer la sélection.",
                Kind = ContextualActionKind.Ai,
                Command = "/action explain",
                Relevance = 0.8
            },
            new ContextualAction
            {
                Id = "project.build",
                Title = "Compiler le projet",
                Description = "Lance la compilation et affiche les diagnostics.",
                Kind = ContextualActionKind.Project,
                Command = "/action build",
                Relevance = 0.9
            }
        };

        public IReadOnlyList<ContextualAction> GetActions(ActionContext context)
        {
            var scored = new List<(ContextualAction Action, double Score)>();

            foreach (var action in Catalog)
            {
                var score = ComputeScore(action, context);
                if (score > 0.2)
                    scored.Add((action, score));
            }

            return scored
                .OrderByDescending(x => x.Score)
                .Select(x => x.Action)
                .Take(6)
                .ToList();
        }

        private static double ComputeScore(ContextualAction action, ActionContext context)
        {
            var score = action.Relevance;

            switch (action.Id)
            {
                case "layout.maximize":
                    if (context.IsMaximized) score -= 0.5;
                    if (context.OpenTabsCount > 1) score += 0.2;
                    break;

                case "layout.restore":
                    if (!context.IsMaximized) score -= 0.4;
                    break;

                case "terminal.open":
                case "terminal.test":
                    if (context.IsTerminalVisible) score -= 0.3;
                    break;

                case "editor.format":
                case "ai.explain":
                    if (!context.HasOpenDocument) score -= 0.8;
                    break;

                case "project.build":
                    if (context.HasErrors) score += 0.3;
                    break;
            }

            return Math.Clamp(score, 0.0, 1.0);
        }
    }
}
