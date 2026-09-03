// Moto.Core/AI/Autonomy/AgentActionParser.cs
using System;
using System.Text;

namespace Moto.Core.AI.Autonomy
{
    /// <summary>
    /// Transforme la réponse texte libre du modèle (MotoAiKernel.RouteAsync ne fait
    /// AUCUN appel d'outil structuré — juste du texte) en une AgentAction typée.
    /// Format attendu, une ligne par champ, tolérant à la casse et aux espaces :
    ///
    ///   ACTION: WriteFile
    ///   PATH: chemin/relatif.txt
    ///   CONTENT: &lt;&lt;&lt;
    ///   contenu du fichier, sur autant de lignes que nécessaire
    ///   &gt;&gt;&gt;
    ///   SUMMARY: courte explication pour l'humain
    ///
    /// Ne lève JAMAIS d'exception : un modèle local (Ollama, souvent moins
    /// discipliné qu'un gros modèle) qui ne respecte pas le format produit
    /// Kind = Malformed plutôt qu'un plantage — BackgroundAgentLoop compte ça
    /// comme un pas "raté" contre son budget, pas comme une erreur fatale.
    /// </summary>
    public static class AgentActionParser
    {
        public static AgentAction Parse(string? modelOutput)
        {
            var action = new AgentAction { RawModelOutput = modelOutput ?? string.Empty };
            if (string.IsNullOrWhiteSpace(modelOutput))
            {
                action.Kind = AgentActionKind.Malformed;
                return action;
            }

            var lines = modelOutput.Replace("\r\n", "\n").Split('\n');
            AgentActionKind? kind = null;
            string? path = null, command = null, summary = null;
            string? content = null;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (TryExtractValue(line, "ACTION:", out var actionText))
                {
                    kind = ParseKind(actionText);
                }
                else if (TryExtractValue(line, "PATH:", out var pathText))
                {
                    path = pathText;
                }
                else if (TryExtractValue(line, "COMMAND:", out var commandText))
                {
                    command = commandText;
                }
                else if (TryExtractValue(line, "SUMMARY:", out var summaryText))
                {
                    summary = summaryText;
                }
                else if (line.TrimStart().StartsWith("CONTENT:", StringComparison.OrdinalIgnoreCase))
                {
                    // Le contenu peut s'étaler sur plusieurs lignes — délimité par <<< ... >>>,
                    // sur la même ligne que CONTENT: ou juste après.
                    content = ExtractDelimitedContent(lines, ref i);
                }
            }

            if (kind is null)
            {
                action.Kind = AgentActionKind.Malformed;
                return action;
            }

            action.Kind = kind.Value;
            action.Path = path;
            action.Command = command;
            action.Summary = summary;
            action.Content = content;

            // Validation minimale : une action sans les champs qu'elle exige n'est
            // pas exploitable — mieux vaut Malformed (nouvelle tentative) qu'un
            // outil appelé avec des données absentes.
            var valid = action.Kind switch
            {
                AgentActionKind.ReadFile => !string.IsNullOrWhiteSpace(path),
                AgentActionKind.WriteFile => !string.IsNullOrWhiteSpace(path) && content is not null,
                AgentActionKind.RunCommand => !string.IsNullOrWhiteSpace(command),
                AgentActionKind.Finish => true,
                _ => false
            };

            if (!valid) action.Kind = AgentActionKind.Malformed;
            return action;
        }

        private static AgentActionKind? ParseKind(string text) => text.Trim().ToLowerInvariant() switch
        {
            "readfile" or "read_file" or "read" => AgentActionKind.ReadFile,
            "writefile" or "write_file" or "write" => AgentActionKind.WriteFile,
            "runcommand" or "run_command" or "run" or "command" => AgentActionKind.RunCommand,
            "finish" or "done" or "terminé" or "termine" => AgentActionKind.Finish,
            _ => null
        };

        private static bool TryExtractValue(string line, string tag, out string value)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith(tag, StringComparison.OrdinalIgnoreCase))
            {
                value = trimmed.Substring(tag.Length).Trim();
                return true;
            }
            value = string.Empty;
            return false;
        }

        private static string ExtractDelimitedContent(string[] lines, ref int i)
        {
            var sb = new StringBuilder();
            var firstLine = lines[i];
            var afterTag = firstLine.TrimStart().Substring("CONTENT:".Length).Trim();

            // Cas "CONTENT: <<<" sur la même ligne, sans rien après le délimiteur.
            var started = afterTag.StartsWith("<<<", StringComparison.Ordinal);
            if (started)
            {
                var rest = afterTag.Substring(3);
                if (rest.Contains(">>>"))
                {
                    // Tout tient sur une seule ligne : "CONTENT: <<<texte>>>".
                    var endIdx = rest.IndexOf(">>>", StringComparison.Ordinal);
                    return rest.Substring(0, endIdx);
                }
                if (rest.Length > 0) sb.AppendLine(rest);
            }

            i++;
            while (i < lines.Length)
            {
                var line = lines[i];
                if (line.Trim() == ">>>") return sb.ToString().TrimEnd('\n', '\r');
                if (!started && line.Trim() == "<<<") { started = true; i++; continue; }
                sb.AppendLine(line);
                i++;
            }

            // Pas de ">>>" trouvé avant la fin — on rend ce qu'on a, le contenu sera
            // simplement moins précis plutôt que de tout perdre.
            return sb.ToString().TrimEnd('\n', '\r');
        }
    }
}
