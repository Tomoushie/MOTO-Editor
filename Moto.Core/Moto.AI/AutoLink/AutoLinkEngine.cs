// Moto.Core/AI/AutoLink/AutoLinkEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moto.Core.AI.Builders;
using Moto.Core.AI.Internal;

namespace Moto.Core.AI.AutoLink
{
    /// <summary>
    /// MOTO AutoLink Engine : détecte les liens manquants et propose des actions.
    /// Ultra léger : regex + ProjectMap + builders existants.
    /// </summary>
    public class AutoLinkEngine
    {
        private readonly ProjectUnderstandingEngine _understanding = new();
        private readonly CodeGenerationEngine _generation = new();
        private readonly BehaviorBuilderV2 _behavior = new();

        public AutoLinkReport Analyze(string filePath)
        {
            var report = new AutoLinkReport { FilePath = filePath };

            if (!File.Exists(filePath)) return report;

            var content = File.ReadAllText(filePath);
            var workspace = Path.GetDirectoryName(filePath);
            var map = _understanding.BuildMap(workspace);

            var detector = new AutoLinkDetector(map);
            var issues = detector.Detect(filePath, content);

            report.Issues.AddRange(issues);

            // Génère les actions pour chaque problème
            foreach (var issue in issues)
            {
                var actions = GenerateActions(issue, map, workspace);
                report.Actions.AddRange(actions);
            }

            return report;
        }

        private List<AutoLinkAction> GenerateActions(AutoLinkIssue issue, ProjectMap map, string workspace)
        {
            var actions = new List<AutoLinkAction>();

            switch (issue.Kind)
            {
                case AutoLinkIssueKind.MissingClass:
                    actions.Add(new AutoLinkAction
                    {
                        Id = $"create-class-{issue.SymbolName}",
                        Title = $"Créer {issue.SymbolName}.cs",
                        Description = $"Générer la classe {issue.SymbolName} vide.",
                        Kind = issue.Kind,
                        TargetSymbol = issue.SymbolName,
                        GeneratedContent = GenerateClassStub(issue.SymbolName),
                        TargetPath = Path.Combine(workspace, $"{issue.SymbolName}.cs"),
                        IsInsertion = false
                    });
                    break;

                case AutoLinkIssueKind.MissingInterface:
                    actions.Add(new AutoLinkAction
                    {
                        Id = $"create-interface-{issue.SymbolName}",
                        Title = $"Créer {issue.SymbolName}.cs",
                        Description = $"Générer l'interface {issue.SymbolName}.",
                        Kind = issue.Kind,
                        TargetSymbol = issue.SymbolName,
                        GeneratedContent = GenerateInterfaceStub(issue.SymbolName),
                        TargetPath = Path.Combine(workspace, $"{issue.SymbolName}.cs"),
                        IsInsertion = false
                    });
                    break;

                case AutoLinkIssueKind.MissingSystem:
                    actions.Add(new AutoLinkAction
                    {
                        Id = $"register-system-{issue.SymbolName}",
                        Title = $"Connecter {issue.SymbolName} au pipeline",
                        Description = $"Ajouter {issue.SymbolName} dans XenoPipeline.",
                        Kind = issue.Kind,
                        TargetSymbol = issue.SymbolName,
                        GeneratedContent = GenerateSystemRegistration(issue.SymbolName),
                        TargetPath = Path.Combine(workspace, "XenoPipeline.cs"),
                        IsInsertion = true
                    });
                    break;

                case AutoLinkIssueKind.IncompleteClass:
                    actions.Add(new AutoLinkAction
                    {
                        Id = $"complete-class-{issue.SymbolName}",
                        Title = $"Compléter {issue.SymbolName}",
                        Description = $"Générer les méthodes standards pour {issue.SymbolName}.",
                        Kind = issue.Kind,
                        TargetSymbol = issue.SymbolName,
                        GeneratedContent = GenerateClassCompletion(issue.SymbolName, map),
                        TargetPath = issue.FilePath,
                        IsInsertion = true
                    });
                    break;
            }

            return actions;
        }

        private string GenerateClassStub(string className)
        {
            return $@"using System;

namespace Game
{{
    /// <summary>Classe générée par MOTO AutoLink.</summary>
    public class {className}
    {{
        public {className}()
        {{
            // TODO : implémenter
        }}
    }}
}}";
        }

        private string GenerateInterfaceStub(string interfaceName)
        {
            return $@"using System;

namespace Game
{{
    /// <summary>Interface générée par MOTO AutoLink.</summary>
    public interface {interfaceName}
    {{
        void Execute();
    }}
}}";
        }

        private string GenerateSystemRegistration(string systemName)
        {
            return $"// Ajouter dans XenoPipeline.Run() :\nvar {systemName.ToLower()} = new {systemName}();\n{systemName.ToLower()}.Initialize();";
        }

        private string GenerateClassCompletion(string className, ProjectMap map)
        {
            // Génère des méthodes standards selon les conventions du projet
            return $@"
    public void Initialize()
    {{
        // TODO : initialiser {className}
    }}

    public void Update(float deltaTime)
    {{
        // TODO : mettre à jour {className}
    }}
";
        }

        /// <summary>
        /// Applique une action : crée le fichier ou insère le code.
        /// </summary>
        public bool Apply(AutoLinkAction action)
        {
            try
            {
                if (action.IsInsertion)
                {
                    // Insère le code dans le fichier existant
                    if (File.Exists(action.TargetPath))
                    {
                        var content = File.ReadAllText(action.TargetPath);
                        var insertPos = content.LastIndexOf('}');

                        if (insertPos >= 0)
                        {
                            var newContent = content.Insert(insertPos, action.GeneratedContent);
                            File.WriteAllText(action.TargetPath, newContent);
                            return true;
                        }
                    }
                }
                else
                {
                    // Crée un nouveau fichier
                    var dir = Path.GetDirectoryName(action.TargetPath);

                    if (!string.IsNullOrWhiteSpace(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.WriteAllText(action.TargetPath, action.GeneratedContent);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}
