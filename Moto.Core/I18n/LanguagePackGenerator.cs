using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Moto.Core.I18n
{
    /// <summary>
    /// Générateur de packs de langues à partir de templates.
    /// Permet de créer rapidement 50+ packs avec structure cohérente.
    /// </summary>
    public sealed class LanguagePackGenerator
    {
        private readonly ILogger<LanguagePackGenerator> _logger;
        private readonly Dictionary<string, string> _baseTranslations;

        public LanguagePackGenerator(ILogger<LanguagePackGenerator> logger)
        {
            _logger = logger;
            _baseTranslations = LoadBaseTranslations();
        }

        /// <summary>
        /// Génère un pack de langue pour un code ISO donné.
        /// </summary>
        public LanguagePack GeneratePack(string isoCode, string nativeName, string flag)
        {
            var pack = new LanguagePack
            {
                Id = isoCode,
                Name = GetEnglishName(isoCode),
                NativeName = nativeName,
                Flag = flag,
                Version = "1.0.0",
                Author = "Community",
                Contributors = new List<string>(),
                LastUpdated = DateTime.UtcNow,
                Translations = new Dictionary<string, string>(_baseTranslations),
                Metadata = new LanguagePackMetadata
                {
                    DownloadCount = 0,
                    Rating = 0.0,
                    Verified = false
                }
            };

            return pack;
        }

        /// <summary>
        /// Génère tous les packs de langues supportés.
        /// </summary>
        public IReadOnlyList<LanguagePack> GenerateAllPacks()
        {
            var packs = new List<LanguagePack>();
            var languages = new[]
            {
                ("es", "Español", "🇪🇸"),
                ("de", "Deutsch", "🇩🇪"),
                ("it", "Italiano", "🇮🇹"),
                ("pt", "Português", "🇵🇹"),
                ("nl", "Nederlands", "🇳🇱"),
                ("pl", "Polski", "🇵🇱"),
                ("sv", "Svenska", "🇸🇪"),
                ("no", "Norsk", "🇳🇴"),
                ("da", "Dansk", "🇩🇰"),
                ("fi", "Suomi", "🇫🇮"),
                ("tr", "Türkçe", "🇹🇷"),
                ("ar", "العربية", "🇸🇦"),
                ("he", "עברית", "🇮🇱"),
                ("hi", "हिन्दी", "🇮🇳"),
                ("th", "ไทย", "🇹🇭"),
                ("vi", "Tiếng Việt", "🇻🇳"),
                ("id", "Bahasa Indonesia", "🇮🇩"),
                ("ms", "Bahasa Melayu", "🇲🇾"),
                ("ko", "한국어", "🇰🇷"),
                ("ja", "日本語", "🇯🇵"),
                ("cs", "Čeština", "🇨🇿"),
                ("hu", "Magyar", "🇭🇺"),
                ("ro", "Română", "🇷🇴"),
                ("bg", "Български", "🇧🇬"),
                ("el", "Ελληνικά", "🇬🇷"),
                ("uk", "Українська", "🇺🇦"),
                ("sr", "Српски", "🇷🇸"),
                ("hr", "Hrvatski", "🇭🇷"),
                ("sk", "Slovenčina", "🇸🇰"),
                ("sl", "Slovenščina", "🇸🇮"),
                ("et", "Eesti", "🇪🇪"),
                ("lv", "Latviešu", "🇱🇻"),
                ("lt", "Lietuvių", "🇱🇹"),
                ("mt", "Malti", "🇲🇹"),
                ("ga", "Gaeilge", "🇮🇪"),
                ("cy", "Cymraeg", "🏴󠁧󠁢󠁷󠁬󠁳󠁿"),
                ("gd", "Gàidhlig", "🏴󠁧󠁢󠁳󠁣󠁴󠁿"),
                ("eu", "Euskara", "🇪🇸"),
                ("ca", "Català", "🇪🇸"),
                ("gl", "Galego", "🇪🇸"),
                ("af", "Afrikaans", "🇿🇦"),
                ("sw", "Kiswahili", "🇹🇿"),
                ("zu", "isiZulu", "🇿🇦"),
                ("xh", "isiXhosa", "🇿🇦"),
                ("bn", "বাংলা", "🇧🇩"),
                ("ta", "தமிழ்", "🇮🇳"),
                ("te", "తెలుగు", "🇮🇳"),
                ("mr", "मराठी", "🇮🇳"),
                ("gu", "ગુજરાતી", "🇮🇳"),
                ("kn", "ಕನ್ನಡ", "🇮🇳"),
                ("ml", "മലയാളം", "🇮🇳"),
                ("pa", "ਪੰਜਾਬੀ", "🇮🇳"),
                ("ur", "اردو", "🇵🇰"),
                ("fa", "فارسی", "🇮🇷")
            };

            foreach (var (code, native, flag) in languages)
            {
                packs.Add(GeneratePack(code, native, flag));
            }

            _logger.LogInformation("[LanguagePackGenerator] {Count} packs générés", packs.Count);
            return packs;
        }

        /// <summary>
        /// Exporte tous les packs vers un répertoire.
        /// </summary>
        public void ExportAllPacks(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            var packs = GenerateAllPacks();

            foreach (var pack in packs)
            {
                var path = Path.Combine(outputDirectory, $"{pack.Id}.json");
                var json = JsonSerializer.Serialize(pack, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(path, json);
            }

            _logger.LogInformation("[LanguagePackGenerator] Packs exportés vers {Path}", outputDirectory);
        }

        private string GetEnglishName(string isoCode)
        {
            return isoCode switch
            {
                "es" => "Spanish",
                "de" => "German",
                "it" => "Italian",
                "pt" => "Portuguese",
                "nl" => "Dutch",
                "pl" => "Polish",
                "sv" => "Swedish",
                "no" => "Norwegian",
                "da" => "Danish",
                "fi" => "Finnish",
                "tr" => "Turkish",
                "ar" => "Arabic",
                "he" => "Hebrew",
                "hi" => "Hindi",
                "th" => "Thai",
                "vi" => "Vietnamese",
                "id" => "Indonesian",
                "ms" => "Malay",
                "ko" => "Korean",
                "ja" => "Japanese",
                "cs" => "Czech",
                "hu" => "Hungarian",
                "ro" => "Romanian",
                "bg" => "Bulgarian",
                "el" => "Greek",
                "uk" => "Ukrainian",
                "sr" => "Serbian",
                "hr" => "Croatian",
                "sk" => "Slovak",
                "sl" => "Slovenian",
                "et" => "Estonian",
                "lv" => "Latvian",
                "lt" => "Lithuanian",
                "mt" => "Maltese",
                "ga" => "Irish",
                "cy" => "Welsh",
                "gd" => "Scottish Gaelic",
                "eu" => "Basque",
                "ca" => "Catalan",
                "gl" => "Galician",
                "af" => "Afrikaans",
                "sw" => "Swahili",
                "zu" => "Zulu",
                "xh" => "Xhosa",
                "bn" => "Bengali",
                "ta" => "Tamil",
                "te" => "Telugu",
                "mr" => "Marathi",
                "gu" => "Gujarati",
                "kn" => "Kannada",
                "ml" => "Malayalam",
                "pa" => "Punjabi",
                "ur" => "Urdu",
                "fa" => "Persian",
                _ => isoCode.ToUpper()
            };
        }

        private Dictionary<string, string> LoadBaseTranslations()
        {
            // Template de base avec toutes les clés nécessaires
            return new Dictionary<string, string>
            {
                ["app.title"] = "MOTO Editor",
                ["app.version"] = "Version",
                ["menu.file"] = "File",
                ["menu.edit"] = "Edit",
                ["menu.view"] = "View",
                ["menu.run"] = "Run",
                ["menu.ai"] = "AI",
                ["menu.help"] = "Help",
                ["action.open"] = "Open",
                ["action.save"] = "Save",
                ["action.saveAll"] = "Save All",
                ["action.close"] = "Close",
                ["action.build"] = "Build",
                ["action.run"] = "Run",
                ["action.debug"] = "Debug",
                ["action.stop"] = "Stop",
                ["status.ready"] = "Ready",
                ["status.building"] = "Building…",
                ["status.running"] = "Running…",
                ["status.debugging"] = "Debugging…",
                ["error.fileNotFound"] = "File not found",
                ["error.buildFailed"] = "Build failed",
                ["update.available"] = "Update available",
                ["update.check"] = "Check for updates",
                ["update.current"] = "You are using the latest version",
                ["language.name"] = "English",
                ["language.select"] = "Select language",
                ["settings.title"] = "Settings",
                ["settings.theme"] = "Theme",
                ["settings.font"] = "Font",
                ["settings.language"] = "Language",
                ["info.version"] = "Version",
                ["info.changelog"] = "Changelog",
                ["info.developer"] = "Developed by",
                ["info.email"] = "Email",
                ["info.website"] = "Website",
                ["info.docs"] = "Documentation",
                ["info.bug"] = "Report a bug"
            };
        }
    }
}
