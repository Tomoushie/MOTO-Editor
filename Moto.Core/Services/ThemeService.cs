// Services/ThemeService.cs
using Microsoft.Maui.ApplicationModel;

namespace Moto.Editor.Services
{
    /// <summary>
    /// Service de thème clair / sombre.
    /// </summary>
    public static class ThemeService
    {
        public static void SetDark()
        {
            Application.Current.UserAppTheme = AppTheme.Dark;
        }

        public static void SetLight()
        {
            Application.Current.UserAppTheme = AppTheme.Light;
        }

        public static void FollowSystem()
        {
            Application.Current.UserAppTheme = AppTheme.Unspecified;
        }
    }
}
