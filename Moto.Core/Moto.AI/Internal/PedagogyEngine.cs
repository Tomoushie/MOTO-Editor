// Moto.Core/AI/Internal/PedagogyEngine.cs
using System.IO;
using System.Text.RegularExpressions;

namespace Moto.Core.AI.Internal
{
    /// <summary>
    /// Moteur pédagogique interne.
    /// Explique le code, les erreurs et les concepts.
    /// </summary>
    public class PedagogyEngine
    {
        private static readonly Regex ClassRegex = new Regex(@"\bclass\s+(\w+)", RegexOptions.Compiled);
        private static readonly Regex InterfaceRegex = new Regex(@"\binterface\s+(\w+)", RegexOptions.Compiled);
        private static readonly Regex NamespaceRegex = new Regex(@"namespace\s+([\w\.]+)", RegexOptions.Compiled);
        private static readonly Regex MethodRegex = new Regex(@"\bvoid\s+(\w+)\s*\(", RegexOptions.Compiled);

        /// <summary>
        /// Explique un fichier de manière simple.
        /// </summary>
        public string ExplainFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return "Je ne peux pas expliquer ce fichier, car il est introuvable.";
            }

            var text = File.ReadAllText(filePath);
            var fileName = Path.GetFileName(filePath);

            var explanation = $"Voici une explication simple du fichier {fileName}.\n\n";

            var ns = NamespaceRegex.Match(text);
            if (ns.Success)
            {
                explanation += $"Ce fichier appartient au namespace '{ns.Groups[1].Value}'.\n";
            }

            var classes = ClassRegex.Matches(text);
            if (classes.Count > 0)
            {
                explanation += $"Il contient {classes.Count} classe(s) :\n";

                foreach (Match match in classes)
                {
                    explanation += $"- {match.Groups[1].Value}\n";
                }
            }

            var interfaces = InterfaceRegex.Matches(text);
            if (interfaces.Count > 0)
            {
                explanation += $"Il contient {interfaces.Count} interface(s) :\n";

                foreach (Match match in interfaces)
                {
                    explanation += $"- {match.Groups[1].Value}\n";
                }
            }

            var methods = MethodRegex.Matches(text);
            if (methods.Count > 0)
            {
                explanation += $"J'ai détecté {methods.Count} méthode(s) principale(s).\n";
            }

            if (text.Contains("TODO"))
            {
                explanation += "\nCe fichier contient des TODO. Il reste donc des choses à compléter.";
            }

            explanation += "\n\nPour comprendre un fichier, regarde d'abord :";
            explanation += "\n1. Le namespace.";
            explanation += "\n2. Les classes.";
            explanation += "\n3. Les interfaces.";
            explanation += "\n4. Les méthodes importantes.";

            return explanation;
        }

        /// <summary>
        /// Enseigne un concept simple.
        /// </summary>
        public string Teach(string topic)
        {
            topic = topic?.ToLowerInvariant() ?? string.Empty;

            if (topic.Contains("classe") || topic.Contains("class"))
            {
                return "Une classe est un plan de construction. Elle décrit les données et les actions qu'un objet pourra faire.";
            }

            if (topic.Contains("interface"))
            {
                return "Une interface est un contrat. Elle dit quelles actions une classe doit fournir.";
            }

            if (topic.Contains("namespace"))
            {
                return "Un namespace est une boîte de rangement. Il organise les classes et évite les conflits de noms.";
            }

            if (topic.Contains("système") || topic.Contains("system"))
            {
                return "Un système est un module qui fait une chose précise. Par exemple : santé, mouvement, combat.";
            }

            if (topic.Contains("pipeline"))
            {
                return "Un pipeline est une suite d'étapes. Chaque étape fait une partie du travail puis passe le résultat à la suivante.";
            }

            return "Je peux t'expliquer : classe, interface, namespace, système, pipeline, module, composant.";
        }
    }
}
