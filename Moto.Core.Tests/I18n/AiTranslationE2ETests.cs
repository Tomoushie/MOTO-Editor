// Moto.Core.Tests/I18n/AiTranslationE2ETests.cs
// Tests E2E : valider la traduction IA temps réel.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moto.Core.I18n;
using Xunit;

namespace Moto.Core.Tests.I18n
{
    // Dossier de données temporaire pour LanguageManager, supprimé après chaque test :
    // sans lui, le changement de langue ("es") était enregistré dans le vrai
    // %AppData%\MotoEditor\language-settings.json.
    public class AiTranslationE2ETests : IDisposable
    {
        private readonly string _tempDir;

        public AiTranslationE2ETests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        [Fact]
        public async Task E2E_TranslateText_ReturnsNonEmptyResult()
        {
            // GIVEN : un moteur de traduction avec Ollama local
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<AiTranslationEngine>();
            var engine = new AiTranslationEngine(logger);

            // WHEN : traduction d'un texte simple
            var result = await engine.TranslateAsync("Hello World", "fr");

            // THEN : le résultat n'est pas vide
            Assert.False(string.IsNullOrWhiteSpace(result));
        }

        [Fact]
        public async Task E2E_TranslatePack_AllKeysTranslated()
        {
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<AiTranslationEngine>();
            var engine = new AiTranslationEngine(logger);

            var sourcePack = new LanguagePack
            {
                Id = "fr",
                Name = "French",
                Translations = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["app.title"] = "MOTO Editor",
                    ["menu.file"] = "Fichier",
                    ["action.save"] = "Enregistrer"
                }
            };

            var translatedPack = await engine.TranslatePackAsync(sourcePack, "es");

            Assert.Equal("es", translatedPack.Id);
            Assert.Equal(sourcePack.Translations.Count, translatedPack.Translations.Count);
        }

        [Fact]
        public async Task E2E_DetectLanguage_ReturnsValidCode()
        {
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<AiTranslationEngine>();
            var engine = new AiTranslationEngine(logger);

            var detectedLang = await engine.DetectLanguageAsync("Bonjour le monde");

            Assert.False(string.IsNullOrEmpty(detectedLang));
            Assert.Equal(2, detectedLang.Length); // Code ISO 2 lettres
        }

        [Fact]
        public async Task E2E_LiveLanguageSwitch_NoRestartRequired()
        {
            var langManagerLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<LanguageManager>();
            var translationLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<AiTranslationEngine>();
            var switcherLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveLanguageSwitcher>();

            var langManager = new LanguageManager(langManagerLogger, _tempDir);
            var translationEngine = new AiTranslationEngine(translationLogger);
            var switcher = new LiveLanguageSwitcher(langManager, translationEngine, switcherLogger);

            // WHEN : changement de langue en temps réel
            await switcher.SwitchLanguageAsync("es");

            // THEN : la langue est changée sans redémarrage
            Assert.Equal("es", langManager.CurrentLanguageCode);

            // … et enregistrée dans le dossier temporaire.
            var settingsPath = Path.Combine(_tempDir, "language-settings.json");
            Assert.True(File.Exists(settingsPath));
            Assert.Contains("\"es\"", File.ReadAllText(settingsPath));
        }

        [Fact]
        public async Task E2E_TranslationAdvisor_DetectsForeignLanguage()
        {
            var translationLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<AiTranslationEngine>();
            var advisorLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentTranslationAdvisor>();

            var translationEngine = new AiTranslationEngine(translationLogger);
            var advisor = new DocumentTranslationAdvisor(translationEngine, advisorLogger);

            // GIVEN : un document en français
            var suggestions = await advisor.AnalyzeFileAsync(
                "/test/document.cs",
                "Console.WriteLine(\"Bonjour le monde\");");

            // THEN : des suggestions sont générées si la langue diffère du système
            Assert.NotNull(suggestions);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort */ }
        }
    }
}
