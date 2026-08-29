// AI/ZenModeEngine.cs
using System;
using System.Collections.Generic;

namespace Moto.Editor.AI
{
    public class ZenPrediction
    {
        public string Action { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Mode Zen AI : prédiction des actions utilisateur.
    /// Objectif : proposer l'action probable avant que l'utilisateur finisse.
    /// </summary>
    public class ZenModeEngine
    {
        private readonly MotoPredictionEngine _predictionEngine;
        private readonly Queue<string> _recentEvents = new Queue<string>();

        public ZenModeEngine(MotoPredictionEngine predictionEngine)
        {
            _predictionEngine = predictionEngine;
        }

        /// <summary>
        /// Observe un événement utilisateur.
        /// Exemple : "open-file", "terminal:dotnet build", "save-file".
        /// </summary>
        public void Observe(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                return;
            }

            _recentEvents.Enqueue(eventName);

            if (_recentEvents.Count > 30)
            {
                _recentEvents.Dequeue();
            }
        }

        /// <summary>
        /// Prédit la prochaine action probable.
        /// </summary>
        public ZenPrediction PredictNextAction(string activeFilePath)
        {
            if (!string.IsNullOrWhiteSpace(activeFilePath) &&
                activeFilePath.Contains("Tests", StringComparison.OrdinalIgnoreCase))
            {
                return new ZenPrediction
                {
                    Action = "Run tests",
                    Reason = "Active file appears to belong to a test project.",
                    Confidence = 0.72
                };
            }

            if (!string.IsNullOrWhiteSpace(activeFilePath) &&
                activeFilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                _recentEvents.Contains("terminal:dotnet build"))
            {
                return new ZenPrediction
                {
                    Action = "Run dotnet test",
                    Reason = "A build was recently executed; tests are a likely next step.",
                    Confidence = 0.68
                };
            }

            var basePrediction = _predictionEngine.Predict();

            if (basePrediction != null)
            {
                return new ZenPrediction
                {
                    Action = basePrediction.Action,
                    Reason = basePrediction.Reason,
                    Confidence = basePrediction.Confidence
                };
            }

            return null;
        }
    }
}
