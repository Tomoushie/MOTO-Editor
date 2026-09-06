// Moto.Core/AI/Autonomy/AgentActionParser.cs
using System;
using System.Collections.Generic;
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

            var lines = NormalizePipeJoinedTags(modelOutput.Replace("\r\n", "\n").Split('\n'));
            AgentActionKind? kind = null;
            string? path = null, command = null, summary = null, toAgentId = null;
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
                else if (TryExtractValue(line, "TO:", out var toText))
                {
                    toAgentId = toText;
                }
                else if (TryExtractValue(line, "SUMMARY:", out var summaryText))
                {
                    summary = summaryText;
                }
                else if (line.TrimStart().StartsWith("CONTENT:", StringComparison.OrdinalIgnoreCase))
                {
                    // Le contenu peut s'étaler sur plusieurs lignes — délimité par <<< ... >>>,
                    // sur la même ligne que CONTENT: ou juste après.
                    content = ExtractDelimitedContent(lines, ref i, out var trailingSummary);
                    if (trailingSummary is not null) summary ??= trailingSummary;
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
            action.ToAgentId = string.IsNullOrWhiteSpace(toAgentId) ? null : toAgentId.Trim();

            // Validation minimale : une action sans les champs qu'elle exige n'est
            // pas exploitable — mieux vaut Malformed (nouvelle tentative) qu'un
            // outil appelé avec des données absentes.
            var valid = action.Kind switch
            {
                AgentActionKind.ReadFile => !string.IsNullOrWhiteSpace(path),
                AgentActionKind.WriteFile => !string.IsNullOrWhiteSpace(path) && content is not null,
                AgentActionKind.RunCommand => !string.IsNullOrWhiteSpace(command),
                AgentActionKind.SendMessage => !string.IsNullOrWhiteSpace(summary),
                AgentActionKind.Finish => true,
                _ => false
            };

            if (!valid) action.Kind = AgentActionKind.Malformed;
            return action;
        }

        private static AgentActionKind? ParseKind(string text)
        {
            var t = text.Trim().ToLowerInvariant();
            switch (t)
            {
                case "readfile": case "read_file": case "read": return AgentActionKind.ReadFile;
                case "writefile": case "write_file": case "write": return AgentActionKind.WriteFile;
                case "runcommand": case "run_command": case "run": case "command": return AgentActionKind.RunCommand;
                case "sendmessage": case "send_message": case "send": case "message": return AgentActionKind.SendMessage;
                case "finish": case "done": case "terminé": case "termine": return AgentActionKind.Finish;
            }

            // Filet large (06/09, observé en pratique sur /refactor) : un modèle
            // local invente souvent un verbe qui colle au vocabulaire de
            // l'objectif ("Refactore le fichier...") plutôt que d'utiliser le nom
            // d'outil exact demandé — vu tour à tour RefactorFile, RefactorCode,
            // RefactorContent en une seule session, avec un CONTENT/SUMMARY par
            // ailleurs correctement formé à chaque fois. Plutôt que de lister
            // chaque variante une par une (jeu du chat et de la souris perdu
            // d'avance), on reconnaît le PATRON : tout verbe qui commence par
            // "refactor", ou qui contient "write"/"update"/"modify" quelque part,
            // est traité comme WriteFile.
            if (t.StartsWith("refactor", StringComparison.Ordinal)
                || t.Contains("write", StringComparison.Ordinal)
                || t.Contains("update", StringComparison.Ordinal)
                || t.Contains("modify", StringComparison.Ordinal))
                return AgentActionKind.WriteFile;

            return null;
        }

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

        private static readonly string[] KnownTags = { "ACTION:", "PATH:", "COMMAND:", "TO:", "SUMMARY:" };

        /// <summary>
        /// Filet (06/09, observé en pratique) : un modèle local met parfois TOUS
        /// les champs sur UNE SEULE ligne séparés par " | " au lieu d'une ligne
        /// par champ comme demandé (ex : "ACTION: X | PATH: Y | CONTENT: Z").
        /// Sans ce filet, tout ce qui suit le premier " | " est absorbé comme
        /// faisant partie de la valeur du premier champ et disparaît. On sépare
        /// en vraies lignes AVANT le parsing principal — mais seulement quand ce
        /// qui suit " | " ressemble VRAIMENT au début d'une balise connue, pour
        /// ne jamais casser un " | " légitime à l'intérieur de vrai code
        /// (ex : "var x = flags | Flags.A;" ressort inchangé).
        /// </summary>
        private static string[] NormalizePipeJoinedTags(string[] lines)
        {
            var result = new List<string>(lines.Length);
            foreach (var line in lines)
            {
                if (!line.Contains(" | "))
                {
                    result.Add(line);
                    continue;
                }

                var segments = line.Split(" | ");
                var current = segments[0];
                for (var s = 1; s < segments.Length; s++)
                {
                    if (LooksLikeKnownTagStart(segments[s]))
                    {
                        result.Add(current);
                        current = segments[s];
                    }
                    else
                    {
                        current += " | " + segments[s];
                    }
                }
                result.Add(current);
            }
            return result.ToArray();
        }

        private static bool LooksLikeKnownTagStart(string line)
        {
            var t = line.TrimStart();
            foreach (var tag in KnownTags)
                if (t.StartsWith(tag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string ExtractDelimitedContent(string[] lines, ref int i, out string? trailingSummary)
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
                    trailingSummary = null;
                    return rest.Substring(0, endIdx);
                }
                if (rest.Length > 0) sb.AppendLine(rest);
            }
            else if (afterTag.Length > 0)
            {
                // (06/09, observé en pratique) : le modèle a écrit du contenu
                // directement après "CONTENT:" SANS jamais utiliser <<< — sans ce
                // cas, cette première ligne de contenu disparaissait purement et
                // simplement (ni gardée ici, ni reconnue par aucune autre balise).
                sb.AppendLine(afterTag);
            }

            i++;
            while (i < lines.Length)
            {
                var line = lines[i];
                if (line.Trim() == ">>>") return FinalizeContent(sb, out trailingSummary);
                if (!started && line.Trim() == "<<<") { started = true; i++; continue; }

                // (06/09) Filet symétrique au précédent : si <<< n'a JAMAIS été
                // ouvert (contenu sans délimiteurs du tout) et que cette ligne
                // démarre une autre balise connue, le contenu s'arrête ICI plutôt
                // que d'avaler la balise suivante (ex: SUMMARY) comme si c'était
                // du code. On recule i d'un cran pour que la boucle principale de
                // Parse retraite correctement cette ligne comme sa propre balise.
                if (!started && LooksLikeKnownTagStart(line))
                {
                    i--;
                    return FinalizeContent(sb, out trailingSummary);
                }

                sb.AppendLine(line);
                i++;
            }

            // Pas de ">>>" trouvé avant la fin — on rend ce qu'on a, le contenu sera
            // simplement moins précis plutôt que de tout perdre.
            return FinalizeContent(sb, out trailingSummary);
        }

        /// <summary>
        /// Filet de sécurité (05/09, trouvé en vérifiant sur disque le résultat d'un
        /// vrai /refactor) : un modèle peu discipliné place parfois sa balise
        /// SUMMARY: À L'INTÉRIEUR du bloc CONTENT: &lt;&lt;&lt; ... &gt;&gt;&gt; au lieu
        /// de juste après. Comme ce parseur est strictement ligne par ligne, cette
        /// ligne aurait sinon été écrite TELLE QUELLE dans le fichier cible — sur du
        /// vrai code, une ligne "SUMMARY: ..." en trop casse la compilation. Si la
        /// dernière ligne non-vide du contenu ressemble à cette balise, on la retire
        /// du contenu écrit et on la récupère comme résumé (utilisé seulement si
        /// aucun SUMMARY: n'a été trouvé ailleurs, correctement placé).
        /// </summary>
        private static string FinalizeContent(StringBuilder sb, out string? trailingSummary)
        {
            trailingSummary = null;
            var text = sb.ToString().TrimEnd('\n', '\r');
            var contentLines = text.Split('\n');

            var lastNonEmpty = contentLines.Length - 1;
            while (lastNonEmpty >= 0 && contentLines[lastNonEmpty].Trim().Length == 0) lastNonEmpty--;
            if (lastNonEmpty < 0) return text;

            if (TryExtractValue(contentLines[lastNonEmpty], "SUMMARY:", out var summaryText))
            {
                trailingSummary = summaryText;
                return string.Join("\n", contentLines, 0, lastNonEmpty).TrimEnd('\n', '\r');
            }

            return text;
        }
    }
}
