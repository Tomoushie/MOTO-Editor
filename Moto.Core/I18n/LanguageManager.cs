// Moto.Core/I18n/LanguageManager.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Moto.Core.I18n
{
    public sealed class LanguageInfo
    {
        public string Code { get; init; } = string.Empty; // "fr", "en", "ru", "zh"
        public string Name { get; init; } = string.Empty; // "Français", "English", etc.
        public string NativeName { get; init; } = string.Empty;
        public string Flag { get; init; } = string.Empty; // "🇫🇷", "🇬🇧", etc.
        public bool IsBuiltIn { get; init; }
        public string? Author { get; init; }
        public DateTime? LastUpdatedUtc { get; init; }
    }

    public sealed class LanguagePack
    {
        public LanguageInfo Info { get; init; } = null!;
        public Dictionary<string, string> Translations { get; init; } = new();
    }

    /// <summary>
    /// Gestionnaire de langues avec support des packs installables.
    /// Langues natives : FR, EN, RU, ZH.
    /// </summary>
    public sealed class LanguageManager
    {
        private readonly ILogger<LanguageManager> _logger;
        private readonly string _languagesPath;
        private readonly string _settingsPath;
        private readonly Dictionary<string, LanguagePack> _loadedPacks = new();
        private LanguagePack _currentPack;
        private string _currentLanguageCode = "fr";

        public event Action<string>? LanguageChanged;

        // Langues intégrées nativement
        private static readonly LanguageInfo[] BuiltInLanguages = new[]
        {
            new LanguageInfo { Code = "fr", Name = "French", NativeName = "Français", Flag = "🇫🇷", IsBuiltIn = true },
            new LanguageInfo { Code = "en", Name = "English", NativeName = "English", Flag = "🇬🇧", IsBuiltIn = true },
            new LanguageInfo { Code = "ru", Name = "Russian", NativeName = "Русский", Flag = "🇷🇺", IsBuiltIn = true },
            new LanguageInfo { Code = "zh", Name = "Chinese", NativeName = "中文", Flag = "🇨🇳", IsBuiltIn = true }
        };

        public LanguageManager(ILogger<LanguageManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _languagesPath = Path.Combine(appData, "MotoEditor", "languages");
            _settingsPath = Path.Combine(appData, "MotoEditor", "language-settings.json");

            Directory.CreateDirectory(_languagesPath);

            // Charger les packs intégrés
            LoadBuiltInPacks();

            // Charger la langue sauvegardée
            LoadSettings();
            _currentPack = GetPack(_currentLanguageCode) ?? GetPack("fr")!;
        }

        public string CurrentLanguageCode => _currentLanguageCode;
        public LanguageInfo CurrentLanguage => _currentPack.Info;
        public IReadOnlyList<LanguageInfo> AvailableLanguages => GetAllLanguages();

        /// <summary>Change la langue active.</summary>
        public void SetLanguage(string code)
        {
            var pack = GetPack(code);
            if (pack == null)
            {
                _logger.LogWarning("[I18n] Langue non trouvée : {Code}", code);
                return;
            }

            _currentLanguageCode = code;
            _currentPack = pack;
            SaveSettings();

            // Appliquer la culture .NET
            try
            {
                var culture = new CultureInfo(code);
                CultureInfo.CurrentUICulture = culture;
                CultureInfo.CurrentCulture = culture;
            }
            catch { }

            LanguageChanged?.Invoke(code);
            _logger.LogInformation("[I18n] Langue changée : {Code}", code);
        }

        /// <summary>Traduit une clé.</summary>
        public string Translate(string key, string? fallback = null)
        {
            if (_currentPack.Translations.TryGetValue(key, out var translation))
                return translation;

            // Fallback sur l'anglais
            var enPack = GetPack("en");
            if (enPack != null && enPack.Translations.TryGetValue(key, out var enTranslation))
                return enTranslation;

            return fallback ?? key;
        }

        /// <summary>Raccourci pour Translate.</summary>
        public string T(string key, string? fallback = null) => Translate(key, fallback);

        /// <summary>Retourne toutes les langues disponibles (intégrées + installées).</summary>
        public IReadOnlyList<LanguageInfo> GetAllLanguages()
        {
            var result = new List<LanguageInfo>(BuiltInLanguages);

            // Ajouter les packs installés
            foreach (var file in Directory.GetFiles(_languagesPath, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var pack = JsonSerializer.Deserialize<LanguagePack>(json);
                    if (pack?.Info != null && !result.Any(l => l.Code == pack.Info.Code))
                        result.Add(pack.Info);
                }
                catch { }
            }

            return result.OrderBy(l => l.Name).ToList();
        }

        /// <summary>Installe un pack de langue depuis un fichier.</summary>
        public bool InstallLanguagePack(string sourcePath)
        {
            try
            {
                var json = File.ReadAllText(sourcePath);
                var pack = JsonSerializer.Deserialize<LanguagePack>(json);
                if (pack?.Info == null) return false;

                var destPath = Path.Combine(_languagesPath, $"{pack.Info.Code}.json");
                File.Copy(sourcePath, destPath, overwrite: true);

                _loadedPacks[pack.Info.Code] = pack;
                _logger.LogInformation("[I18n] Pack installé : {Code}", pack.Info.Code);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[I18n] Erreur installation pack");
                return false;
            }
        }

        /// <summary>Désinstalle un pack de langue.</summary>
        public bool UninstallLanguagePack(string code)
        {
            if (BuiltInLanguages.Any(l => l.Code == code))
            {
                _logger.LogWarning("[I18n] Impossible de désinstaller une langue intégrée");
                return false;
            }

            var path = Path.Combine(_languagesPath, $"{code}.json");
            if (File.Exists(path))
            {
                File.Delete(path);
                _loadedPacks.Remove(code);

                // Si c'était la langue active, revenir au français
                if (_currentLanguageCode == code)
                    SetLanguage("fr");

                return true;
            }
            return false;
        }

        private LanguagePack? GetPack(string code)
        {
            if (_loadedPacks.TryGetValue(code, out var pack))
                return pack;

            // Essayer de charger depuis le disque
            var path = Path.Combine(_languagesPath, $"{code}.json");
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    pack = JsonSerializer.Deserialize<LanguagePack>(json);
                    if (pack != null)
                    {
                        _loadedPacks[code] = pack;
                        return pack;
                    }
                }
                catch { }
            }
            return null;
        }

        private void LoadBuiltInPacks()
        {
            // Pack français (défaut)
            _loadedPacks["fr"] = new LanguagePack
            {
                Info = BuiltInLanguages.First(l => l.Code == "fr"),
                Translations = new Dictionary<string, string>
                {
                    ["app.title"] = "MOTO Editor",
                    ["menu.file"] = "Fichier",
                    ["menu.edit"] = "Édition",
                    ["menu.view"] = "Affichage",
                    ["menu.run"] = "Exécuter",
                    ["menu.ai"] = "IA",
                    ["menu.help"] = "Aide",
                    ["action.open"] = "Ouvrir",
                    ["action.save"] = "Enregistrer",
                    ["action.build"] = "Compiler",
                    ["action.run"] = "Exécuter",
                    ["action.debug"] = "Déboguer",
                    ["status.ready"] = "Prêt",
                    ["status.building"] = "Compilation…",
                    ["status.running"] = "Exécution…",
                    ["update.available"] = "Mise à jour disponible",
                    ["update.check"] = "Rechercher des mises à jour",
                    ["update.current"] = "Vous utilisez la dernière version",
                    ["language.name"] = "Français",
                    ["info.version"] = "Version",
                    ["info.changelog"] = "Journal des modifications",
                    ["info.developer"] = "Développé par",
                    ["info.email"] = "Email",
                    ["info.website"] = "Site web",
                    ["info.docs"] = "Documentation",
                    ["info.bug"] = "Signaler un bug"
                }
            };

            // Pack anglais
            _loadedPacks["en"] = new LanguagePack
            {
                Info = BuiltInLanguages.First(l => l.Code == "en"),
                Translations = new Dictionary<string, string>
                {
                    ["app.title"] = "MOTO Editor",
                    ["menu.file"] = "File",
                    ["menu.edit"] = "Edit",
                    ["menu.view"] = "View",
                    ["menu.run"] = "Run",
                    ["menu.ai"] = "AI",
                    ["menu.help"] = "Help",
                    ["action.open"] = "Open",
                    ["action.save"] = "Save",
                    ["action.build"] = "Build",
                    ["action.run"] = "Run",
                    ["action.debug"] = "Debug",
                    ["status.ready"] = "Ready",
                    ["status.building"] = "Building…",
                    ["status.running"] = "Running…",
                    ["update.available"] = "Update available",
                    ["update.check"] = "Check for updates",
                    ["update.current"] = "You are using the latest version",
                    ["language.name"] = "English",
                    ["info.version"] = "Version",
                    ["info.changelog"] = "Changelog",
                    ["info.developer"] = "Developed by",
                    ["info.email"] = "Email",
                    ["info.website"] = "Website",
                    ["info.docs"] = "Documentation",
                    ["info.bug"] = "Report a bug"
                }
            };

            // Pack russe
            _loadedPacks["ru"] = new LanguagePack
            {
                Info = BuiltInLanguages.First(l => l.Code == "ru"),
                Translations = new Dictionary<string, string>
                {
                    ["app.title"] = "MOTO Редактор",
                    ["menu.file"] = "Файл",
                    ["menu.edit"] = "Редактирование",
                    ["menu.view"] = "Вид",
                    ["menu.run"] = "Запуск",
                    ["menu.ai"] = "ИИ",
                    ["menu.help"] = "Помощь",
                    ["action.open"] = "Открыть",
                    ["action.save"] = "Сохранить",
                    ["action.build"] = "Собрать",
                    ["action.run"] = "Запустить",
                    ["action.debug"] = "Отладка",
                    ["status.ready"] = "Готов",
                    ["status.building"] = "Сборка…",
                    ["status.running"] = "Выполнение…",
                    ["update.available"] = "Доступно обновление",
                    ["update.check"] = "Проверить обновления",
                    ["update.current"] = "Вы используете последнюю версию",
                    ["language.name"] = "Русский",
                    ["info.version"] = "Версия",
                    ["info.changelog"] = "Журнал изменений",
                    ["info.developer"] = "Разработчик",
                    ["info.email"] = "Эл. почта",
                    ["info.website"] = "Веб-сайт",
                    ["info.docs"] = "Документация",
                    ["info.bug"] = "Сообщить об ошибке"
                }
            };

            // Pack chinois
            _loadedPacks["zh"] = new LanguagePack
            {
                Info = BuiltInLanguages.First(l => l.Code == "zh"),
                Translations = new Dictionary<string, string>
                {
                    ["app.title"] = "MOTO 编辑器",
                    ["menu.file"] = "文件",
                    ["menu.edit"] = "编辑",
                    ["menu.view"] = "视图",
                    ["menu.run"] = "运行",
                    ["menu.ai"] = "AI",
                    ["menu.help"] = "帮助",
                    ["action.open"] = "打开",
                    ["action.save"] = "保存",
                    ["action.build"] = "构建",
                    ["action.run"] = "运行",
                    ["action.debug"] = "调试",
                    ["status.ready"] = "就绪",
                    ["status.building"] = "构建中…",
                    ["status.running"] = "运行中…",
                    ["update.available"] = "有可用更新",
                    ["update.check"] = "检查更新",
                    ["update.current"] = "您使用的是最新版本",
                    ["language.name"] = "中文",
                    ["info.version"] = "版本",
                    ["info.changelog"] = "更新日志",
                    ["info.developer"] = "开发者",
                    ["info.email"] = "邮箱",
                    ["info.website"] = "网站",
                    ["info.docs"] = "文档",
                    ["info.bug"] = "报告错误"
                }
            };
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var settings = JsonSerializer.Deserialize<LanguageSettings>(json);
                    if (settings != null && !string.IsNullOrEmpty(settings.Code))
                        _currentLanguageCode = settings.Code;
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new LanguageSettings { Code = _currentLanguageCode };
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch { }
        }

        private sealed class LanguageSettings
        {
            public string Code { get; set; } = "fr";
        }
    }
}
