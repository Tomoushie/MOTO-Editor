// AI/MotoAiV2.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Moto.Editor.AI
{
    /// <summary>
    /// Contexte fourni à MOTO AI v2.
    /// </summary>
    public class MotoAiContext
    {
        public string WorkspacePath { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int CaretIndex { get; set; }
    }

    /// <summary>
    /// Suggestion produite par MOTO AI v2.
    /// </summary>
    public class MotoSuggestion
    {
        public string Title { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string Kind { get; set; } = "generic";
    }

    /// <summary>
    /// MOTO AI v2.
    /// IA locale légère basée sur :
    /// - historique des commandes ;
    /// - historique des fichiers ;
    /// - contexte du document actif ;
    /// - autocomplétion locale.
    /// </summary>
    public class MotoAiV2
    {
        private static readonly Regex WordRegex = new Regex(
            @"\\b\\w+\\b",
            RegexOptions.Compiled
        );

        private readonly Dictionary<string, int> _commandFrequencies =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, int> _fileFrequencies =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private readonly Queue<string> _recentEvents = new Queue<string>();

        /// <summary>
        /// Enregistre une commande terminal ou palette de commandes.
        /// </summary>
        public void RecordCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            Increment(_commandFrequencies, command);
            RecordEvent($"command:{command}");
        }

        /// <summary>
        /// Enregistre un fichier ouvert.
        /// </summary>
        public void RecordFileOpened(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            Increment(_fileFrequencies, filePath);
            RecordEvent($"file:{filePath}");
        }

        /// <summary>
        /// Retourne les suggestions contextuelles.
        /// </summary>
        public IReadOnlyList<MotoSuggestion> GetSuggestions(MotoAiContext context)
        {
            var suggestions = new List<MotoSuggestion>();

            suggestions.AddRange(PredictCommands());
            suggestions.AddRange(PredictFiles());
            suggestions.AddRange(GetLocalCompletions(context));

            return suggestions
                .OrderByDescending(s => s.Confidence)
                .Take(20)
                .ToList();
        }

        private void RecordEvent(string eventName)
        {
            _recentEvents.Enqueue(eventName);

            if (_recentEvents.Count > 50)
            {
                _recentEvents.Dequeue();
            }
        }

        private void Increment(Dictionary<string, int> map, string key)
        {
            map.TryGetValue(key, out var count);
            map[key] = count + 1;
        }

        private IEnumerable<MotoSuggestion> PredictCommands()
        {
            foreach (var kv in _commandFrequencies.OrderByDescending(kv => kv.Value).Take(5))
            {
                yield return new MotoSuggestion
                {
                    Title = $"Run command: {kv.Key}",
                    Reason = "Based on command history.",
                    Confidence = Math.Min(0.95, kv.Value / 10.0),
                    Kind = "command"
                };
            }
        }

        private IEnumerable<MotoSuggestion> PredictFiles()
        {
            foreach (var kv in _fileFrequencies.OrderByDescending(kv => kv.Value).Take(5))
            {
                yield return new MotoSuggestion
                {
                    Title = $"Open file: {kv.Key}",
                    Reason = "Based on file usage history.",
                    Confidence = Math.Min(0.90, kv.Value / 12.0),
                    Kind = "file"
                };
            }
        }

        private IEnumerable<MotoSuggestion> GetLocalCompletions(MotoAiContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.Text))
            {
                yield break;
            }

            var word = GetWordBeforeCaret(context.Text, context.CaretIndex);

            if (word.Length < 2)
            {
                yield break;
            }

            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in WordRegex.Matches(context.Text))
            {
                var candidate = match.Value;

                if (candidate.Length >= word.Length &&
                    candidate.StartsWith(word, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(candidate, word, StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add(candidate);
                }
            }

            foreach (var candidate in candidates.Take(10))
            {
                yield return new MotoSuggestion
                {
                    Title = candidate,
                    Reason = "Local document completion.",
                    Confidence = 0.45 + Math.Min(0.30, candidate.Length / 20.0),
                    Kind = "autocomplete"
                };
            }
        }

        private string GetWordBeforeCaret(string text, int caretIndex)
        {
            caretIndex = Math.Min(caretIndex, text.Length);

            int start = caretIndex;

            while (start > 0 && char.IsLetterOrDigit(text[start - 1]))
            {
                start--;
            }

            return text.Substring(start, caretIndex - start);
        }
    }
}
