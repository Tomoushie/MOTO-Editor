// Moto.Editor/Platforms/Windows/ToastNotificationService.cs
#if WINDOWS
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;

namespace Moto.Editor.Platforms.Windows
{
    /// <summary>
    /// Service de notifications toast Windows natif.
    /// Remplace l'overlay MAUI pour une intégration OS plus profonde.
    /// </summary>
    public static class ToastNotificationService
    {
        private static bool _registered;

        /// <summary>
        /// Initialise le service de notifications.
        /// À appeler une seule fois au démarrage de l'app.
        /// </summary>
        public static void Initialize()
        {
            if (_registered) return;

            try
            {
                AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
                AppNotificationManager.Default.Register();
                _registered = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Toast] Échec enregistrement : {ex.Message}");
            }
        }

        /// <summary>
        /// Affiche un toast de notification Windows natif.
        /// </summary>
        /// <param name="title">Titre du toast.</param>
        /// <param name="message">Message principal.</param>
        /// <param name="icon">Icône optionnelle (emoji ou path).</param>
        public static void Show(string title, string message, string icon = "🔔")
        {
            if (!_registered)
            {
                System.Diagnostics.Debug.WriteLine("[Toast] Service non initialisé.");
                return;
            }

            try
            {
                var builder = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(message);

                // Ajoute une icône si fournie
                if (!string.IsNullOrWhiteSpace(icon))
                {
                    // Note: Windows toast n'accepte pas les emojis directement,
                    // on les met dans le titre
                }

                var notification = builder.BuildNotification();
                AppNotificationManager.Default.Show(notification);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Toast] Échec affichage : {ex.Message}");
            }
        }

        /// <summary>
        /// Affiche un toast de succès (vert).
        /// </summary>
        public static void ShowSuccess(string title, string message)
            => Show($"✅ {title}", message);

        /// <summary>
        /// Affiche un toast d'avertissement (jaune).
        /// </summary>
        public static void ShowWarning(string title, string message)
            => Show($"⚠️ {title}", message);

        /// <summary>
        /// Affiche un toast d'erreur (rouge).
        /// </summary>
        public static void ShowError(string title, string message)
            => Show($"❌ {title}", message);

        /// <summary>
        /// Affiche un toast d'information (bleu).
        /// </summary>
        public static void ShowInfo(string title, string message)
            => Show($"ℹ️ {title}", message);

        private static void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
        {
            // Callback quand l'utilisateur clique sur le toast
            System.Diagnostics.Debug.WriteLine($"[Toast] Notification cliquée : {args.Arguments}");
        }

        /// <summary>
        /// Désenregistre le service (à appeler à la fermeture de l'app).
        /// </summary>
        public static void Unregister()
        {
            if (_registered)
            {
                AppNotificationManager.Default.Unregister();
                _registered = false;
            }
        }
    }
}
#endif
