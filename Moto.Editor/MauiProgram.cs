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
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace Moto.Editor
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            App.Breadcrumb("MauiProgram.CreateMauiApp — entrée");

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

            App.Breadcrumb("MauiProgram — avant MauiApp.CreateBuilder()");

            // ── Construction de l'application MAUI ──
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit() // FolderPicker.Default (FileExplorerView/MainPage.UI)
                .UseSkiaSharp() // CodeEditorViewSkia (chantier rendu direct, incrément 1 — 22/09)
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            App.Breadcrumb("MauiProgram — builder configuré (fonts/toolkit OK)");

            // ── Logging ──
            builder.Logging.AddDebug();

#if WINDOWS
            // ── Handlers natifs Windows : neutralisation du chrome ──
            Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping("NoNative", (h, v) =>
            {
                h.PlatformView.Style = null;
                h.PlatformView.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                h.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(0);

                // ★ AJOUT (26/09, passe « moyen → élevé ») : les boutons du système de design (MotoPrimary/Secondary/
                // Ghost/Danger/IconButton, Themes/MotoTheme.xaml) retrouvent leur marge intérieure, leur bordure et leur
                // arrondi — la remise à zéro ci-dessus, faite APRÈS les réglages MAUI, les réduisait à du texte collé sur
                // un aplat carré (vu sur capture : « Refuser »/« Appliquer »). Les autres boutons ne changent pas.
                if (v is Microsoft.Maui.Controls.Button button && IsDesignSystemButton(button.Style))
                {
                    // Bordure : on enlève la valeur posée en dur pour que l'épaisseur/couleur fournies par MAUI (et leurs
                    // changements d'état, ex. le contour de focus) s'appliquent. Arrondi : posé directement (aucun état ne le change).
                    h.PlatformView.ClearValue(Microsoft.UI.Xaml.Controls.Control.BorderThicknessProperty);
                    h.UpdateValue(nameof(Microsoft.Maui.IPadding.Padding));
                    h.UpdateValue(nameof(Microsoft.Maui.IButtonStroke.StrokeThickness));
                    h.UpdateValue(nameof(Microsoft.Maui.IButtonStroke.StrokeColor));
                    h.UpdateValue(nameof(Microsoft.Maui.IButtonStroke.CornerRadius));
                    h.PlatformView.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(Math.Max(0, button.CornerRadius));
                }
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
            App.Breadcrumb("MauiProgram — avant RegisterMotoServices()");
            try
            {
                builder.Services.RegisterMotoServices();
            }
            catch (Exception ex)
            {
                App.LogCrash("MauiProgram — RegisterMotoServices()", ex);
                throw;
            }
            App.Breadcrumb("MauiProgram — RegisterMotoServices() OK");

            // ── Build de l'application ──
            App.Breadcrumb("MauiProgram — avant builder.Build()");
            MauiApp app;
            try
            {
                app = builder.Build();
            }
            catch (Exception ex)
            {
                App.LogCrash("MauiProgram — builder.Build()", ex);
                throw;
            }
            App.Breadcrumb("MauiProgram — builder.Build() OK");

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

        /// <summary>
        /// ★ AJOUT (26/09) : vrai si le bouton porte un des styles du système de design (Themes/MotoTheme.xaml). Comparaison
        /// par référence : un style posé par StaticResource EST l'objet du dictionnaire. Voir le mappage « NoNative ».
        /// </summary>
        private static readonly string[] DesignSystemButtonStyles =
            { "MotoPrimaryButton", "MotoSecondaryButton", "MotoGhostButton", "MotoDangerButton", "MotoIconButton" };

        private static bool IsDesignSystemButton(Microsoft.Maui.Controls.Style? style)
        {
            if (style is null || Microsoft.Maui.Controls.Application.Current is not { } app) return false;
            foreach (var key in DesignSystemButtonStyles)
                if (app.Resources.TryGetValue(key, out var candidate) && ReferenceEquals(candidate, style))
                    return true;
            return false;
        }
    }
}
