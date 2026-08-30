// Moto.Editor/Platforms/Windows/GlobalHotkeyService.cs
#if WINDOWS
using System;
using Microsoft.Maui.ApplicationModel;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Moto.Editor.Platforms.Windows
{
    /// <summary>
    /// Enregistre CTRL+SHIFT+I et l'activation de la fenêtre (clic icône barre des tâches).
    /// Utilise KeyboardAccelerator WinUI : fonctionne quand la fenêtre a le focus.
    /// </summary>
    public partial class GlobalHotkeyService
    {
        /// <summary>
        /// À appeler une fois la fenêtre native disponible.
        /// </summary>
        public static void Register(
            Microsoft.UI.Xaml.Window window,
            Action onHotkey,
            Action onWindowActivated)
        {
            if (window == null)
            {
                return;
            }

            // 1. Raccourci CTRL+SHIFT+I
            if (window.Content is Microsoft.UI.Xaml.UIElement root)
            {
                var accelerator = new KeyboardAccelerator
                {
                    Modifiers = VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift,
                    Key = VirtualKey.I
                };

                accelerator.Invoked += (s, e) =>
                {
                    MainThread.BeginInvokeOnMainThread(() => onHotkey?.Invoke());
                    e.Handled = true;
                };

                root.KeyboardAccelerators.Add(accelerator);
            }

            // 2. Activation de la fenêtre (clic sur l'icône barre des tâches).
            bool wasMinimizedOrDeactivated = false;

            window.Activated += (s, e) =>
            {
                var state = e.WindowActivationState;

                if (state == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
                {
                    wasMinimizedOrDeactivated = true;
                    return;
                }

                // La fenêtre revient au premier plan : on ouvre la barre IA.
                if (wasMinimizedOrDeactivated)
                {
                    wasMinimizedOrDeactivated = false;
                    MainThread.BeginInvokeOnMainThread(() => onWindowActivated?.Invoke());
                }
            };
        }
    }
}
#endif
