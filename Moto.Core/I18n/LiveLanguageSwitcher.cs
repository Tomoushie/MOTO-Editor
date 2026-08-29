// Moto.Core/I18n/LiveLanguageSwitcher.cs
// Changement de langue en temps réel sans redémarrage.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;

namespace Moto.Core.I18n
{
    /// <summary>
    /// Permet de changer la langue de l'application en temps réel.
    /// Utilise AiTranslationEngine pour les langues non natives.
    /// </summary>
    public sealed class LiveLanguageSwitcher
    {
        private readonly LanguageManager _languageManager;
        private readonly AiTranslationEngine _translationEngine;
        private readonly ILogger<LiveLanguageSwitcher> _logger;
        private readonly Dictionary<string, Dictionary<string, string>> _runtimeTranslations = new();

        public event Action<string>? LanguageSwitched;

        public LiveLanguageSwitcher(
            LanguageManager languageManager,
            AiTranslationEngine translationEngine,
            ILogger<LiveLanguageSwitcher> logger)
        {
            _languageManager = languageManager ?? throw new ArgumentNullException(nameof(languageManager));
            _translationEngine = translationEngine ?? throw new ArgumentNullException(nameof(translationEngine));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Change la langue immédiatement sans redémarrage.
        /// </summary>
        public async Task SwitchLanguageAsync(string targetCode, CancellationToken ct = default)
        {
            _logger.LogInformation("[LiveSwitch] Changement vers {Code}", targetCode);

            // 1. Vérifier si c'est une langue native
            var nativePacks = _languageManager.GetAllLanguages();
            var isNative = false;
            foreach (var lang in nativePacks)
            {
                if (lang.Code == targetCode && lang.IsBuiltIn)
                {
                    isNative = true;
                    break;
                }
            }

            if (!isNative)
            {
                // 2. Traduire via IA si nécessaire
                await TranslateMissingKeysAsync(targetCode, ct).ConfigureAwait(false);
            }

            // 3. Appliquer le changement
            _languageManager.SetLanguage(targetCode);
            LanguageSwitched?.Invoke(targetCode);

            _logger.LogInformation("[LiveSwitch] Langue changée : {Code}", targetCode);
        }

        /// <summary>
        /// Traduit les clés manquantes via IA.
        /// </summary>
        private async Task TranslateMissingKeysAsync(string targetCode, CancellationToken ct)
        {
            if (!_runtimeTranslations.ContainsKey(targetCode))
                _runtimeTranslations[targetCode] = new Dictionary<string, string>();

            var sourcePack = _languageManager.GetAllLanguages()[0]; // FR comme base
            var targetTranslations = _runtimeTranslations[targetCode];

            // Traduire uniquement les clés non traduites
            var keysToTranslate = new List<string>();
            foreach (var key in GetBaseKeys())
            {
                if (!targetTranslations.ContainsKey(key))
                    keysToTranslate.Add(key);
            }

            if (keysToTranslate.Count == 0)
                return;

            _logger.LogInformation("[LiveSwitch] Traduction de {Count} clés via IA", keysToTranslate.Count);

            foreach (var key in keysToTranslate)
            {
                ct.ThrowIfCancellationRequested();

                var sourceText = GetBaseText(key);
                var translated = await _translationEngine.TranslateAsync(sourceText, targetCode, ct).ConfigureAwait(false);
                targetTranslations[key] = translated;
            }
        }

        /// <summary>
        /// Obtient la traduction runtime (après changement live).
        /// </summary>
        public string Translate(string key, string targetCode)
        {
            // Priorité : runtime > natif
            if (_runtimeTranslations.TryGetValue(targetCode, out var runtime) &&
                runtime.TryGetValue(key, out var value))
                return value;

            return _languageManager.Translate(key);
        }

        private static List<string> GetBaseKeys() => new()
        {
            "app.title", "menu.file", "menu.edit", "menu.view", "menu.run",
            "menu.ai", "menu.help", "action.open", "action.save", "action.build",
            "status.ready", "update.available", "language.name"
        };

        private static string GetBaseText(string key) => key switch
        {
            "app.title" => "MOTO Editor",
            "menu.file" => "Fichier",
            "menu.edit" => "Édition",
            "menu.view" => "Affichage",
            "menu.run" => "Exécuter",
            "menu.ai" => "IA",
            "menu.help" => "Aide",
            "action.open" => "Ouvrir",
            "action.save" => "Enregistrer",
            "action.build" => "Compiler",
            "status.ready" => "Prêt",
            "update.available" => "Mise à jour disponible",
            "language.name" => "Français",
            _ => key
        };
    }
}
