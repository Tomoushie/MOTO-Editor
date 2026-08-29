// Moto.Core/I18n/DocumentTranslationAdvisor.cs
// Détecte les opportunités de traduction de manière proactive.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.I18n
{
    public sealed class TranslationSuggestion
    {
        public string FilePath { get; init; } = string.Empty;
        public string DetectedLanguage { get; init; } = string.Empty;
        public string SuggestedTargetLanguage { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public double Confidence { get; init; }
    }

    /// <summary>
    /// Analyse le contexte utilisateur pour proposer des traductions pertinentes.
    /// Détection : langue Windows, fichiers étrangers, patterns de code.
    /// </summary>
    public sealed class DocumentTranslationAdvisor
    {
        private readonly AiTranslationEngine _translationEngine;
        private readonly ILogger<DocumentTranslationAdvisor> _logger;
        private readonly List<TranslationSuggestion> _suggestions = new();

        public event Action<TranslationSuggestion>? SuggestionGenerated;

        public DocumentTranslationAdvisor(
            AiTranslationEngine translationEngine,
            ILogger<DocumentTranslationAdvisor> logger)
        {
            _translationEngine = translationEngine;
            _logger = logger;
        }

        /// <summary>
        /// Analyse un fichier ouvert et génère des suggestions de traduction.
        /// </summary>
        public async Task<IReadOnlyList<TranslationSuggestion>> AnalyzeFileAsync(
            string filePath,
            string content,
            CancellationToken ct = default)
        {
            var suggestions = new List<TranslationSuggestion>();

            // 1. Détecter la langue du document
            var detectedLang = await _translationEngine.DetectLanguageAsync(content, ct).ConfigureAwait(false);

            // 2. Obtenir la langue système (Windows)
            var systemLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            // 3. Si le document est dans une langue différente du système
            if (detectedLang != systemLang)
            {
                suggestions.Add(new TranslationSuggestion
                {
                    FilePath = filePath,
                    DetectedLanguage = detectedLang,
                    SuggestedTargetLanguage = systemLang,
                    Reason = $"Document en {detectedLang}, votre système est en {systemLang}",
                    Confidence = 0.9
                });
            }

            // 4. Analyser les fichiers du projet pour détecter des patterns multilingues
            var projectFiles = await ScanProjectForLanguagesAsync(filePath, ct).ConfigureAwait(false);
            foreach (var (lang, count) in projectFiles)
            {
                if (count >= 3 && lang != detectedLang)
                {
                    suggestions.Add(new TranslationSuggestion
                    {
                        FilePath = filePath,
                        DetectedLanguage = detectedLang,
                        SuggestedTargetLanguage = lang,
                        Reason = $"Le projet contient {count} fichiers en {lang}",
                        Confidence = 0.7
                    });
                }
            }

            foreach (var suggestion in suggestions)
            {
                SuggestionGenerated?.Invoke(suggestion);
            }

            return suggestions;
        }

        /// <summary>
        /// Scanne le projet pour détecter les langues utilisées.
        /// </summary>
        private async Task<Dictionary<string, int>> ScanProjectForLanguagesAsync(
            string currentFilePath,
            CancellationToken ct)
        {
            var languageCounts = new Dictionary<string, int>();
            var projectDir = Path.GetDirectoryName(currentFilePath);

            if (string.IsNullOrEmpty(projectDir))
                return languageCounts;

            try
            {
                var files = Directory.GetFiles(projectDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("bin") && !f.Contains("obj") && !f.Contains(".git"))
                    .Take(50) // Limite pour les performances
                    .ToList();

                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();

                    if (file == currentFilePath)
                        continue;

                    try
                    {
                        var content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                        if (content.Length < 50)
                            continue;

                        var lang = await _translationEngine.DetectLanguageAsync(content, ct).ConfigureAwait(false);

                        if (!languageCounts.ContainsKey(lang))
                            languageCounts[lang] = 0;
                        languageCounts[lang]++;
                    }
                    catch
                    {
                        // Fichier non lisible : ignoré
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TranslationAdvisor] Erreur scan projet");
            }

            return languageCounts;
        }
    }
}
