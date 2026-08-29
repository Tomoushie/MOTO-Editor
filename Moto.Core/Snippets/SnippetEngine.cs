// Moto.Core/Snippets/SnippetEngine.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Moto.Core.Snippets
{
    /// <summary>
    /// Moteur de snippets avec support des variables et preview.
    /// </summary>
    public sealed class SnippetEngine
    {
        private readonly ILogger<SnippetEngine> _logger;
        private readonly string _snippetsDir;
        private readonly List<Snippet> _snippets = new();

        public event Action<Snippet>? SnippetAdded;

        public SnippetEngine(ILogger<SnippetEngine> logger)
        {
            _logger = logger;
            _snippetsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MotoEditor", "snippets");
            Directory.CreateDirectory(_snippetsDir);
            LoadSnippets();
        }

        /// <summary>
        /// Insère un snippet avec substitution des variables.
        /// </summary>
        public string RenderSnippet(Snippet snippet, Dictionary<string, string>? variables = null)
        {
            var result = snippet.Body;

            if (variables != null)
            {
                foreach (var (key, value) in variables)
                {
                    result = result.Replace($"${{{key}}}", value);
                }
            }

            return result;
        }

        /// <summary>
        /// Extrait les variables d'un snippet pour le preview.
        /// </summary>
        public List<string> ExtractVariables(Snippet snippet)
        {
            var variables = new List<string>();
            var matches = System.Text.RegularExpressions.Regex.Matches(snippet.Body, @"\$\{(\w+)\}");

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var varName = match.Groups[1].Value;
                if (!variables.Contains(varName))
                    variables.Add(varName);
            }

            return variables;
        }

        /// <summary>
        /// Crée un snippet personnalisé.
        /// </summary>
        public void CreateSnippet(Snippet snippet)
        {
            _snippets.Add(snippet);
            SaveSnippet(snippet);
            SnippetAdded?.Invoke(snippet);
        }

        /// <summary>
        /// Obtient les snippets pour un langage.
        /// </summary>
        public IReadOnlyList<Snippet> GetSnippetsForLanguage(string language)
        {
            return _snippets
                .Where(s => s.Language == language)
                .OrderBy(s => s.Trigger)
                .ToList();
        }

        /// <summary>
        /// Recherche un snippet par trigger.
        /// </summary>
        public Snippet? FindByTrigger(string trigger, string language)
        {
            return _snippets.FirstOrDefault(s =>
                s.Trigger == trigger && s.Language == language);
        }

        private void LoadSnippets()
        {
            try
            {
                var files = Directory.GetFiles(_snippetsDir, "*.json");
                foreach (var file in files)
                {
                    var json = File.ReadAllText(file);
                    var snippet = JsonSerializer.Deserialize<Snippet>(json);
                    if (snippet != null)
                        _snippets.Add(snippet);
                }

                _logger.LogInformation("[Snippets] {Count} snippets chargés", _snippets.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Snippets] Erreur chargement");
            }
        }

        private void SaveSnippet(Snippet snippet)
        {
            try
            {
                var path = Path.Combine(_snippetsDir, $"{snippet.Id}.json");
                var json = JsonSerializer.Serialize(snippet, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Snippets] Erreur sauvegarde");
            }
        }
    }
}
