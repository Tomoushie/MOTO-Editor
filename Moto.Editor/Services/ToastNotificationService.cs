using System.Diagnostics;
using Moto.Core.Settings;

namespace Moto.Editor.Services;

/// <summary>
/// Service de notifications toast multiplateforme.
/// Utilise ToastNotificationManager sur Windows, fallback sur les autres plateformes.
/// </summary>
public sealed class ToastNotificationService
{
    private readonly SettingsEngine _settings;

    public ToastNotificationService(SettingsEngine settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Affiche un toast quand un modèle est téléchargé avec succès.
    /// </summary>
    public async Task ShowModelDownloadedToast(string modelName)
    {
        if (!_settings.GetBool("ai.embedded.notifyDownload", defaultValue: true))
            return;

        await ShowToastAsync(
            title: "📥 Modèle téléchargé",
            message: $"Le modèle '{modelName}' a été téléchargé avec succès.");
    }

    /// <summary>
    /// Affiche un toast quand un benchmark se termine.
    /// </summary>
    public async Task ShowBenchmarkCompletedToast(string result)
    {
        if (!_settings.GetBool("ai.embedded.notifyBenchmark", defaultValue: true))
            return;

        await ShowToastAsync(
            title: "📊 Benchmark terminé",
            message: result);
    }

    private async Task ShowToastAsync(string title, string message)
    {
        try
        {
#if WINDOWS
            await ShowWindowsToastAsync(title, message);
#elif ANDROID || IOS || MACCATALYST
            await ShowMauiToastAsync(title, message);
#else
            Debug.WriteLine($"[Toast] {title}: {message}");
#endif
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ToastNotificationService] Failed to show toast: {ex.Message}");
        }
    }

#if WINDOWS
    private static async Task ShowWindowsToastAsync(string title, string message)
    {
        // WinUI 3 Toast Notification
        // ★ global:: nécessaire : "Windows" est aussi un namespace de ce projet
        // (Moto.Editor.Windows), qui masquerait sinon la racine WinRT "Windows.UI".
        var toastNotifier = global::Windows.UI.Notifications.ToastNotificationManager.CreateToastNotifier();
        var toastXml = global::Windows.UI.Notifications.ToastNotificationManager.GetTemplateContent(
            global::Windows.UI.Notifications.ToastTemplateType.ToastText02);

        var toastText = toastXml.GetElementsByTagName("text");
        toastText[0].AppendChild(toastXml.CreateTextNode(title));
        toastText[1].AppendChild(toastXml.CreateTextNode(message));

        var toast = new global::Windows.UI.Notifications.ToastNotification(toastXml);
        toastNotifier.Show(toast);

        await Task.CompletedTask;
    }
#endif

    private static async Task ShowMauiToastAsync(string title, string message)
    {
        // Fallback MAUI : utilise une alerte simple
        await Application.Current?.Windows[0].Page?.DisplayAlert(title, message, "OK")!;
    }
}
