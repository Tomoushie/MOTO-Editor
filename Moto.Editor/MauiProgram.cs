// Moto.Editor/MauiProgram.cs (v31 — DI centralisée + FeatureFlag bindings)
using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Hosting;
using Moto.Core.DevOps;
using Moto.Core.Settings;
using Moto.Editor.DependencyInjection;

namespace Moto.Editor
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // ── Hook de migration AVANT toute résolution de SettingsEngine.Shared ──
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MotoEditor",
                "settings.json");

            var migrationLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsMigrationEngine>();
            var migrationEngine = new SettingsMigrationEngine(migrationLogger);
            var migrationResult = migrationEngine.MigrateIfNeeded(settingsPath);

            if (!migrationResult.Success)
            {
                System.Diagnostics.Debug.WriteLine($"[Migration] {migrationResult.Message}");
            }
            else if (migrationResult.MigratedKeys > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[Migration] {migrationResult.Message}");
            }

            // ── Construction de l'application MAUI ──
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // ── Logging ──
            builder.Logging.AddDebug();

#if WINDOWS
            // ── Handlers natifs Windows : neutralisation du chrome ──
            Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping("NoNative", (h, v) =>
            {
                h.PlatformView.Style = null;
                h.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                h.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(0);
            });

            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoNative", (h, v) =>
            {
                h.PlatformView.Style = null;
                h.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                h.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(0);
            });

            Microsoft.Maui.Handlers.BorderHandler.Mapper.AppendToMapping("NoNative", (h, v) =>
            {
                // Volontairement neutre
            });

            // ── Initialise le service toast natif Windows ──
            Moto.Editor.Platforms.Windows.ToastNotificationService.Initialize();
#endif

            // ── Stocke le résultat de migration pour MainPage ──
            if (migrationResult.Success && migrationResult.MigratedKeys > 0)
            {
                builder.Services.AddSingleton(migrationResult);
            }

            // ══════════════════════════════════════════════════════════════
            // ★ Tous les services MOTO via RegisterMotoServices (source unique de vérité)
            // ══════════════════════════════════════════════════════════════
            builder.Services.RegisterMotoServices();

            // ── Build de l'application ──
            var app = builder.Build();

            // ══════════════════════════════════════════════════════════════
            // ★ Bindings FeatureFlag → Settings (APRÈS le build, quand les services sont disponibles)
            // ══════════════════════════════════════════════════════════════
            var serviceProvider = app.Services;
            var flags = serviceProvider.GetRequiredService<FeatureFlagService>();
            var settings = SettingsEngine.Shared;

            flags.BindToSetting("feature.command_palette",       settings.Shared.Editor.Ux.CommandPaletteEnabled);
            flags.BindToSetting("feature.proactive_suggestions", settings.Shared.Editor.Ux.ProactiveSuggestionsEnabled);
            flags.BindToSetting("feature.context_engine",        settings.Shared.Editor.Ux.ContextEngineEnabled);

            System.Diagnostics.Debug.WriteLine("[FeatureFlags] Bindings attachés aux settings.");

            return app;
        }
    }
}
