// AI/MotoPredictionEngine.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace Moto.Editor.AI
{
    public class MotoPrediction
    {
        public string Action { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Moteur de prédiction des habitudes utilisateur.
    /// Base locale, légère, sans dépendance externe.
    /// </summary>
    public class MotoPredictionEngine
    {
        private readonly Dictionary<string, int> _commandFrequencies =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, int> _fileFrequencies =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private string _lastCommand = string.Empty;
        private string _lastFile = string.Empty;

        /// <summary>
        /// Enregistre une commande terminal.
        /// </summary>
        public void RecordCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            _lastCommand = command.Trim();
            Increment(_commandFrequencies, _lastCommand);
        }

        /// <summary>
        /// Enregistre un fichier ouvert.
        /// </summary>
        public void RecordFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            _lastFile = filePath;
            Increment(_fileFrequencies, filePath);
        }

        /// <summary>
        /// Prédit la prochaine action probable.
        /// </summary>
        public MotoPrediction Predict()
        {
            if (_commandFrequencies.Count == 0)
            {
                return null;
            }

            var topCommand = _commandFrequencies
                .OrderByDescending(kv => kv.Value)
                .First();

            var confidence = Math.Min(0.95, topCommand.Value / 10.0);

            return new MotoPrediction
            {
                Action = $"Run command: {topCommand.Key}",
                Reason = $"Based on repeated terminal usage. Last command: {_lastCommand}",
                Confidence = confidence
            };
        }

        private void Increment(Dictionary<string, int> map, string key)
        {
            map.TryGetValue(key, out var count);
            map[key] = count + 1;
        }
    }
}
