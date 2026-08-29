// AI/MotoAi.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moto.Editor.Integration;

namespace Moto.Editor.AI
{
    /// <summary>
    /// MOTO AI est l'assistant intégré de MOTO Editor.
    /// Il ne remplace pas XENO-SSS∞ :
    /// - Ollama gère les suggestions rapides et locales ;
    /// - XENO gère les opérations structurées sur projet complet.
    /// </summary>
    public class MotoAi
    {
        private readonly OllamaClient _ollama;
        private readonly HttpXenoClient _xeno;

        private readonly Dictionary<string, int> _commandHabits =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, int> _fileHabits =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Événement utilisé pour afficher une suggestion MOTO dans l'UI.
        /// </summary>
        public event Action<string> SuggestionReady;

        public MotoAi(OllamaClient ollama, HttpXenoClient xeno)
        {
            _ollama = ollama;
            _xeno = xeno;
        }

        /// <summary>
        /// Enregistre une commande terminal utilisée par l'utilisateur.
        /// Sert à la prédiction d'habitudes.
        /// </summary>
        public void RecordCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            Increment(_commandHabits, command);
            PredictNextCommand(command);
        }

        /// <summary>
        /// Enregistre un fichier ouvert.
        /// Sert à la prédiction de navigation et de contexte.
        /// </summary>
        public void RecordFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            Increment(_fileHabits, path);
        }

        /// <summary>
        /// Complétion locale via Ollama.
        /// Cette méthode est adaptée à une génération courte,
        /// pas à une refactorisation complète de projet.
        /// </summary>
        public async Task<string> CompleteCodeAsync(string filePath, string code)
        {
            var prompt =
                "Tu es MOTO AI, un assistant de code local, précis et prudent.\n" +
                $"Fichier: {filePath}\n" +
                "Objectif: compléter uniquement le code manquant, sans casser l'existant.\n" +
                "Réponds avec le code ajouté seulement, sans explication.\n\n" +
                "Code actuel:\n" +
                code;

            return await _ollama.GenerateAsync(prompt);
        }

        /// <summary>
        /// Lance une opération projet via XENO-SSS∞.
        /// Exemple : run-full-pipeline, fix-architecture, generate-module.
        /// </summary>
        public async Task<XenoResponse> RunProjectOperationAsync(string workspacePath, string instruction)
        {
            var request = new XenoRequest
            {
                WorkspacePath = workspacePath,
                Goal = instruction,
                Mode = "xeno-sss"
            };

            return await _xeno.RunAsync(request);
        }

        private void Increment(Dictionary<string, int> map, string key)
        {
            map.TryGetValue(key, out var count);
            map[key] = count + 1;
        }

        private void PredictNextCommand(string lastCommand)
        {
            var predicted = _commandHabits
                .Where(kv => !string.Equals(kv.Key, lastCommand, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Key)
                .FirstOrDefault();

            if (predicted != null)
            {
                SuggestionReady?.Invoke($"MOTO prediction: prochaine commande probable '{predicted}'");
            }
        }
    }
}
