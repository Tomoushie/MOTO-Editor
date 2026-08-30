// Moto.Editor/MauiProgram.cs (v31 — DI centralisée + FeatureFlag bindings)
using System;
using System.IO;
using CommunityToolkit.Maui;
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
                .UseMauiCommunityToolkit() // FolderPicker.Default (FileExplorerView/MainPage.UI)
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
            // ★ Bindings FeatureFlag → Settings : mis de côté pour cette passe.
            // Le code supposait une API de réglages typés imbriqués
            // (settings.Shared.Editor.Ux.X, un objet "bindable") qui n'a jamais
            // été construite — SettingsEngine n'expose que Get/Set/GetBool à
            // plat (voir Moto.Core/Settings/SettingsEngineCore.cs). Les feature
            // flags gardent donc leurs valeurs par défaut tant que
            // FeatureFlagService n'a pas un vrai binding vers cette API plate.
            // ══════════════════════════════════════════════════════════════

            return app;
        }
    }
}
