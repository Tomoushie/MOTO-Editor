// Moto.Core/I18n/AiTranslationEngine.cs
// Traduction IA temps réel via Ollama (local, sans API externe).
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Moto.Core.I18n
{
    /// <summary>
    /// Moteur de traduction IA utilisant Ollama local.
    /// Aucune API externe (Google, DeepL) : tout est local.
    /// </summary>
    public sealed class AiTranslationEngine : IDisposable
    {
        private readonly HttpClient _http;
        private readonly ILogger<AiTranslationEngine> _logger;
        private readonly string _ollamaUrl;
        private readonly string _model;
        private readonly Dictionary<string, string> _cache = new();
        private readonly SemaphoreSlim _gate = new(1, 1);

        public event Action<string, string, string>? TranslationCompleted; // key, source, target

        public AiTranslationEngine(
            ILogger<AiTranslationEngine> logger,
            string ollamaUrl = "http://localhost:11434",
            string model = "llama3.1")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ollamaUrl = ollamaUrl;
            _model = model;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>
        /// Traduit un texte vers la langue cible via Ollama.
        /// Cache les résultats pour éviter les appels répétés.
        /// </summary>
        public async Task<string> TranslateAsync(
            string text,
            string targetLanguageCode,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            var cacheKey = $"{targetLanguageCode}:{text.GetHashCode()}";

            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_cache.TryGetValue(cacheKey, out var cached))
                    return cached;
            }
            finally
            {
                _gate.Release();
            }

            try
            {
                var languageName = GetLanguageName(targetLanguageCode);
                var prompt = $"Translate the following text to {languageName}. " +
                             $"Return ONLY the translation, nothing else:\n\n{text}";

                var requestBody = new
                {
                    model = _model,
                    prompt = prompt,
                    stream = false
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _http.PostAsync($"{_ollamaUrl}/api/generate", content, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<OllamaResponse>(responseJson);

                var translation = result?.Response?.Trim() ?? text;

                await _gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    _cache[cacheKey] = translation;
                }
                finally
                {
                    _gate.Release();
                }

                TranslationCompleted?.Invoke(cacheKey, text, translation);
                return translation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AiTranslation] Erreur traduction");
                return text; // Fallback : texte original
            }
        }

        /// <summary>
        /// Traduit un pack de langue complet.
        /// </summary>
        public async Task<LanguagePack> TranslatePackAsync(
            LanguagePack sourcePack,
            string targetLanguageCode,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            var translatedPack = new LanguagePack
            {
                Id = targetLanguageCode,
                Name = GetLanguageName(targetLanguageCode),
                NativeName = GetNativeName(targetLanguageCode),
                Flag = GetFlag(targetLanguageCode),
                Version = sourcePack.Version,
                Author = "MOTO AI",
                Translations = new Dictionary<string, string>()
            };

            var total = sourcePack.Translations.Count;
            var done = 0;

            foreach (var (key, value) in sourcePack.Translations)
            {
                ct.ThrowIfCancellationRequested();

                var translated = await TranslateAsync(value, targetLanguageCode, ct).ConfigureAwait(false);
                translatedPack.Translations[key] = translated;

                done++;
                progress?.Report((double)done / total);
            }

            return translatedPack;
        }

        /// <summary>
        /// Traduit un document complet.
        /// </summary>
        public async Task<string> TranslateDocumentAsync(
            string content,
            string targetLanguageCode,
            CancellationToken ct = default)
        {
            // Découpe le document en chunks pour éviter les limites de contexte
            var chunks = SplitIntoChunks(content, maxChars: 2000);
            var translatedChunks = new List<string>();

            foreach (var chunk in chunks)
            {
                ct.ThrowIfCancellationRequested();
                var translated = await TranslateAsync(chunk, targetLanguageCode, ct).ConfigureAwait(false);
                translatedChunks.Add(translated);
            }

            return string.Join("\n", translatedChunks);
        }

        /// <summary>
        /// Détecte la langue d'un texte.
        /// </summary>
        public async Task<string> DetectLanguageAsync(string text, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "en";

            try
            {
                var prompt = $"Detect the language of the following text. " +
                             $"Return ONLY the 2-letter ISO code (en, fr, es, etc):\n\n{text.Substring(0, Math.Min(500, text.Length))}";

                var requestBody = new { model = _model, prompt, stream = false };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _http.PostAsync($"{_ollamaUrl}/api/generate", content, ct).ConfigureAwait(false);
                var responseJson = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<OllamaResponse>(responseJson);

                return result?.Response?.Trim().ToLowerInvariant() ?? "en";
            }
            catch
            {
                return "en";
            }
        }

        private static string GetLanguageName(string code) => code switch
        {
            "fr" => "French", "en" => "English", "ru" => "Russian", "zh" => "Chinese",
            "es" => "Spanish", "de" => "German", "it" => "Italian", "pt" => "Portuguese",
            "ja" => "Japanese", "ko" => "Korean", "ar" => "Arabic", "hi" => "Hindi",
            _ => code.ToUpper()
        };

        private static string GetNativeName(string code) => code switch
        {
            "fr" => "Français", "en" => "English", "ru" => "Русский", "zh" => "中文",
            "es" => "Español", "de" => "Deutsch", "it" => "Italiano", "pt" => "Português",
            "ja" => "日本語", "ko" => "한국어", "ar" => "العربية", "hi" => "हिन्दी",
            _ => code.ToUpper()
        };

        private static string GetFlag(string code) => code switch
        {
            "fr" => "🇫🇷", "en" => "🇬🇧", "ru" => "🇷🇺", "zh" => "🇨🇳",
            "es" => "🇪🇸", "de" => "🇩🇪", "it" => "🇮🇹", "pt" => "🇵🇹",
            "ja" => "🇯🇵", "ko" => "🇰🇷", "ar" => "🇸🇦", "hi" => "🇮🇳",
            _ => "🏳️"
        };

        private static List<string> SplitIntoChunks(string text, int maxChars)
        {
            var chunks = new List<string>();
            var lines = text.Split('\n');
            var currentChunk = "";

            foreach (var line in lines)
            {
                if (currentChunk.Length + line.Length > maxChars)
                {
                    if (!string.IsNullOrWhiteSpace(currentChunk))
                        chunks.Add(currentChunk);
                    currentChunk = line;
                }
                else
                {
                    currentChunk += "\n" + line;
                }
            }

            if (!string.IsNullOrWhiteSpace(currentChunk))
                chunks.Add(currentChunk);

            return chunks;
        }

        public void Dispose()
        {
            _http.Dispose();
            _gate.Dispose();
        }

        private sealed class OllamaResponse
        {
            public string? Response { get; set; }
        }
    }
}
