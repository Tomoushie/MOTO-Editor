// Moto.Core/AI/Internal/ArchitectureBuilderEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moto.Core.AI.Internal.Models;

namespace Moto.Core.AI.Internal
{
    public class ArchitectureAction
    {
        public string Type { get; set; } = "Move"; // Move, Rename
        public string SourcePath { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public class ArchitecturePlan
    {
        public List<ArchitectureAction> Actions { get; } = new List<ArchitectureAction>();
        public List<string> ValidationSteps { get; } = new List<string>();
    }

    /// <summary>
    /// AI Architecture Builder : restructure, renomme, déplace, génère, connecte, valide.
    /// Toujours précédé d'un snapshot TimeMachine pour rollback sécurisé.
    /// </summary>
    public class ArchitectureBuilderEngine
    {
        public ArchitecturePlan Build(ProjectMap map)
        {
            var plan = new ArchitecturePlan();

            foreach (var symbol in map.Symbols.Where(s =>
                         s.Kind == SymbolKind.System ||
                         s.Kind == SymbolKind.Component ||
                         s.Kind == SymbolKind.Interface ||
                         s.Kind == SymbolKind.Class))
            {
                var idealFolder = GetIdealFolder(symbol);

                if (idealFolder == null)
                {
                    continue;
                }

                var relative = Path.GetRelativePath(map.RootPath, symbol.FilePath);
                var currentFolder = Path.GetDirectoryName(relative) ?? string.Empty;

                // 1. Déplacement vers le dossier conventionnel.
                if (!currentFolder.Replace("\\", "/").Contains(idealFolder, StringComparison.OrdinalIgnoreCase))
                {
                    var fileName = Path.GetFileName(symbol.FilePath);

                    plan.Actions.Add(new ArchitectureAction
                    {
                        Type = "Move",
                        SourcePath = symbol.FilePath,
                        TargetPath = Path.Combine(map.RootPath, idealFolder, fileName),
                        Reason = $"Convention : {symbol.Kind} → {idealFolder}/"
                    });
                }

                // 2. Renommage si le fichier ne porte pas le nom de la classe.
                var fileBase = Path.GetFileNameWithoutExtension(symbol.FilePath);

                if (fileBase != symbol.Name &&
                    symbol.Kind != SymbolKind.Class)
                {
                    var dir = Path.GetDirectoryName(symbol.FilePath) ?? map.RootPath;

                    plan.Actions.Add(new ArchitectureAction
                    {
                        Type = "Rename",
                        SourcePath = symbol.FilePath,
                        TargetPath = Path.Combine(dir, $"{symbol.Name}.cs"),
                        Reason = $"Le fichier doit porter le nom du {symbol.Kind} : {symbol.Name}."
                    });
                }
            }

            // Déduplication des actions.
            var unique = plan.Actions
                .GroupBy(a => a.SourcePath)
                .Select(g => g.First())
                .ToList();

            plan.Actions.Clear();
            plan.Actions.AddRange(unique);

            plan.ValidationSteps.Add("Snapshot TimeMachine avant application.");
            plan.ValidationSteps.Add("Application des déplacements/renommages.");
            plan.ValidationSteps.Add("Mise à jour des namespaces via AutoFix.");
            plan.ValidationSteps.Add("Validation finale via XENO-SSS∞.");

            return plan;
        }

        /// <summary>Applique le plan sur disque. À appeler APRÈS un snapshot TimeMachine.</summary>
        public void Apply(ArchitecturePlan plan)
        {
            foreach (var action in plan.Actions)
            {
                try
                {
                    var targetDir = Path.GetDirectoryName(action.TargetPath);

                    if (!string.IsNullOrWhiteSpace(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    if (File.Exists(action.SourcePath))
                    {
                        File.Move(action.SourcePath, action.TargetPath, overwrite: false);
                    }
                }
                catch
                {
                    // Un déplacement échoué ne bloque pas les suivants.
                }
            }
        }

        private string GetIdealFolder(ProjectSymbol symbol)
        {
            return symbol.Kind switch
            {
                SymbolKind.System => "Systems",
                SymbolKind.Component => "Components",
                SymbolKind.Interface => "Interfaces",
                _ => null
            };
        }
    }
}
